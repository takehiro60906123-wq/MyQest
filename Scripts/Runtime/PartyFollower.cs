using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// パーティ仲間の追従。リーダーの軌跡(breadcrumb)を辿るDQ風。
/// CharacterAnimatorDriver と連携してアニメ・向きを切替える。
/// </summary>
public class PartyFollower : MonoBehaviour
{
    [Header("追従対象")]
    public Transform leader;
    [Tooltip("リーダーから何歩後ろを歩くか (0 が一番後ろ)")]
    public int orderInLine = 0;

    [Header("追従設定")]
    public float stepDistance = 0.45f;
    public float moveSpeed = 4.5f;
    public float teleportDistance = 8f;

    [Header("見た目")]
    [Tooltip("空なら自動取得")]
    public CharacterAnimatorDriver animatorDriver;

    private readonly Queue<Vector3> trail = new Queue<Vector3>();
    private Vector3 lastLeaderRecorded;
    private bool frozen;

    private void Start()
    {
        if (animatorDriver == null) animatorDriver = GetComponent<CharacterAnimatorDriver>();
        if (leader != null) lastLeaderRecorded = leader.position;
    }

    public void SetFrozen(bool value) => frozen = value;

    private void Update()
    {
        if (leader == null) return;

        if (frozen)
        {
            // 凍結中はIdle表示のみ
            if (animatorDriver != null) animatorDriver.UpdateMovement(Vector2.zero);
            return;
        }

        // ワープ条件 (取り残されたとき)
        if ((leader.position - transform.position).magnitude > teleportDistance)
        {
            transform.position = leader.position;
            trail.Clear();
            lastLeaderRecorded = leader.position;
            if (animatorDriver != null) animatorDriver.UpdateMovement(Vector2.zero);
            return;
        }

        // リーダーが stepDistance 進むごとに通過点を記録
        if ((leader.position - lastLeaderRecorded).magnitude >= stepDistance)
        {
            trail.Enqueue(lastLeaderRecorded);
            lastLeaderRecorded = leader.position;
            int maxQueue = Mathf.Max(4, orderInLine + 4);
            while (trail.Count > maxQueue) trail.Dequeue();
        }

        Vector2 velocity = Vector2.zero;

        if (trail.Count > orderInLine)
        {
            Vector3 target = PeekAt(orderInLine);
            velocity = MoveTowards(target);
        }

        // アニメ・反転をドライバに任せる
        if (animatorDriver != null)
            animatorDriver.UpdateMovement(velocity);
    }

    private Vector3 PeekAt(int index)
    {
        int i = 0;
        foreach (Vector3 v in trail)
        {
            if (i == index) return v;
            i++;
        }
        return transform.position;
    }

    /// <summary>1フレームで進んだ世界速度ベクトルを返す</summary>
    private Vector2 MoveTowards(Vector3 target)
    {
        Vector3 from = transform.position;
        Vector3 dir = target - from;
        float dist = dir.magnitude;

        if (dist <= 0.02f) return Vector2.zero;

        float step = moveSpeed * Time.deltaTime;
        if (step > dist) step = dist;
        Vector3 displacement = dir.normalized * step;
        transform.position = from + displacement;

        // 1秒換算速度を返す (アニメ判定の閾値が「速度」基準)
        return new Vector2(displacement.x, displacement.y) / Mathf.Max(0.0001f, Time.deltaTime);
    }
}
