using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] public Slider healthBar;
    [SerializeField] public Text ammoText;
    [SerializeField] public Text enemyText;
    [SerializeField] public GameObject winPanel;
    [SerializeField] public GameObject losePanel;
    [SerializeField] public PlayerController player;
    [SerializeField] public HealthSystem playerHealth;
    [SerializeField] public GameManager gameManager;

    void Start()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;
            UpdateHealthBar(playerHealth.GetCurrentHealth(), playerHealth.GetMaxHealth());
        }

        if (gameManager != null)
        {
            gameManager.OnGameWin += ShowWinPanel;
            gameManager.OnGameLose += ShowLosePanel;
        }

        winPanel?.SetActive(false);
        losePanel?.SetActive(false);
    }

    void Update()
    {
        if (player != null && ammoText != null)
        {
            ammoText.text = $"Ammo: {player.CurrentAmmo} / {player.MaxAmmo}";
        }

        if (gameManager != null && enemyText != null)
        {
            enemyText.text = $"Enemies: {gameManager.GetRemainingEnemies()} / {gameManager.GetTotalEnemies()}";
        }
    }

    void UpdateHealthBar(int current, int max)
    {
        if (healthBar != null)
            healthBar.value = (float)current / max;
    }

    void ShowWinPanel()
    {
        winPanel?.SetActive(true);
    }

    void ShowLosePanel()
    {
        losePanel?.SetActive(true);
    }
}
