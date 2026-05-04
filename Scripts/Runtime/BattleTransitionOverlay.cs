using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class BattleTransitionOverlay : MonoBehaviour
{
    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;
        SetAlpha(0f);
    }

    public IEnumerator Flash(float maxAlpha = 0.45f, float fadeInTime = 0.08f, float fadeOutTime = 0.22f)
    {
        float timer = 0f;
        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;
            SetAlpha(Mathf.Lerp(0f, maxAlpha, timer / fadeInTime));
            yield return null;
        }

        timer = 0f;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            SetAlpha(Mathf.Lerp(maxAlpha, 0f, timer / fadeOutTime));
            yield return null;
        }

        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        if (image == null)
            image = GetComponent<Image>();

        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
