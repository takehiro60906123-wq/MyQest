using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キャラの足元に表示する小型ゲージ (HP/MP)。
/// World Space Canvas を子に持って追従する。
/// </summary>
public class BattleActorOverhead : MonoBehaviour
{
    [Header("ターゲット")]
    public BattleActor actor;

    [Header("ゲージ参照")]
    public Slider hpSlider;
    public Image hpFill;
    public Slider mpSlider;
    public Image mpFill;
    public Text nameLabel;

    [Header("配置 (キャラ基準のローカルオフセット)")]
    [Tooltip("キャラ位置からの相対オフセット (足元のさらに下に表示)")]
    public Vector3 worldOffset = new Vector3(0f, -0.75f, 0f);

    [Header("色")]
    public Color hpHigh = new Color(0.30f, 0.85f, 0.55f);
    public Color hpMid  = new Color(0.95f, 0.78f, 0.20f);
    public Color hpLow  = new Color(0.92f, 0.30f, 0.30f);
    public Color mpColor = new Color(0.30f, 0.65f, 1.0f);

    private void Awake()
    {
        if (actor == null) actor = GetComponentInParent<BattleActor>();
    }

    private void LateUpdate()
    {
        if (actor == null) return;
        transform.localPosition = worldOffset;
        Refresh();
    }

    public void Refresh()
    {
        if (actor == null) return;

        if (hpSlider != null)
        {
            hpSlider.maxValue = actor.maxHp;
            hpSlider.value = actor.currentHp;
        }
        if (hpFill != null)
        {
            float r = actor.maxHp > 0 ? (float)actor.currentHp / actor.maxHp : 0f;
            hpFill.color = r > 0.55f ? hpHigh : (r > 0.25f ? hpMid : hpLow);
        }

        bool hasMp = actor.maxMp > 0;
        if (mpSlider != null)
        {
            mpSlider.gameObject.SetActive(hasMp);
            if (hasMp)
            {
                mpSlider.maxValue = actor.maxMp;
                mpSlider.value = actor.currentMp;
            }
        }
        if (mpFill != null) mpFill.color = mpColor;

        if (nameLabel != null && string.IsNullOrEmpty(nameLabel.text))
            nameLabel.text = actor.actorName;
    }

    public void SetVisible(bool v)
    {
        gameObject.SetActive(v);
    }
}
