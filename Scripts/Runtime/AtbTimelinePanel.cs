using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面下に表示するパーティのATBゲージ一覧。
/// TurnBattleManager から SetActors で渡されると行を作る。
/// </summary>
public class AtbTimelinePanel : MonoBehaviour
{
    [Header("レイアウト")]
    public RectTransform rowContainer;
    public AtbRow rowTemplate;

    private readonly List<AtbRow> rows = new List<AtbRow>();

    public void SetActors(List<BattleActor> actors)
    {
        Clear();
        if (rowContainer == null || rowTemplate == null || actors == null) return;

        foreach (var a in actors)
        {
            if (a == null) continue;
            AtbRow row = Instantiate(rowTemplate, rowContainer);
            row.gameObject.SetActive(true);
            row.Bind(a);
            rows.Add(row);
        }
    }

    public void Clear()
    {
        foreach (var r in rows) if (r != null) Destroy(r.gameObject);
        rows.Clear();
    }

    private void Update()
    {
        foreach (var r in rows) r?.Refresh();
    }

    public void HighlightActive(BattleActor active)
    {
        foreach (var r in rows) r?.SetHighlight(r.Actor == active);
    }
}
