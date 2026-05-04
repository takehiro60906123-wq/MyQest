using UnityEngine;

/// <summary>
/// 戦闘に参加するキャラの共通基底クラス。MP/ATB対応。
/// </summary>
public abstract class BattleActor : MonoBehaviour
{
    [Header("基本ステータス")]
    public string actorName = "Actor";
    public int maxHp = 30;
    public int maxMp = 10;
    public int attackPower = 5;

    [Header("ATB (Active Time Battle)")]
    [Tooltip("ATBゲージが満タンになる速度。1秒あたりにゲージが何%回復するか (0.0〜1.0)")]
    [Range(0.05f, 2f)]
    public float atbSpeed = 0.35f;
    [Tooltip("初期ゲージ値 (0〜1)。先制を作りたいキャラは大きく")]
    [Range(0f, 1f)]
    public float atbStart = 0f;

    [HideInInspector] public int currentHp;
    [HideInInspector] public int currentMp;
    [HideInInspector] public float atbValue;

    [Header("Animator (オプション)")]
    public Animator animator;
    public string idleSideStateName = "";
    public string attackStateName = "";
    public string hitStateName = "";

    public virtual bool IsAlive => currentHp > 0;
    public virtual bool IsPlayerSide => false;
    public bool IsAtbReady => atbValue >= 1f;

    protected virtual void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        ResetAll();
    }

    public virtual void ResetAll()
    {
        currentHp = Mathf.Max(1, maxHp);
        currentMp = Mathf.Max(0, maxMp);
        atbValue = Mathf.Clamp01(atbStart);
    }

    public virtual void ResetHp()
    {
        currentHp = Mathf.Max(1, maxHp);
    }

    public virtual int TakeDamage(int amount)
    {
        int dmg = Mathf.Max(0, amount);
        currentHp = Mathf.Max(0, currentHp - dmg);
        // 死亡したら ATB ゼロ
        if (!IsAlive) atbValue = 0f;
        PlayState(hitStateName);
        return dmg;
    }

    public virtual bool ConsumeMp(int amount)
    {
        if (currentMp < amount) return false;
        currentMp -= amount;
        return true;
    }

    public virtual void TickAtb(float deltaTime)
    {
        if (!IsAlive) { atbValue = 0f; return; }
        atbValue = Mathf.Clamp01(atbValue + atbSpeed * deltaTime);
    }

    public virtual void ConsumeAtb()
    {
        atbValue = 0f;
    }

    public virtual void PlayIdleSide() => PlayState(idleSideStateName);
    public virtual void PlayAttack()   => PlayState(attackStateName);

    protected void PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        if (animator.HasState(0, Animator.StringToHash(stateName)))
            animator.Play(stateName, 0, 0f);
    }

    public SpriteRenderer GetSprite()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        return sr;
    }
}
