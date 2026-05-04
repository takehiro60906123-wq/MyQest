using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyStatus))]
[RequireComponent(typeof(Collider2D))]
public class MapEnemyEncounter : MonoBehaviour
{
    [Header("バトル管理")]
    public TurnBattleManager battleManager;
    public BattleCameraController cameraController;
    public BattleTransitionOverlay transitionOverlay;

    [Header("味方/敵パーティ (省略可)")]
    public List<Transform> playerParty = new List<Transform>();
    public List<Transform> enemyParty = new List<Transform>();

    [Header("開始演出")]
    public float startDelay = 0.15f;
    public float enemyPopScale = 1.10f;
    public float popTime = 0.10f;
    public float runCooldown = 1.0f;

    [Header("立ち位置")]
    public float minGapBetweenParties = 0.5f;
    public float memberSpacing = 0.9f;
    public float zigzagDepth = 0.35f;
    [Range(-0.4f, 0.4f)] public float battleStationYRatio = 0.08f;
    public float repositionTime = 0.30f;

    [Header("カメラ動作")]
    public bool keepCameraStill = true;

    private EnemyStatus enemyStatus;
    private Collider2D triggerCollider;
    private MapEnemyPatrol2D patrol;
    private bool battleStarted;
    private Vector3 originalScale;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        triggerCollider = GetComponent<Collider2D>();
        patrol = GetComponent<MapEnemyPatrol2D>();
        originalScale = transform.localScale;
        if (triggerCollider != null) triggerCollider.isTrigger = true;
    }

    private void Start()
    {
        if (battleManager == null) battleManager = FindObjectOfType<TurnBattleManager>();
        if (cameraController == null) cameraController = FindObjectOfType<BattleCameraController>();
        if (transitionOverlay == null) transitionOverlay = FindObjectOfType<BattleTransitionOverlay>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (battleStarted) return;
        PS5PlayerController player = other.GetComponent<PS5PlayerController>();
        if (player == null) return;
        if (battleManager == null) { Debug.LogError("TurnBattleManager not found", this); return; }
        StartCoroutine(StartSeamlessBattle(player));
    }

    private IEnumerator StartSeamlessBattle(PS5PlayerController player)
    {
        battleStarted = true;
        SetPatrolActive(false);
        player.SetControl(false);

        // ── プレイヤー側集合 ──
        BattleActor leaderActor = player.GetComponent<BattleActor>();
        if (leaderActor == null) leaderActor = player.gameObject.AddComponent<PartyMember>();

        List<BattleActor> playerActors = new List<BattleActor> { leaderActor };
        if (playerParty != null)
        {
            foreach (var t in playerParty)
            {
                if (t == null || t == player.transform) continue;
                BattleActor a = t.GetComponent<BattleActor>();
                if (a == null) a = t.gameObject.AddComponent<PartyMember>();
                if (!playerActors.Contains(a)) playerActors.Add(a);
            }
        }

        // ── 敵側集合 ──
        List<BattleActor> enemyActors = new List<BattleActor> { enemyStatus };
        if (enemyParty != null)
        {
            foreach (var t in enemyParty)
            {
                if (t == null || t == transform) continue;
                BattleActor a = t.GetComponent<BattleActor>();
                if (a != null && !enemyActors.Contains(a)) enemyActors.Add(a);
            }
        }

        FreezeFollowers(playerActors, true);
        // 敵パーティの徘徊も全て止める
        SetEnemyPartyPatrolActive(enemyActors, false);

        // ── 立ち位置計算 ──
        List<Transform> playerXfs = ToTransforms(playerActors);
        List<Transform> enemyXfs = ToTransforms(enemyActors);

        Vector3 playerCenter = AverageOf(playerXfs);
        Vector3 enemyCenter = AverageOf(enemyXfs);
        Vector3 midpoint = (playerCenter + enemyCenter) * 0.5f;
        bool playerOnLeft = playerCenter.x <= enemyCenter.x;

        float requiredSpacing = BattleFormation.CalculateRequiredSpacing(
            playerXfs[0], enemyXfs[0], minimumGap: minGapBetweenParties, fallback: 2.4f);

        Camera cam = (cameraController != null && cameraController.targetCamera != null)
            ? cameraController.targetCamera : Camera.main;

        var (leftCenter, rightCenter) = BattleFormation.GetSideCenters(
            encounterMidpoint: midpoint,
            camera: keepCameraStill ? cam : null,
            pairSpacing: requiredSpacing,
            yOffsetRatio: battleStationYRatio,
            playerOnLeft: playerOnLeft);

        Vector3 playerSideCenter = playerOnLeft ? leftCenter : rightCenter;
        Vector3 enemySideCenter = playerOnLeft ? rightCenter : leftCenter;

        Vector3[] pStations = BattleFormation.GetPartyStations(playerSideCenter, playerActors.Count, playerOnLeft, memberSpacing, zigzagDepth);
        Vector3[] eStations = BattleFormation.GetPartyStations(enemySideCenter, enemyActors.Count, !playerOnLeft, memberSpacing, zigzagDepth);

        // 移動
        List<Coroutine> moves = new List<Coroutine>();
        for (int i = 0; i < playerActors.Count; i++)
        {
            Vector3 to = pStations[i]; to.z = playerActors[i].transform.position.z;
            moves.Add(StartCoroutine(SmoothMove(playerActors[i].transform, to, repositionTime)));
        }
        for (int i = 0; i < enemyActors.Count; i++)
        {
            Vector3 to = eStations[i]; to.z = enemyActors[i].transform.position.z;
            moves.Add(StartCoroutine(SmoothMove(enemyActors[i].transform, to, repositionTime)));
        }
        foreach (var c in moves) yield return c;

        // 向き合わせ
        foreach (var a in playerActors)
        {
            FlipTowards(a.transform, enemySideCenter);
            a.PlayIdleSide();
            PS5PlayerController pc = a.GetComponent<PS5PlayerController>();
            if (pc != null) pc.FaceTarget(enemySideCenter);
        }
        foreach (var a in enemyActors)
        {
            FlipTowards(a.transform, playerSideCenter);
            a.PlayIdleSide();
        }

        if (!keepCameraStill && cameraController != null)
            yield return StartCoroutine(cameraController.FocusBattle(player.transform, transform));

        yield return new WaitForSeconds(startDelay);
        yield return StartCoroutine(PopAnimation());

        if (transitionOverlay != null)
            yield return StartCoroutine(transitionOverlay.Flash());

        battleManager.StartBattle(
            player, playerActors, enemyActors,
            onWin: () =>
            {
                if (cameraController != null && !keepCameraStill)
                    cameraController.StartCoroutine(cameraController.ResetCamera());
                FreezeFollowers(playerActors, false);
                // ★勝利: 敵オブジェクト破棄。徘徊スクリプトも一緒に消える
                foreach (var a in enemyActors) if (a != null) Destroy(a.gameObject);
            },
            onRun: () =>
            {
                if (cameraController != null && !keepCameraStill)
                    cameraController.StartCoroutine(cameraController.ResetCamera());
                FreezeFollowers(playerActors, false);
                // ★逃走: 徘徊復帰させない (= もう近寄っても再戦闘しないように)
                //   トリガーをクールダウンの後に再有効化する従来動作を保つ
                StartCoroutine(RunCooldownRoutine(enemyActors, resumePatrol: true));
            },
            onLose: () =>
            {
                if (cameraController != null && !keepCameraStill)
                    cameraController.StartCoroutine(cameraController.ResetCamera());
                FreezeFollowers(playerActors, false);
                // ★敗北: 徘徊復帰
                StartCoroutine(RunCooldownRoutine(enemyActors, resumePatrol: true));
            }
        );
    }

    /// <summary>自分のpatrolをON/OFF</summary>
    private void SetPatrolActive(bool active)
    {
        if (patrol != null) patrol.SetFrozen(!active);
    }

    /// <summary>敵パーティ全員のpatrolをON/OFF</summary>
    private void SetEnemyPartyPatrolActive(List<BattleActor> enemies, bool active)
    {
        foreach (var a in enemies)
        {
            if (a == null) continue;
            MapEnemyPatrol2D pat = a.GetComponent<MapEnemyPatrol2D>();
            if (pat != null) pat.SetFrozen(!active);
        }
    }

    private static void FreezeFollowers(List<BattleActor> actors, bool freeze)
    {
        if (actors == null) return;
        foreach (var a in actors)
        {
            if (a == null) continue;
            PartyFollower pf = a.GetComponent<PartyFollower>();
            if (pf != null) pf.SetFrozen(freeze);
        }
    }

    private static List<Transform> ToTransforms(List<BattleActor> list)
    {
        List<Transform> r = new List<Transform>();
        foreach (var a in list) if (a != null) r.Add(a.transform);
        return r;
    }

    private static Vector3 AverageOf(List<Transform> list)
    {
        if (list == null || list.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var t in list) { if (t == null) continue; sum += t.position; n++; }
        return n > 0 ? sum / n : Vector3.zero;
    }

    private IEnumerator SmoothMove(Transform actor, Vector3 to, float duration)
    {
        if (actor == null) yield break;
        if (duration <= 0f) { actor.position = to; yield break; }

        Vector3 from = actor.position;
        if ((to - from).sqrMagnitude < 0.0001f) { actor.position = to; yield break; }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / duration);
            actor.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        actor.position = to;
    }

    private static void FlipTowards(Transform actor, Vector3 facePosition)
    {
        if (actor == null) return;
        SpriteRenderer sr = actor.GetComponent<SpriteRenderer>();
        if (sr == null) sr = actor.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        sr.flipX = facePosition.x < actor.position.x;
    }

    private IEnumerator PopAnimation()
    {
        float timer = 0f;
        Vector3 targetScale = originalScale * enemyPopScale;
        while (timer < popTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / popTime);
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }
        timer = 0f;
        while (timer < popTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / popTime);
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }
        transform.localScale = originalScale;
    }

    private IEnumerator RunCooldownRoutine(List<BattleActor> enemyActors, bool resumePatrol)
    {
        if (triggerCollider != null) triggerCollider.enabled = false;
        yield return new WaitForSeconds(runCooldown);

        if (resumePatrol)
            SetEnemyPartyPatrolActive(enemyActors, true);

        if (triggerCollider != null) triggerCollider.enabled = true;
        battleStarted = false;
    }
}
