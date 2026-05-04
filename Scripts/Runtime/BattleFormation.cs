using UnityEngine;

/// <summary>
/// 戦闘隊列の立ち位置を計算するユーティリティ。
/// プレイヤーN人 vs 敵M匹でも展開できるように設計。
/// </summary>
public static class BattleFormation
{
    /// <summary>
    /// 1陣営分の立ち位置を計算する。<br/>
    /// 中央を基準に、メンバーを縦に並べ、奇数番目を少し奥にずらして奥行き感を出す。
    /// </summary>
    /// <param name="sideCenter">この陣営の中心座標</param>
    /// <param name="count">メンバー数</param>
    /// <param name="isLeftSide">true: 左側陣営(プレイヤー), false: 右側陣営(敵)</param>
    /// <param name="memberSpacing">メンバー間の縦距離 (world unit)</param>
    /// <param name="zigzagDepth">奥行きずらし量 (0で完全縦並び)</param>
    public static Vector3[] GetPartyStations(
        Vector3 sideCenter,
        int count,
        bool isLeftSide,
        float memberSpacing = 0.9f,
        float zigzagDepth = 0.4f)
    {
        Vector3[] result = new Vector3[Mathf.Max(0, count)];
        if (count <= 0) return result;
        if (count == 1)
        {
            result[0] = sideCenter;
            return result;
        }

        for (int i = 0; i < count; i++)
        {
            float y = (i - (count - 1) * 0.5f) * memberSpacing;
            float zigX = (i % 2 == 0) ? 0f : (isLeftSide ? -zigzagDepth : zigzagDepth);
            result[i] = sideCenter + new Vector3(zigX, y, 0f);
        }
        return result;
    }

    /// <summary>
    /// 両陣営の中心座標を計算する。
    /// 接触地点の中点を基準に左右へ広げ、Yはカメラを使って画面UI領域を避ける位置に置く。
    /// </summary>
    public static (Vector3 leftCenter, Vector3 rightCenter) GetSideCenters(
        Vector3 encounterMidpoint,
        Camera camera,
        float pairSpacing,
        float yOffsetRatio,
        bool playerOnLeft)
    {
        float battleY = encounterMidpoint.y;
        if (camera != null && camera.orthographic)
        {
            // カメラを動かさない前提: 画面UIを避けるため screen-relative な Y にする
            battleY = camera.transform.position.y + camera.orthographicSize * yOffsetRatio;
        }

        float half = pairSpacing * 0.5f;
        Vector3 left  = new Vector3(encounterMidpoint.x - half, battleY, encounterMidpoint.z);
        Vector3 right = new Vector3(encounterMidpoint.x + half, battleY, encounterMidpoint.z);
        return (left, right);
    }

    /// <summary>
    /// 対峙する2陣営の最小間隔をスプライト境界から計算する。
    /// 大きい敵スプライトでも重ならないように。
    /// </summary>
    public static float CalculateRequiredSpacing(
        Transform leftSampleSprite,
        Transform rightSampleSprite,
        float minimumGap = 0.4f,
        float fallback = 2.4f)
    {
        float lw = GetSpriteHalfWidth(leftSampleSprite);
        float rw = GetSpriteHalfWidth(rightSampleSprite);
        if (lw <= 0f && rw <= 0f) return fallback;
        return lw + rw + minimumGap;
    }

    public static float GetSpriteHalfWidth(Transform t)
    {
        if (t == null) return 0f;
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return 0f;
        // bounds.size.x は world scale 適用後の幅
        return sr.bounds.size.x * 0.5f;
    }
}
