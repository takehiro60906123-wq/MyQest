using System.Collections;
using UnityEngine;

public class BattleCameraController : MonoBehaviour
{
    [Header("カメラ設定")]
    public Camera targetCamera;
    [Tooltip("バトル中の orthographicSize。小さいほど寄る。元のカメラサイズと比較して min が選ばれる。")]
    public float battleZoomSize = 3.0f;
    public float focusTime = 0.35f;
    public float resetTime = 0.25f;

    [Header("UI回避")]
    [Tooltip("画面下のバトルUIが占める割合(0〜0.5)。この分カメラを下にずらしてキャラを上半分に集める。")]
    [Range(0f, 0.5f)]
    public float uiBottomFraction = 0.15f;

    private Vector3 originalPosition;
    private float originalSize;
    private bool hasOriginal;
    private Vector3 focusBasePosition;
    private bool hasFocusBase;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        CaptureOriginal();
    }

    public void CaptureOriginal()
    {
        if (targetCamera == null)
            return;

        originalPosition = targetCamera.transform.position;
        originalSize = targetCamera.orthographicSize;
        hasOriginal = true;
    }

    public IEnumerator FocusBattle(Transform player, Transform enemy)
    {
        yield return FocusBattle(player, enemy, uiBottomFraction);
    }

    public IEnumerator FocusBattle(Transform player, Transform enemy, float overrideUiFraction)
    {
        if (targetCamera == null || player == null || enemy == null)
            yield break;

        if (!hasOriginal)
            CaptureOriginal();

        Vector3 startPos = targetCamera.transform.position;
        float startSize = targetCamera.orthographicSize;

        Vector3 center = (player.position + enemy.position) * 0.5f;
        float targetSize = Mathf.Min(startSize, battleZoomSize);

        // UI回避: カメラを下にずらすと、キャラは画面上半分に映る
        float yOffset = -targetSize * overrideUiFraction;
        Vector3 targetPos = new Vector3(center.x, center.y + yOffset, startPos.z);

        focusBasePosition = targetPos;
        hasFocusBase = true;

        float timer = 0f;
        while (timer < focusTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / focusTime);
            targetCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            targetCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }

        targetCamera.transform.position = targetPos;
        targetCamera.orthographicSize = targetSize;
    }

    public IEnumerator ResetCamera()
    {
        if (targetCamera == null || !hasOriginal)
            yield break;

        Vector3 startPos = targetCamera.transform.position;
        float startSize = targetCamera.orthographicSize;

        float timer = 0f;
        while (timer < resetTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / resetTime);
            targetCamera.transform.position = Vector3.Lerp(startPos, originalPosition, t);
            targetCamera.orthographicSize = Mathf.Lerp(startSize, originalSize, t);
            yield return null;
        }

        targetCamera.transform.position = originalPosition;
        targetCamera.orthographicSize = originalSize;
        hasFocusBase = false;
    }

    public void Shake(float duration = 0.16f, float strength = 0.08f)
    {
        if (targetCamera == null)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        Vector3 basePosition = hasFocusBase ? focusBasePosition : targetCamera.transform.position;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            Vector2 offset = Random.insideUnitCircle * strength;
            targetCamera.transform.position = basePosition + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        targetCamera.transform.position = basePosition;
        shakeRoutine = null;
    }
}
