using UnityEngine;

/// <summary>
/// マップ上の敵。BattleActor を継承して 1v1/NvM どちらでも使える。
/// 既存フィールド名 (enemyName, exp, gold) は互換維持。
/// </summary>
public class EnemyStatus : BattleActor
{
    [Header("敵固有")]
    public string enemyName = "Goblin";

    [Header("報酬")]
    public int exp = 5;
    public int gold = 3;

    public override bool IsPlayerSide => false;

    protected override void Awake()
    {
        if (!string.IsNullOrEmpty(enemyName)) actorName = enemyName;
        else if (!string.IsNullOrEmpty(actorName)) enemyName = actorName;

        if (string.IsNullOrEmpty(idleSideStateName)) idleSideStateName = "Idle";
        if (string.IsNullOrEmpty(attackStateName))   attackStateName   = "Attack";

        // 敵はデフォルトMPなし (足元バーで非表示になる)
        // 必要なら個別にinspectorで設定する

        base.Awake();
    }

    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(enemyName)) actorName = enemyName;
    }
}
