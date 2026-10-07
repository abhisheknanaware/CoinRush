using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Goal : MonoBehaviour
{
    public SpriteRenderer glow;
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.15f;

    Vector3 glowScale;
    Color glowColor;
    bool reached;

    void Start()
    {
        if (!glow) return;
        glowScale = glow.transform.localScale;
        glowColor = glow.color;
    }

    void Update()
    {
        if (!glow) return;
        float p = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        glow.transform.localScale = glowScale * (1f + p * pulseAmount);
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * (0.5f + 0.5f * p));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (reached || !other.CompareTag("Player")) return;
        reached = true;
        if (GameManager.Instance) GameManager.Instance.Win(transform.position);
    }
}
