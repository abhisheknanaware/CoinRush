using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Coin : MonoBehaviour
{
    public int value = 10;
    public float spinSpeed = 3f;
    public float bobHeight = 0.15f;
    public float bobSpeed = 2.5f;
    public ParticleSystem collectEffectPrefab;

    Vector3 startPos;
    Vector3 startScale;
    float phase;
    bool collected;

    void Start()
    {
        startPos = transform.position;
        startScale = transform.localScale;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        float t = Time.time + phase;
        transform.localScale = new Vector3(startScale.x * Mathf.Cos(t * spinSpeed), startScale.y, startScale.z);
        transform.position = startPos + Vector3.up * Mathf.Sin(t * bobSpeed) * bobHeight;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.CompareTag("Player")) return;
        collected = true;

        if (collectEffectPrefab)
        {
            ParticleSystem fx = Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, 1.5f);
        }
        if (AudioManager.Instance) AudioManager.Instance.PlayCoin();
        if (GameManager.Instance) GameManager.Instance.AddCoin(value);
        Destroy(gameObject);
    }
}
