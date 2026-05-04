using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class FloatingDamageText : MonoBehaviour
{
    public float duration = 0.7f;
    public float moveDistance = 55f;
    public float startScale = 1.25f;
    public float endScale = 0.95f;

    private Text text;
    private RectTransform rectTransform;

    private void Awake()
    {
        text = GetComponent<Text>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void Play(string value, Vector2 anchoredPosition)
    {
        if (text == null)
            text = GetComponent<Text>();

        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (text == null || rectTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        text.text = value;
        rectTransform.anchoredPosition = anchoredPosition;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        Color startColor = text.color;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            rectTransform.anchoredPosition = startPos + Vector2.up * moveDistance * t;
            rectTransform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            text.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}
