using UnityEngine;
using UnityEngine.UI;

public class AtbRow : MonoBehaviour
{
    public Text nameText;
    public Slider atbSlider;
    public Image atbFill;
    public GameObject highlight;

    [Header("色")]
    public Color charging = new Color(0.65f, 0.65f, 0.85f);
    public Color ready    = new Color(1f, 0.95f, 0.4f);

    public BattleActor Actor { get; private set; }

    public void Bind(BattleActor a)
    {
        Actor = a;
        if (nameText != null) nameText.text = a != null ? a.actorName : "";
        if (atbSlider != null) { atbSlider.minValue = 0f; atbSlider.maxValue = 1f; atbSlider.interactable = false; }
        if (highlight != null) highlight.SetActive(false);
        Refresh();
    }

    public void Refresh()
    {
        if (Actor == null) return;
        if (atbSlider != null) atbSlider.value = Actor.atbValue;
        if (atbFill != null) atbFill.color = Actor.IsAtbReady ? ready : charging;
        if (nameText != null && Actor != null && !Actor.IsAlive)
            nameText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
    }

    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }
}
