using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    public TMP_Text scoreText;
    public TMP_Text healthText;
    public TMP_Text timeText;
    public Image damageOverlay;

    [Header("Win Panel")]
    public GameObject winPanel;
    public TMP_Text winScoreText;
    public TMP_Text winCoinsText;
    public TMP_Text winTimeText;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverTitle;
    public TMP_Text gameOverScoreText;

    [Header("Main Menu")]
    public GameObject howToPanel;
    public RectTransform menuTitle;
    public string gameSceneName = "Game";

    int lastSecond = -1;

    void Awake()
    {
        if (winPanel) winPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (howToPanel) howToPanel.SetActive(false);
    }

    void Update()
    {
        if (menuTitle)
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * 2f) * 0.04f;
            menuTitle.localScale = new Vector3(s, s, 1f);
        }
    }

    public void SetScore(int score)
    {
        if (scoreText) scoreText.text = $"Score: {score}";
    }

    public void SetHealth(int health)
    {
        if (healthText) healthText.text = $"Health: {health}";
    }

    public void SetTime(float timeLeft)
    {
        int seconds = Mathf.CeilToInt(timeLeft);
        if (seconds == lastSecond || !timeText) return;
        lastSecond = seconds;
        timeText.text = $"Time: {seconds}";
        timeText.color = seconds <= 10 ? new Color(1f, 0.35f, 0.3f) : Color.white;
    }

    public void FlashDamage()
    {
        if (damageOverlay) StartCoroutine(DamageFlash());
    }

    IEnumerator DamageFlash()
    {
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            damageOverlay.color = new Color(1f, 0f, 0f, 0.35f * (1f - t / 0.4f));
            yield return null;
        }
        damageOverlay.color = Color.clear;
    }

    public void ShowWin(int score, int coins, int timeRemaining)
    {
        winScoreText.text = $"Final Score: {score}";
        winCoinsText.text = $"Coins Collected: {coins}";
        winTimeText.text = $"Time Remaining: {timeRemaining}s";
        StartCoroutine(PopIn(winPanel));
    }

    public void ShowGameOver(string title, int score, int coins)
    {
        gameOverTitle.text = title;
        gameOverScoreText.text = $"Score: {score}\nCoins Collected: {coins}";
        StartCoroutine(PopIn(gameOverPanel));
    }

    IEnumerator PopIn(GameObject panel)
    {
        panel.SetActive(true);
        Transform box = panel.transform.childCount > 0 ? panel.transform.GetChild(0) : panel.transform;
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.3f;
            float s = 1f + 0.15f * Mathf.Sin(k * Mathf.PI) - 0.15f * (1f - k);
            box.localScale = Vector3.one * s;
            yield return null;
        }
        box.localScale = Vector3.one;
    }

    public void PlayGame()
    {
        Click();
        SceneManager.LoadScene(gameSceneName);
    }

    public void ShowHowTo()
    {
        Click();
        if (howToPanel) StartCoroutine(PopIn(howToPanel));
    }

    public void HideHowTo()
    {
        Click();
        if (howToPanel) howToPanel.SetActive(false);
    }

    public void Restart()
    {
        Click();
        if (GameManager.Instance) GameManager.Instance.Restart();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void MainMenu()
    {
        Click();
        if (GameManager.Instance) GameManager.Instance.LoadMenu();
        else SceneManager.LoadScene(0);
    }

    static void Click()
    {
        if (AudioManager.Instance) AudioManager.Instance.PlayClick();
    }
}
