using System.Collections;
using UnityEngine;

/// <summary>
/// 攻撃ヒット時のフラッシュ・斬撃エフェクトをプロシージャルに生成。
/// TurnBattleManager から呼ばれる。
/// </summary>
public class BattleVfx : MonoBehaviour
{
    private static BattleVfx _instance;

    public static BattleVfx Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("BattleVfx");
                _instance = go.AddComponent<BattleVfx>();
            }
            return _instance;
        }
    }

    private static Sprite _slashSprite;
    private static Sprite _circleSprite;

    /// <summary>
    /// 対象のSpriteRendererを白く点滅させる。
    /// </summary>
    public void HitFlash(SpriteRenderer sr, float duration = 0.18f, Color? flashColor = null)
    {
        if (sr == null)
            return;

        StartCoroutine(HitFlashRoutine(sr, duration, flashColor ?? Color.white));
    }

    private IEnumerator HitFlashRoutine(SpriteRenderer sr, float duration, Color flashColor)
    {
        Color original = sr.color;
        float half = duration * 0.5f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            sr.color = Color.Lerp(original, flashColor, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            sr.color = Color.Lerp(flashColor, original, t / half);
            yield return null;
        }

        sr.color = original;
    }

    /// <summary>
    /// 指定位置に斬撃エフェクトを表示する。
    /// </summary>
    public void SpawnSlash(Vector3 worldPosition, float angleDegrees = 35f, int sortingOrder = 100)
    {
        EnsureSlashSprite();

        GameObject go = new GameObject("SlashFx");
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _slashSprite;
        sr.sortingOrder = sortingOrder;
        sr.color = new Color(1f, 1f, 1f, 0.95f);

        StartCoroutine(SlashRoutine(go.transform, sr));
    }

    private IEnumerator SlashRoutine(Transform t, SpriteRenderer sr)
    {
        float life = 0.22f;
        float timer = 0f;
        Vector3 startScale = new Vector3(0.4f, 0.4f, 1f);
        Vector3 endScale = new Vector3(1.6f, 1.6f, 1f);
        t.localScale = startScale;

        while (timer < life && sr != null)
        {
            timer += Time.deltaTime;
            float p = timer / life;
            t.localScale = Vector3.Lerp(startScale, endScale, p);
            sr.color = new Color(1f, 1f, 1f, 1f - p);
            yield return null;
        }

        if (t != null)
            Destroy(t.gameObject);
    }

    /// <summary>
    /// 指定位置に円形ヒット波紋を表示する。
    /// </summary>
    public void SpawnImpactRing(Vector3 worldPosition, Color color, int sortingOrder = 100)
    {
        EnsureCircleSprite();

        GameObject go = new GameObject("ImpactRing");
        go.transform.position = worldPosition;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _circleSprite;
        sr.sortingOrder = sortingOrder;
        sr.color = color;

        StartCoroutine(ImpactRingRoutine(go.transform, sr));
    }

    private IEnumerator ImpactRingRoutine(Transform t, SpriteRenderer sr)
    {
        float life = 0.30f;
        float timer = 0f;
        Vector3 startScale = new Vector3(0.2f, 0.2f, 1f);
        Vector3 endScale = new Vector3(1.4f, 1.4f, 1f);
        t.localScale = startScale;

        Color baseColor = sr.color;

        while (timer < life && sr != null)
        {
            timer += Time.deltaTime;
            float p = timer / life;
            t.localScale = Vector3.Lerp(startScale, endScale, p);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - p));
            yield return null;
        }

        if (t != null)
            Destroy(t.gameObject);
    }

    private static void EnsureSlashSprite()
    {
        if (_slashSprite != null)
            return;

        // 横長の斬線を中央に。フィルタ無し。
        const int W = 256;
        const int H = 64;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float dy = (y - H * 0.5f) / (H * 0.5f);
            for (int x = 0; x < W; x++)
            {
                float dx = (x - W * 0.5f) / (W * 0.5f);
                // 中央が明るい横長グラデ
                float core = Mathf.Exp(-(dy * dy) * 14f);     // 縦方向の太さ
                float fall = Mathf.Exp(-(dx * dx) * 2.0f);    // 横方向のフェード
                float a = Mathf.Clamp01(core * fall);
                pixels[y * W + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        _slashSprite = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 200f);
    }

    private static void EnsureCircleSprite()
    {
        if (_circleSprite != null)
            return;

        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[N * N];
        Vector2 c = new Vector2(N * 0.5f, N * 0.5f);
        float maxR = N * 0.5f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float r = Vector2.Distance(new Vector2(x, y), c) / maxR;
                // 中心は薄く、外周にリングが出る
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.85f) * 8f, 2f));
                pixels[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ring));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 200f);
    }
}
