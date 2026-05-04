using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 戦闘参加全員が1本のトラックを走るATB表示。
/// グランディアIPゲージ・FF6プレビュー・オクトパスのターン順表示風。
/// 左端: 待機 / 右端: 行動可
/// </summary>
public class AtbRacePanel : MonoBehaviour
{
    [Header("レイアウト")]
    [Tooltip("ランナーアイコンが移動する横長トラック (Imageでもただの矩形でも可)")]
    public RectTransform track;
    [Tooltip("ランナーアイコンの親 (track の中に置くのが普通)")]
    public RectTransform runnerContainer;
    [Tooltip("ランナーアイコンのテンプレート")]
    public AtbRunner runnerTemplate;

    [Header("色テーマ")]
    public Color playerColor = new Color(0.45f, 0.85f, 1.0f);
    public Color enemyColor  = new Color(1.00f, 0.55f, 0.55f);
    public Color readyColor  = new Color(1.0f, 0.95f, 0.4f);

    [Header("配置")]
    [Tooltip("アイコンY座標のジグザグ量。同じ位置で重なるのを軽減 (0で完全水平)")]
    public float yJitter = 12f;
    [Tooltip("敵側のYオフセット (上下の段に分けたいとき)")]
    public float enemyYOffset = -16f;

    private readonly List<AtbRunner> runners = new List<AtbRunner>();

    public void SetActors(List<BattleActor> playerActors, List<BattleActor> enemyActors)
    {
        Clear();
        if (runnerContainer == null || runnerTemplate == null) return;

        int idx = 0;
        if (playerActors != null)
        {
            foreach (var a in playerActors)
            {
                if (a == null) continue;
                AddRunner(a, true, idx++);
            }
        }
        if (enemyActors != null)
        {
            foreach (var a in enemyActors)
            {
                if (a == null) continue;
                AddRunner(a, false, idx++);
            }
        }
    }

    private void AddRunner(BattleActor a, bool isPlayer, int orderForJitter)
    {
        AtbRunner r = Instantiate(runnerTemplate, runnerContainer);
        r.gameObject.SetActive(true);

        Color baseColor = isPlayer ? playerColor : enemyColor;
        float yOffset = (orderForJitter % 2 == 0 ? -1f : 1f) * yJitter
                        + (isPlayer ? 0f : enemyYOffset);
        r.Bind(a, baseColor, readyColor, yOffset);
        runners.Add(r);
    }

    public void Clear()
    {
        foreach (var r in runners) if (r != null) Destroy(r.gameObject);
        runners.Clear();
    }

    private void Update()
    {
        if (track == null) return;
        // トラック幅
        float trackWidth = track.rect.width;
        foreach (var r in runners) r?.Refresh(trackWidth);
    }

    public void HighlightActive(BattleActor active)
    {
        foreach (var r in runners) r?.SetHighlight(r.Actor == active);
    }
}
