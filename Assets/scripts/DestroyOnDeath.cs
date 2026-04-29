using UnityEngine;

public class DestroyOnDeath : MonoBehaviour
{
    void Start()
    {
        var health = GetComponent<HealthSystem>();
        if (health != null)
            health.OnDeath += () => Destroy(gameObject);
    }
}
