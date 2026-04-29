using UnityEngine;

public enum CollectibleType { Health, Ammo }

public class Collectible : MonoBehaviour
{
    [SerializeField] public CollectibleType type;
    [SerializeField] public int amount = 10;
    [SerializeField] private float rotationSpeed = 100f;

    void Update()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        if (type == CollectibleType.Health)
        {
            HealthSystem health = other.GetComponent<HealthSystem>();
            if (health != null) health.Heal(amount);
        }
        else if (type == CollectibleType.Ammo)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null) player.AddAmmo(amount);
        }

        AudioManager.Instance?.PlayPickupSound();
        Destroy(gameObject);
    }
}
