using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AtbRacePanel のトラック上を走る1ランナー (=戦闘参加者)。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class AtbRunner : MonoBehaviour
{
    [Header("UI参照")]
    public Image iconBg;
    public Image iconRing;
    public Text label;
    public GameObject readyGlow;

    private BattleActor actor;
    private Color baseColor;
    private Color readyColor;
    private float yOffset;
    private RectTransform rt;

    public BattleActor Actor => actor;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        // 親トラックの左端基準で X を動かしたいので pivot を 中心 (0.5, 0.5)
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
    }

    public void Bind(BattleActor a, Color baseColor, Color readyColor, float yOffset)
    {
        this.actor = a;
        this.baseColor = baseColor;
        this.readyColor = readyColor;
        this.yOffset = yOffset;

        if (iconBg != null) iconBg.color = baseColor;
        if (iconRing != null) iconRing.color = baseColor;

        // ラベル: 名前の頭2文字 (敵: 識別しやすく) / プレイヤー: 名前
        if (label != null)
            label.text = ShortName(a != null ? a.actorName : "?");

        if (readyGlow != null) readyGlow.SetActive(false);
    }

    private string ShortName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "?";
        // 全角文字なら最初の1〜2文字、英字なら最初の3文字
        int take = 2;
        if (s.Length <= take) return s;
        return s.Substring(0, take);
    }

    public void Refresh(float trackWidth)
    {
        if (actor == null) return;

        // X位置 = atbValue × trackWidth (左端0、右端=満タン)
        float x = Mathf.Clamp01(actor.atbValue) * trackWidth;
        rt.anchoredPosition = new Vector2(x, yOffset);

        // 死亡時は薄く
        if (!actor.IsAlive)
        {
            SetAlpha(0.25f);
            if (readyGlow != null) readyGlow.SetActive(false);
            return;
        }
        else
        {
            SetAlpha(1f);
        }

        // 満タン: グロー表示 + リングをreadyColorに
        bool ready = actor.IsAtbReady;
        if (readyGlow != null) readyGlow.SetActive(ready);
        if (iconRing != null) iconRing.color = ready ? readyColor : baseColor;
    }

    private void SetAlpha(float a)
    {
        if (iconBg != null) { Color c = iconBg.color; c.a = a; iconBg.color = c; }
        if (iconRing != null) { Color c = iconRing.color; c.a = a; iconRing.color = c; }
        if (label != null) { Color c = label.color; c.a = a; label.color = c; }
    }

    public void SetHighlight(bool on)
    {
        // 行動中キャラはreadyGlowを強制ON
        if (readyGlow != null && on) readyGlow.SetActive(true);
    }
}
