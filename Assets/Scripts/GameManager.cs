using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Won, Lost }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int startingHealth = 3;
    public float levelTime = 60f;
    public PlayerController player;
    public UIManager ui;
    public ParticleSystem victoryEffectPrefab;
    public string menuSceneName = "MainMenu";

    public int Score { get; private set; }
    public int Coins { get; private set; }
    public int Health { get; private set; }
    public float TimeLeft { get; private set; }
    public GameState State { get; private set; }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        Health = startingHealth;
        TimeLeft = levelTime;
        State = GameState.Playing;
    }

    void Start()
    {
        RefreshHud();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (State != GameState.Playing) return;

        TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
        if (ui) ui.SetTime(TimeLeft);
        if (TimeLeft <= 0f) Lose("TIME UP");
    }

    public void AddCoin(int value)
    {
        if (State != GameState.Playing) return;
        Coins++;
        Score += value;
        RefreshHud();
    }

    public void DamagePlayer()
    {
        if (State != GameState.Playing) return;
        Health = Mathf.Max(0, Health - 1);
        RefreshHud();
        if (ui) ui.FlashDamage();
        if (AudioManager.Instance) AudioManager.Instance.PlayHit();
        if (Health <= 0) Lose("GAME OVER");
    }

    public void Win(Vector3 goalPosition)
    {
        if (State != GameState.Playing) return;
        State = GameState.Won;
        if (player) player.DisableControl();

        if (victoryEffectPrefab)
        {
            ParticleSystem fx = Instantiate(victoryEffectPrefab, goalPosition + Vector3.up * 1.5f, Quaternion.identity);
            fx.Play();
        }
        if (AudioManager.Instance) AudioManager.Instance.PlayWin();
        if (ui) ui.ShowWin(Score, Coins, Mathf.CeilToInt(TimeLeft));
    }

    void Lose(string reason)
    {
        if (State != GameState.Playing) return;
        State = GameState.Lost;
        if (player) player.DisableControl();
        if (AudioManager.Instance) AudioManager.Instance.PlayLose();
        if (ui) ui.ShowGameOver(reason, Score, Coins);
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    void RefreshHud()
    {
        if (!ui) return;
        ui.SetScore(Score);
        ui.SetHealth(Health);
        ui.SetTime(TimeLeft);
    }
}
