using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 攻撃対象選択用の画面カーソル。
/// </summary>
public class BattleTargetCursor : MonoBehaviour
{
    [Header("見た目")]
    public RectTransform cursorRect;
    public Image cursorImage;
    public float bobAmplitude = 6f;
    public float bobSpeed = 4f;

    private RectTransform parentCanvasRect;
    private Camera worldCamera;
    private Canvas parentCanvas;
    private List<BattleActor> targets;
    private int index;
    private bool active;
    private Action<BattleActor> onConfirm;
    private Action onCancel;
    private float bobPhase;

    public void Setup(RectTransform canvasRect, Camera camera)
    {
        parentCanvasRect = canvasRect;
        worldCamera = camera;
        if (canvasRect != null) parentCanvas = canvasRect.GetComponent<Canvas>();
        if (cursorRect != null) cursorRect.gameObject.SetActive(false);
    }

    public void Show(List<BattleActor> selectableTargets, Action<BattleActor> onConfirm, Action onCancel)
    {
        if (selectableTargets == null || selectableTargets.Count == 0) { onCancel?.Invoke(); return; }
        targets = new List<BattleActor>();
        foreach (var t in selectableTargets) if (t != null && t.IsAlive) targets.Add(t);
        if (targets.Count == 0) { onCancel?.Invoke(); return; }

        this.onConfirm = onConfirm;
        this.onCancel = onCancel;
        index = 0;
        active = true;
        if (cursorRect != null) cursorRect.gameObject.SetActive(true);

        // 必要ならカメラを再取得
        if (worldCamera == null) worldCamera = Camera.main;

        UpdateCursorPosition();
    }

    public void Hide()
    {
        active = false;
        targets = null;
        if (cursorRect != null) cursorRect.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!active || targets == null || targets.Count == 0) return;

        for (int i = targets.Count - 1; i >= 0; i--)
            if (targets[i] == null || !targets[i].IsAlive) targets.RemoveAt(i);
        if (targets.Count == 0) { Hide(); onCancel?.Invoke(); return; }
        if (index >= targets.Count) index = 0;

        bool prev = false, next = false, confirm = false, cancel = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) prev = true;
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) next = true;
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame) prev = true;
            if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame) next = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) confirm = true;
            if (Keyboard.current.escapeKey.wasPressedThisFrame) cancel = true;
        }
        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.leftStick.left.wasPressedThisFrame) prev = true;
            if (Gamepad.current.dpad.right.wasPressedThisFrame || Gamepad.current.leftStick.right.wasPressedThisFrame) next = true;
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) confirm = true;
            if (Gamepad.current.buttonEast.wasPressedThisFrame) cancel = true;
        }

        if (prev) { index = (index - 1 + targets.Count) % targets.Count; }
        if (next) { index = (index + 1) % targets.Count; }
        if (confirm) { var c = targets[index]; Hide(); onConfirm?.Invoke(c); return; }
        if (cancel)  { Hide(); onCancel?.Invoke(); return; }

        UpdateCursorPosition();
    }

    private void LateUpdate()
    {
        // 戦闘演出でターゲットが動くこともあるので毎フレ位置追従
        if (active) UpdateCursorPosition();
    }

    private void UpdateCursorPosition()
    {
        if (cursorRect == null || parentCanvasRect == null) return;
        if (targets == null || targets.Count == 0) return;
        BattleActor t = targets[index];
        if (t == null) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        bobPhase += Time.deltaTime * bobSpeed;
        float bob = Mathf.Sin(bobPhase) * bobAmplitude;

        // ターゲットの上端 + マージンを world で計算
        Vector3 worldPos = t.transform.position + Vector3.up * 0.6f;
        SpriteRenderer sr = t.GetSprite();
        if (sr != null) worldPos = new Vector3(t.transform.position.x, sr.bounds.max.y + 0.3f, t.transform.position.z);

        // World → Screen
        Vector2 screenPos = worldCamera.WorldToScreenPoint(worldPos);

        // Screen → Canvas Local
        // Overlay Canvas の場合は cam=null を渡す。そうでなければCanvasの worldCamera を使う。
        Camera uiCam = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCam = parentCanvas.worldCamera;

        Vector2 localPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentCanvasRect, screenPos, uiCam, out localPos))
        {
            cursorRect.anchoredPosition = localPos + new Vector2(0f, bob);
        }
    }
}
