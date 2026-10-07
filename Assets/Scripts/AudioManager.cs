using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public const int SampleRate = 44100;
    public static readonly string[] SoundNames = { "coin", "jump", "hit", "click", "win", "lose" };

    [Range(0f, 1f)] public float volume = 0.5f;
    public AudioClip coin, jump, hit, click, win, lose;

    AudioSource source;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;

        coin = coin ? coin : Make("coin");
        jump = jump ? jump : Make("jump");
        hit = hit ? hit : Make("hit");
        click = click ? click : Make("click");
        win = win ? win : Make("win");
        lose = lose ? lose : Make("lose");
    }

    public void PlayCoin() => Play(coin);
    public void PlayJump() => Play(jump);
    public void PlayHit() => Play(hit);
    public void PlayWin() => Play(win);
    public void PlayLose() => Play(lose);
    public void PlayClick() => Play(click);

    void Play(AudioClip clip)
    {
        if (clip) source.PlayOneShot(clip, volume);
    }

    static AudioClip Make(string name)
    {
        float[] data = Generate(name);
        AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static float[] Generate(string name)
    {
        switch (name)
        {
            case "coin": return Tones(0.06f, 0.35f, 988f, 1319f);
            case "jump": return Sweep(0.15f, 300f, 600f, 0.25f, false);
            case "hit": return Sweep(0.25f, 400f, 90f, 0.4f, true);
            case "click": return Tones(0.04f, 0.25f, 660f);
            case "win": return Tones(0.12f, 0.35f, 523f, 659f, 784f, 1047f);
            default: return Tones(0.18f, 0.35f, 392f, 330f, 262f);
        }
    }

    static float[] Tones(float noteLength, float amp, params float[] freqs)
    {
        int perNote = (int)(SampleRate * noteLength);
        float[] data = new float[perNote * freqs.Length];
        for (int n = 0; n < freqs.Length; n++)
        {
            for (int i = 0; i < perNote; i++)
            {
                float t = (float)i / SampleRate;
                float env = 1f - (float)i / perNote;
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freqs[n] * t));
                data[n * perNote + i] = square * env * amp * 0.5f;
            }
        }
        return data;
    }

    static float[] Sweep(float length, float from, float to, float amp, bool noise)
    {
        int count = (int)(SampleRate * length);
        float[] data = new float[count];
        float phase = 0f;
        System.Random rng = new System.Random(7);
        for (int i = 0; i < count; i++)
        {
            float k = (float)i / count;
            phase += 2f * Mathf.PI * Mathf.Lerp(from, to, k) / SampleRate;
            float v = Mathf.Sin(phase);
            if (noise) v = Mathf.Lerp(v, (float)(rng.NextDouble() * 2.0 - 1.0), 0.5f);
            data[i] = v * (1f - k) * amp;
        }
        return data;
    }
}
