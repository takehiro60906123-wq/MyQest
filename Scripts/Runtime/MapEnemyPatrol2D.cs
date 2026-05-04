using UnityEngine;

public class MapEnemyPatrol2D : MonoBehaviour
{
    [Header("簡易パトロール")]
    public bool patrolEnabled = true;
    public Vector2 localOffset = new Vector2(1.5f, 0f);
    public float speed = 1.0f;
    public float waitTime = 0.6f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float waitTimer;
    private bool goingToTarget = true;
    private bool frozen;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        startPosition = transform.position;
        targetPosition = startPosition + (Vector3)localOffset;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (!patrolEnabled || frozen)
            return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        Vector3 destination = goingToTarget ? targetPosition : startPosition;
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

        Vector3 delta = transform.position - before;
        if (spriteRenderer != null && Mathf.Abs(delta.x) > 0.001f)
            spriteRenderer.flipX = delta.x < 0f;

        if (Vector3.Distance(transform.position, destination) < 0.02f)
        {
            goingToTarget = !goingToTarget;
            waitTimer = waitTime;
        }
    }

    public void SetFrozen(bool value)
    {
        frozen = value;
    }
}
