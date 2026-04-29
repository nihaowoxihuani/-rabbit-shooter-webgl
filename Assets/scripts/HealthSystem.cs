using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;
    private bool isInvulnerable = false;
    private float invulnerableTimer = 0f;
    private SpriteRenderer spriteRenderer;

    public event Action OnDeath;
    public event Action<int, int> OnHealthChanged;

    void Awake()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (isInvulnerable)
        {
            invulnerableTimer -= Time.deltaTime;
            if (invulnerableTimer <= 0)
            {
                isInvulnerable = false;
                if (spriteRenderer != null)
                    spriteRenderer.color = Color.white;
            }
            else
            {
                if (spriteRenderer != null)
                    spriteRenderer.color = Color.Lerp(Color.white, Color.red, Mathf.PingPong(Time.time * 10, 1));
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isInvulnerable || currentHealth <= 0) return;

        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            OnDeath?.Invoke();
        }
        else
        {
            isInvulnerable = true;
            invulnerableTimer = 0.5f;
            PlayHitSound();
        }
    }

    void PlayHitSound()
    {
        if (GetComponent<PlayerController>() != null)
        {
            AudioManager.Instance?.PlayPlayerHitSound();
        }
        else
        {
            AudioManager.Instance?.PlayEnemyHitSound();
        }
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
}
