using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 60f;
    public float deceleration = 70f;
    public float jumpForce = 13f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;
    public float fallMultiplier = 2.2f;
    public float lowJumpMultiplier = 2.5f;

    [Header("Ground Check")]
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    public float groundCheckOffset = 0.5f;

    [Header("Damage")]
    public float knockbackForce = 9f;
    public float invulnerableTime = 1.2f;
    public float fallDeathY = -12f;
    public ParticleSystem hitEffectPrefab;
    public ParticleSystem jumpEffectPrefab;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Color baseColor;
    Vector3 safePosition;
    readonly Collider2D[] groundHits = new Collider2D[4];
    ContactFilter2D groundFilter;

    float coyoteCounter;
    float jumpBufferCounter;
    float knockbackLock;
    bool invulnerable;
    bool controlEnabled = true;
    bool grounded;

    public bool IsGrounded => grounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        baseColor = sr.color;
        safePosition = transform.position;
        groundFilter = new ContactFilter2D { useTriggers = false };
    }

    void Update()
    {
        if (!controlEnabled) return;

        grounded = CheckGrounded();
        if (grounded && knockbackLock <= 0f) safePosition = transform.position;
        coyoteCounter = grounded ? coyoteTime : coyoteCounter - Time.deltaTime;
        jumpBufferCounter = JumpPressed() ? jumpBufferTime : jumpBufferCounter - Time.deltaTime;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            if (jumpEffectPrefab) SpawnEffect(jumpEffectPrefab, transform.position + Vector3.down * 0.5f);
            if (AudioManager.Instance) AudioManager.Instance.PlayJump();
        }

        float x = HorizontalInput();
        if (Mathf.Abs(x) > 0.01f) sr.flipX = x < 0f;

        if (transform.position.y < fallDeathY)
        {
            transform.position = safePosition + Vector3.up * 0.5f;
            rb.linearVelocity = Vector2.zero;
            if (GameManager.Instance) GameManager.Instance.DamagePlayer();
            StartCoroutine(Flash());
        }
    }

    void FixedUpdate()
    {
        if (!controlEnabled) return;

        if (knockbackLock > 0f)
        {
            knockbackLock -= Time.fixedDeltaTime;
        }
        else
        {
            float target = HorizontalInput() * moveSpeed;
            float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
            float vx = Mathf.MoveTowards(rb.linearVelocity.x, target, rate * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        }

        if (rb.linearVelocity.y < 0f)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * rb.gravityScale * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0f && !JumpHeld())
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * rb.gravityScale * Time.fixedDeltaTime;
    }

    bool CheckGrounded()
    {
        if (rb.linearVelocity.y > 0.1f) return false;
        Vector2 center = (Vector2)transform.position + Vector2.down * groundCheckOffset;
        int count = Physics2D.OverlapBox(center, groundCheckSize, 0f, groundFilter, groundHits);
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].attachedRigidbody != rb) return true;
        }
        return false;
    }

    public void TakeHit(Vector2 sourcePosition)
    {
        if (invulnerable || !controlEnabled) return;

        Vector2 dir = ((Vector2)transform.position - sourcePosition).normalized;
        dir = new Vector2(Mathf.Sign(dir.x == 0f ? 1f : dir.x), 0.7f).normalized;
        rb.linearVelocity = dir * knockbackForce;
        knockbackLock = 0.25f;

        if (hitEffectPrefab) SpawnEffect(hitEffectPrefab, transform.position);
        if (GameManager.Instance) GameManager.Instance.DamagePlayer();
        StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        invulnerable = true;
        float t = 0f;
        while (t < invulnerableTime)
        {
            bool on = Mathf.FloorToInt(t * 12f) % 2 == 0;
            sr.color = on ? new Color(1f, 0.3f, 0.3f, 0.6f) : baseColor;
            t += Time.deltaTime;
            yield return null;
        }
        sr.color = baseColor;
        invulnerable = false;
    }

    public void DisableControl()
    {
        controlEnabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    static void SpawnEffect(ParticleSystem prefab, Vector3 position)
    {
        ParticleSystem fx = Instantiate(prefab, position, Quaternion.identity);
        fx.Play();
        Destroy(fx.gameObject, 2f);
    }

    static float HorizontalInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null) return 0f;
        float x = 0f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
        return x;
#else
        float x = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        return x;
#endif
    }

    static bool JumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    static bool JumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
        return Input.GetKey(KeyCode.Space);
#endif
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.down * groundCheckOffset, groundCheckSize);
    }
}
