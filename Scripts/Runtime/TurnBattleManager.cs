using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TurnBattleManager : MonoBehaviour
{
    [Header("UI ルート")]
    public GameObject battlePanel;
    public Text messageText;
    public Button attackButton;
    public Button runButton;
    public RectTransform damageTextParent;

    [Header("ATBレース表示 (1本のトラックを全員が走る)")]
    public AtbRacePanel atbRace;

    [Header("対象選択カーソル")]
    public BattleTargetCursor targetCursor;

    [Header("足元ゲージ")]
    public BattleActorOverhead overheadTemplate;

    [Header("演出")]
    public BattleCameraController cameraController;
    public float actionWait = 0.4f;
    public float lungeDistance = 0.45f;
    public float lungeTime = 0.12f;

    [Header("ATB設定")]
    public bool freezeAtbDuringCommand = true;
    public bool freezeAtbDuringAction = true;

    [Header("ダメージフォント")]
    public Font damageFont;

    [Header("挙動")]
    public bool autoBindButtons = true;
    [Range(0f, 1f)] public float runSuccessChance = 0.85f;

    private readonly List<BattleActor> playerSide = new List<BattleActor>();
    private readonly List<BattleActor> enemySide = new List<BattleActor>();
    private readonly Dictionary<BattleActor, BattleActorOverhead> overheads = new Dictionary<BattleActor, BattleActorOverhead>();

    private bool battleActive;
    private bool waitingForPlayerCommand;
    private BattleActor activeActor;
    private bool actionInProgress;
    private float nextPadInputTime;
    private bool runRequested;

    private PS5PlayerController activePlayerController;
    private Action onWin, onRun, onLose;

    private void Awake()
    {
        if (battlePanel != null) battlePanel.SetActive(false);
        if (cameraController == null) cameraController = FindObjectOfType<BattleCameraController>();

        if (autoBindButtons)
        {
            if (attackButton != null) attackButton.onClick.AddListener(OnAttackPressed);
            if (runButton != null) runButton.onClick.AddListener(OnRunPressed);
        }
    }

    private void Update()
    {
        if (!battleActive) return;

        bool tickAllowed = !(waitingForPlayerCommand && freezeAtbDuringCommand)
                        && !(actionInProgress && freezeAtbDuringAction);
        if (tickAllowed)
        {
            float dt = Time.deltaTime;
            foreach (var a in playerSide) if (a != null) a.TickAtb(dt);
            foreach (var a in enemySide)  if (a != null) a.TickAtb(dt);
        }

        if (waitingForPlayerCommand && Time.unscaledTime >= nextPadInputTime)
        {
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.wasPressedThisFrame) { nextPadInputTime = Time.unscaledTime + 0.2f; OnAttackPressed(); }
                else if (Gamepad.current.buttonEast.wasPressedThisFrame) { nextPadInputTime = Time.unscaledTime + 0.2f; OnRunPressed(); }
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) OnAttackPressed();
                else if (Keyboard.current.escapeKey.wasPressedThisFrame) OnRunPressed();
            }
        }

        if (!waitingForPlayerCommand && !actionInProgress && activeActor == null)
        {
            BattleActor next = PickNextReady();
            if (next != null) StartCoroutine(BeginTurn(next));
        }
    }

    public void StartBattle(
        PS5PlayerController playerController,
        List<BattleActor> playerActors,
        List<BattleActor> enemyActors,
        Action onWin, Action onRun, Action onLose = null)
    {
        this.activePlayerController = playerController;
        this.onWin = onWin;
        this.onRun = onRun;
        this.onLose = onLose;
        this.runRequested = false;

        playerSide.Clear();
        enemySide.Clear();
        foreach (var a in playerActors) if (a != null) { a.ResetAll(); playerSide.Add(a); a.PlayIdleSide(); }
        foreach (var a in enemyActors)  if (a != null) { a.ResetAll(); enemySide.Add(a);  a.PlayIdleSide(); }

        BuildOverheads();

        if (atbRace != null)
            atbRace.SetActors(new List<BattleActor>(playerSide), new List<BattleActor>(enemySide));

        battleActive = true;
        waitingForPlayerCommand = false;
        actionInProgress = false;
        activeActor = null;

        if (battlePanel != null) battlePanel.SetActive(true);

        SetMessage(BuildOpeningMessage());
        SetButtons(false);
    }

    public void StartBattle(PS5PlayerController player, EnemyStatus enemy, Action onWin, Action onRun, Action onLose = null)
    {
        var p = new List<BattleActor>();
        var e = new List<BattleActor>();
        BattleActor leaderActor = player.GetComponent<BattleActor>();
        if (leaderActor == null) leaderActor = player.gameObject.AddComponent<PartyMember>();
        p.Add(leaderActor); e.Add(enemy);
        StartBattle(player, p, e, onWin, onRun, onLose);
    }

    private BattleActor PickNextReady()
    {
        BattleActor best = null;
        float bestVal = 0f;
        foreach (var a in playerSide)
            if (a != null && a.IsAlive && a.IsAtbReady && a.atbValue > bestVal) { best = a; bestVal = a.atbValue; }
        foreach (var a in enemySide)
            if (a != null && a.IsAlive && a.IsAtbReady && a.atbValue > bestVal) { best = a; bestVal = a.atbValue; }
        return best;
    }

    private IEnumerator BeginTurn(BattleActor actor)
    {
        activeActor = actor;
        if (atbRace != null) atbRace.HighlightActive(actor);

        if (actor.IsPlayerSide)
        {
            SetMessage($"{actor.actorName} のターン  どうする？");
            SetButtons(true);
            waitingForPlayerCommand = true;
            yield break;
        }
        else
        {
            yield return StartCoroutine(EnemyAi(actor));
        }
    }

    private void OnAttackPressed()
    {
        if (!battleActive || !waitingForPlayerCommand || activeActor == null) return;
        if (!activeActor.IsPlayerSide) return;

        waitingForPlayerCommand = false;
        SetButtons(false);

        var aliveEnemies = AliveOf(enemySide);
        if (aliveEnemies.Count == 0) { StartCoroutine(WinBattle()); return; }

        if (targetCursor != null && aliveEnemies.Count > 1)
        {
            SetMessage("攻撃する敵を選択");
            BattleActor attacker = activeActor;
            targetCursor.Show(aliveEnemies,
                onConfirm: chosen => StartCoroutine(PlayerAttackRoutine(attacker, chosen)),
                onCancel: () =>
                {
                    SetMessage($"{attacker.actorName} のターン  どうする？");
                    SetButtons(true);
                    waitingForPlayerCommand = true;
                });
        }
        else
        {
            StartCoroutine(PlayerAttackRoutine(activeActor, aliveEnemies[0]));
        }
    }

    private void OnRunPressed()
    {
        if (!battleActive || !waitingForPlayerCommand) return;
        waitingForPlayerCommand = false;
        SetButtons(false);
        StartCoroutine(RunAwayRoutine());
    }

    private IEnumerator PlayerAttackRoutine(BattleActor attacker, BattleActor target)
    {
        actionInProgress = true;
        SetMessage($"{attacker.actorName} の攻撃！");

        yield return StartCoroutine(Lunge(attacker.transform, target.transform.position));
        attacker.PlayAttack();

        bool critical = UnityEngine.Random.value < 0.12f;
        int damage = critical ? Mathf.RoundToInt(attacker.attackPower * 1.6f) : attacker.attackPower;

        BattleVfx.Instance.SpawnSlash(target.transform.position, UnityEngine.Random.Range(20f, 50f), GetSortingOrderFront(target));
        BattleVfx.Instance.SpawnImpactRing(target.transform.position, new Color(1f, 0.95f, 0.6f, 0.85f), GetSortingOrderFront(target));

        SpriteRenderer sr = target.GetSprite();
        BattleVfx.Instance.HitFlash(sr, 0.18f, critical ? new Color(1f, 0.5f, 0.5f) : Color.white);

        int dealt = target.TakeDamage(damage);
        SpawnFloatingText(dealt.ToString(), target.transform, critical);

        if (cameraController != null)
            cameraController.Shake(critical ? 0.22f : 0.16f, critical ? 0.14f : 0.08f);

        SetMessage(critical
            ? $"会心の一撃！ {target.actorName} に {dealt} のダメージ！"
            : $"{target.actorName} に {dealt} のダメージ！");

        yield return new WaitForSeconds(actionWait);

        attacker.ConsumeAtb();
        FinishTurn();
    }

    private IEnumerator EnemyAi(BattleActor enemy)
    {
        actionInProgress = true;
        var alivePlayers = AliveOf(playerSide);
        if (alivePlayers.Count == 0) { StartCoroutine(LoseBattle()); yield break; }

        BattleActor target = alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];

        SetMessage($"{enemy.actorName} の攻撃！");
        yield return StartCoroutine(Lunge(enemy.transform, target.transform.position));
        enemy.PlayAttack();

        int damage = enemy.attackPower;
        SpriteRenderer sr = target.GetSprite();
        BattleVfx.Instance.HitFlash(sr, 0.18f, new Color(1f, 0.6f, 0.6f));
        BattleVfx.Instance.SpawnImpactRing(target.transform.position, new Color(1f, 0.4f, 0.4f, 0.8f), GetSortingOrderFront(target));

        int dealt = target.TakeDamage(damage);
        SpawnFloatingText(dealt.ToString(), target.transform, false);
        if (cameraController != null) cameraController.Shake(0.14f, 0.06f);

        SetMessage($"{target.actorName} は {dealt} のダメージ！");
        yield return new WaitForSeconds(actionWait);

        enemy.ConsumeAtb();
        FinishTurn();
    }

    private void FinishTurn()
    {
        actionInProgress = false;
        activeActor = null;
        if (atbRace != null) atbRace.HighlightActive(null);

        if (!HasAlive(enemySide)) { StartCoroutine(WinBattle()); return; }
        if (!HasAlive(playerSide)) { StartCoroutine(LoseBattle()); return; }
    }

    private IEnumerator RunAwayRoutine()
    {
        actionInProgress = true;
        if (UnityEngine.Random.value <= runSuccessChance)
        {
            SetMessage("うまく逃げきった！");
            if (activeActor != null) activeActor.ConsumeAtb();
            yield return new WaitForSeconds(0.55f);
            actionInProgress = false; activeActor = null;
            EndBattle(); onRun?.Invoke();
        }
        else
        {
            SetMessage("逃げられなかった！");
            if (activeActor != null) activeActor.ConsumeAtb();
            yield return new WaitForSeconds(0.55f);
            FinishTurn();
        }
    }

    private IEnumerator WinBattle()
    {
        battleActive = false;
        waitingForPlayerCommand = false;
        actionInProgress = false;
        SetButtons(false);

        int totalExp = 0, totalGold = 0;
        foreach (var e in enemySide)
        {
            EnemyStatus es = e as EnemyStatus;
            if (es != null) { totalExp += es.exp; totalGold += es.gold; }
        }
        SetMessage($"敵をすべて倒した!  EXP {totalExp} / Gold {totalGold} を獲得！");
        yield return new WaitForSeconds(1.1f);

        EndBattle(); onWin?.Invoke();
    }

    private IEnumerator LoseBattle()
    {
        battleActive = false;
        waitingForPlayerCommand = false;
        actionInProgress = false;
        SetButtons(false);
        SetMessage("パーティは全滅した……");
        yield return new WaitForSeconds(1.2f);

        foreach (var p in playerSide) p.ResetHp();
        EndBattle(); onLose?.Invoke();
    }

    private void EndBattle()
    {
        if (battlePanel != null) battlePanel.SetActive(false);
        battleActive = false;
        waitingForPlayerCommand = false;
        actionInProgress = false;
        activeActor = null;

        if (atbRace != null) atbRace.Clear();
        ClearOverheads();

        if (activePlayerController != null) activePlayerController.SetControl(true);
    }

    private void BuildOverheads()
    {
        ClearOverheads();
        if (overheadTemplate == null) return;

        foreach (var a in playerSide) AttachOverhead(a);
        foreach (var a in enemySide)  AttachOverhead(a);
    }

    private void AttachOverhead(BattleActor a)
    {
        if (a == null) return;

        BattleActorOverhead existing = a.GetComponentInChildren<BattleActorOverhead>(true);
        if (existing != null)
        {
            existing.actor = a;
            existing.SetVisible(true);
            existing.Refresh();
            overheads[a] = existing;
            return;
        }

        if (overheadTemplate == null) return;

        BattleActorOverhead inst = Instantiate(overheadTemplate, a.transform);
        inst.gameObject.SetActive(true);
        inst.actor = a;
        inst.transform.localPosition = inst.worldOffset;
        inst.Refresh();
        overheads[a] = inst;
    }

    private void ClearOverheads()
    {
        foreach (var kv in overheads)
        {
            if (kv.Value == null) continue;
            kv.Value.SetVisible(false);
        }
        overheads.Clear();
    }

    private static bool HasAlive(List<BattleActor> list)
    {
        foreach (var a in list) if (a != null && a.IsAlive) return true;
        return false;
    }
    private static List<BattleActor> AliveOf(List<BattleActor> list)
    {
        var r = new List<BattleActor>();
        foreach (var a in list) if (a != null && a.IsAlive) r.Add(a);
        return r;
    }

    private string BuildOpeningMessage()
    {
        if (enemySide.Count == 1) return $"{enemySide[0].actorName} があらわれた！";
        return $"敵 {enemySide.Count} 体があらわれた！";
    }

    private void SetButtons(bool on)
    {
        if (attackButton != null) attackButton.interactable = on;
        if (runButton != null) runButton.interactable = on;
    }

    private void SetMessage(string s) { if (messageText != null) messageText.text = s; }

    private IEnumerator Lunge(Transform actor, Vector3 targetPosition)
    {
        if (actor == null) yield break;
        Vector3 start = actor.position;
        Vector3 dir = targetPosition - start;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        Vector3 attackPos = start + dir.normalized * lungeDistance;

        float timer = 0f;
        while (timer < lungeTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / lungeTime);
            actor.position = Vector3.Lerp(start, attackPos, t);
            yield return null;
        }
        timer = 0f;
        while (timer < lungeTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / lungeTime);
            actor.position = Vector3.Lerp(attackPos, start, t);
            yield return null;
        }
        actor.position = start;
    }

    private static int GetSortingOrderFront(Component c)
    {
        if (c == null) return 100;
        SpriteRenderer sr = c.GetComponent<SpriteRenderer>();
        if (sr == null) sr = c.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.sortingOrder + 1 : 100;
    }

    private void SpawnFloatingText(string value, Transform target, bool critical)
    {
        if (damageTextParent == null || target == null) return;

        GameObject go = new GameObject("FloatingDamageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline), typeof(FloatingDamageText));
        go.transform.SetParent(damageTextParent, false);

        Text text = go.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = critical ? 56 : 40;
        text.fontStyle = critical ? FontStyle.Bold : FontStyle.Normal;
        text.color = critical ? new Color(1f, 0.85f, 0.35f) : Color.white;
        text.font = damageFont != null ? damageFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220f, 80f);

        Camera cam = Camera.main;
        Vector3 worldPos = target.position + Vector3.up * 0.8f;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(damageTextParent, screenPos, null, out localPos);

        go.GetComponent<FloatingDamageText>().Play(value, localPos);
    }
}
