using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private int damage = 25;
    [SerializeField] private float lifetime = 3f;
    private Vector2 direction;
    private float timer;

    public void Initialize(Vector2 dir)
    {
        direction = dir.normalized;
        timer = lifetime;
    }

    void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) return;

        HealthSystem health = other.GetComponent<HealthSystem>();
        if (health != null)
        {
            health.TakeDamage(damage);
            gameObject.SetActive(false);
            return;
        }

        if (other.GetComponent<Obstacle>() != null)
        {
            gameObject.SetActive(false);
            return;
        }
    }
}
