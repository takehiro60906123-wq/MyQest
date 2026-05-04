using UnityEngine;

/// <summary>
/// 移動方向に応じてキャラのアニメ状態 (Idle_Down/Run_Side/...) を切り替え、
/// 横向きのときは flipX で左右反転する共通ドライバ。
/// </summary>
public class CharacterAnimatorDriver : MonoBehaviour
{
    [Header("参照 (空なら子から自動取得)")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("ステート名 (Animator の State 名と一致させる)")]
    public string idleDown = "Swordman_Idle_Down";
    public string idleUp   = "Swordman_Idle_Up";
    public string idleSide = "Swordman_Idle_Side";
    public string runDown  = "Swordman_Run_Down";
    public string runUp    = "Swordman_Run_Up";
    public string runSide  = "Swordman_Run_Side";

    [Header("挙動")]
    [Tooltip("動いている判定の最小速度。これ以下はIdle扱い")]
    public float movementThreshold = 0.05f;
    public bool replayOnlyOnStateChange = true;
    public bool reapplyFlipInLateUpdate = true;

    [Header("デバッグ")]
    [Tooltip("毎フレConsoleに状態を出力。原因切り分けのため一時的にON。")]
    public bool debugLog = false;
    [Tooltip("ON: dir.x が dir.y より少しでも大きければ横扱い (推奨)\nOFF: 厳密判定")]
    public bool preferHorizontalOnTie = true;

    private string lastStateName = "";
    private Vector2 lastFacingDirection = Vector2.down;
    private bool hasFacing;
    private bool desiredFlipX;
    private string lastDebugStatus = "";

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void UpdateMovement(Vector2 worldVelocity)
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) return;

        bool moving = worldVelocity.magnitude >= movementThreshold;
        Vector2 dir = moving ? worldVelocity.normalized : lastFacingDirection;

        if (moving)
        {
            lastFacingDirection = dir;
            hasFacing = true;
        }

        // 横優勢の判定: 浮動小数の誤差や微小縦入力に強くするため、
        // |x| > |y| - epsilon にする。preferHorizontalOnTie がOFFのときは厳密判定。
        bool horizontalDominant;
        if (preferHorizontalOnTie)
            horizontalDominant = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) - 0.0001f && Mathf.Abs(dir.x) > 0.01f;
        else
            horizontalDominant = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y);

        string stateName;
        if (horizontalDominant)
        {
            stateName = moving ? runSide : idleSide;
            if (hasFacing) desiredFlipX = (dir.x < 0f);
        }
        else
        {
            if (dir.y > 0f) stateName = moving ? runUp   : idleUp;
            else            stateName = moving ? runDown : idleDown;
            // 上下のときは flipX を維持 (リセットしない)
            // ←重要: ここでリセットすると上下移動入った瞬間 flipX が消える
        }

        ApplyFlipX();
        PlayState(stateName);

        if (debugLog)
        {
            string status = $"vel={worldVelocity:F3} dir={dir:F3} horiz={horizontalDominant} state={stateName} flipX={desiredFlipX} sr={(spriteRenderer != null ? spriteRenderer.name : "null")}";
            if (status != lastDebugStatus)
            {
                Debug.Log($"[AnimDriver:{name}] {status}");
                lastDebugStatus = status;
            }
        }
    }

    public void FaceDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        lastFacingDirection = dir.normalized;
        hasFacing = true;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y) - 0.0001f && Mathf.Abs(dir.x) > 0.01f)
            desiredFlipX = (dir.x < 0f);
        // 縦向き優勢のときは現在のflipXを変えない

        ApplyFlipX();
        UpdateMovement(Vector2.zero);
    }

    public void PlayStateRaw(string stateName) => PlayState(stateName);

    private void PlayState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || animator == null) return;

        if (replayOnlyOnStateChange && stateName == lastStateName)
            return;

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash))
            return;

        animator.Play(hash, 0, 0f);
        lastStateName = stateName;
    }

    private void ApplyFlipX()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.flipX = desiredFlipX;
    }

    private void LateUpdate()
    {
        if (!reapplyFlipInLateUpdate) return;
        if (spriteRenderer != null)
            spriteRenderer.flipX = desiredFlipX;
    }
}
