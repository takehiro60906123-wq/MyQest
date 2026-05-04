using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1キャラ分のHPカード。TurnBattleManager から Bind して使う。
/// </summary>
public class BattleHpCard : MonoBehaviour
{
    [Header("UI参照")]
    public Text nameText;
    public Text hpText;
    public Slider slider;
    public Image fillImage;
    public Image accentBar;
    public Image background;
    public GameObject activeHighlight;

    [Header("HPバー色")]
    public Color hpHigh = new Color(0.30f, 0.85f, 0.55f, 1f);
    public Color hpMid  = new Color(0.95f, 0.78f, 0.20f, 1f);
    public Color hpLow  = new Color(0.92f, 0.30f, 0.30f, 1f);
    public Color deadColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("テーマ色")]
    public Color playerAccent = new Color(0.50f, 0.90f, 1.00f, 1f);
    public Color enemyAccent  = new Color(1.00f, 0.55f, 0.55f, 1f);

    private BattleActor boundActor;

    public void SetTheme(bool isPlayer)
    {
        if (accentBar != null) accentBar.color = isPlayer ? playerAccent : enemyAccent;
    }

    public void Bind(BattleActor actor)
    {
        boundActor = actor;
        if (nameText != null) nameText.text = actor != null ? actor.actorName : "";
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;
        }
        SetActiveHighlight(false);
        Refresh();
    }

    public void Refresh()
    {
        if (boundActor == null) return;

        if (hpText != null)
            hpText.text = $"{boundActor.currentHp} / {boundActor.maxHp}";

        if (slider != null)
        {
            slider.maxValue = boundActor.maxHp;
            slider.value = boundActor.currentHp;
        }

        if (fillImage != null)
        {
            float ratio = boundActor.maxHp > 0 ? (float)boundActor.currentHp / boundActor.maxHp : 0f;
            fillImage.color = boundActor.IsAlive ? HpColor(ratio) : deadColor;
        }

        if (nameText != null && !boundActor.IsAlive)
            nameText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
    }

    public void SetActiveHighlight(bool on)
    {
        if (activeHighlight != null) activeHighlight.SetActive(on);
    }

    private Color HpColor(float ratio)
    {
        if (ratio > 0.55f) return hpHigh;
        if (ratio > 0.25f) return hpMid;
        return hpLow;
    }
}
