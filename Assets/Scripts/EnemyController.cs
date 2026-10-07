using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
    [Tooltip("Patrol distance to the left and right of the start position.")]
    public float patrolDistance = 3f;
    public float speed = 2.5f;
    public float squashAmount = 0.12f;
    public float squashSpeed = 10f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Vector2 pointA;
    Vector2 pointB;
    Vector2 target;
    Vector3 baseScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        sr = GetComponentInChildren<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    void Start()
    {
        Vector2 start = transform.position;
        pointA = start + Vector2.left * patrolDistance;
        pointB = start + Vector2.right * patrolDistance;
        target = pointB;
    }

    void FixedUpdate()
    {
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if (Vector2.Distance(next, target) < 0.02f)
            target = target == pointA ? pointB : pointA;

        if (sr) sr.flipX = target.x < rb.position.x;
    }

    void Update()
    {
        float s = Mathf.Sin(Time.time * squashSpeed) * squashAmount;
        transform.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z);
    }

    void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    void OnTriggerStay2D(Collider2D other) => TryHit(other);

    void TryHit(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        PlayerController player = other.GetComponent<PlayerController>();
        if (player) player.TakeHit(transform.position);
    }

    void OnDrawGizmos()
    {
        Vector3 p = Application.isPlaying ? (Vector3)((pointA + pointB) * 0.5f) : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(p + Vector3.left * patrolDistance, p + Vector3.right * patrolDistance);
    }
}
