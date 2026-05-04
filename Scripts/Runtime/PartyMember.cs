using UnityEngine;

/// <summary>
/// プレイヤー陣営のメンバー。リーダー(操作キャラ)もフォロワーも同じこれを付ける。
/// </summary>
public class PartyMember : BattleActor
{
    public override bool IsPlayerSide => true;

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(actorName))
            actorName = gameObject.name;

        if (string.IsNullOrEmpty(idleSideStateName)) idleSideStateName = "Swordman_Idle_Side";
        if (string.IsNullOrEmpty(attackStateName))   attackStateName   = "Swordman_Attack_Side";

        // プレイヤー側はデフォでMP持ち
        if (maxMp <= 0) maxMp = 10;

        base.Awake();
    }
}
