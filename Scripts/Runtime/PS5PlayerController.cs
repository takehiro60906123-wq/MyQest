using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PS5PlayerController : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 3.5f;

    [Header("参照 (空なら自動取得)")]
    [Tooltip("通常はAddComponentで同じGameObjectに付ける。空なら自動取得。")]
    public CharacterAnimatorDriver animatorDriver;

    public bool CanMove { get; private set; } = true;
    public Vector2 LastMoveDirection => lastMoveDirection;
    public Vector2 MoveInput => moveInput;
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f;

    private Rigidbody2D rb;

    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.down;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if (animatorDriver == null)
            animatorDriver = GetComponent<CharacterAnimatorDriver>();
        if (animatorDriver == null)
            animatorDriver = GetComponentInChildren<CharacterAnimatorDriver>();

        // ない場合は自動付与 (デフォルト設定で動く)
        if (animatorDriver == null)
            animatorDriver = gameObject.AddComponent<CharacterAnimatorDriver>();
    }

    private void Update()
    {
        ReadInput();

        if (animatorDriver != null)
        {
            // 動いていない時は最後の向きで Idle 表示
            Vector2 animVelocity = IsMoving ? moveInput : Vector2.zero;
            if (!IsMoving)
                animatorDriver.FaceDirection(lastMoveDirection); // 向きを保ったままIdle
            else
                animatorDriver.UpdateMovement(animVelocity);
        }
    }

    private void FixedUpdate()
    {
        rb.velocity = moveInput * moveSpeed;
    }

    public void SetControl(bool canMove)
    {
        CanMove = canMove;

        if (!CanMove)
        {
            moveInput = Vector2.zero;
            rb.velocity = Vector2.zero;
            if (animatorDriver != null)
                animatorDriver.UpdateMovement(Vector2.zero);
        }
    }

    public void FaceTarget(Vector3 targetPosition)
    {
        Vector2 dir = targetPosition - transform.position;

        if (dir.sqrMagnitude > 0.01f)
        {
            lastMoveDirection = dir.normalized;
            if (animatorDriver != null)
                animatorDriver.FaceDirection(lastMoveDirection);
        }
    }

    private void ReadInput()
    {
        if (!CanMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = Vector2.zero;

        if (Gamepad.current != null)
        {
            moveInput = Gamepad.current.leftStick.ReadValue();
        }

        if (Keyboard.current != null)
        {
            Vector2 keyboardInput = Vector2.zero;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                keyboardInput.y += 1f;

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                keyboardInput.y -= 1f;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                keyboardInput.x -= 1f;

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                keyboardInput.x += 1f;

            if (keyboardInput != Vector2.zero)
                moveInput = keyboardInput.normalized;
        }

        if (moveInput.magnitude > 1f)
            moveInput.Normalize();

        if (moveInput.sqrMagnitude > 0.01f)
            lastMoveDirection = moveInput.normalized;
    }
}
