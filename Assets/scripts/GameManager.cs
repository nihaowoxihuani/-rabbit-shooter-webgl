using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private int totalEnemies = 0;
    private int remainingEnemies = 0;
    private bool gameOver = false;

    public event Action OnGameWin;
    public event Action OnGameLose;

    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterEnemy(HealthSystem enemyHealth)
    {
        enemyHealth.OnDeath += OnEnemyDeath;
        totalEnemies++;
        remainingEnemies++;
    }

    void OnEnemyDeath()
    {
        remainingEnemies--;
        if (remainingEnemies <= 0 && !gameOver)
        {
            gameOver = true;
            OnGameWin?.Invoke();
            AudioManager.Instance?.PlayWinSound();
        }
    }

    public void OnPlayerDeath()
    {
        if (gameOver) return;
        gameOver = true;
        OnGameLose?.Invoke();
        AudioManager.Instance?.PlayLoseSound();
    }

    public int GetRemainingEnemies() => remainingEnemies;
    public int GetTotalEnemies() => totalEnemies;

    public void RestartGame()
    {
        var setup = FindObjectOfType<RabbitShooterGame>();
        if (setup != null)
        {
            setup.RestartGame();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public static void ResetInstance()
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }
    }
}
