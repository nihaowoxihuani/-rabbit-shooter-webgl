using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [SerializeField] public float patrolSpeed = 2f;
    [SerializeField] public float chaseSpeed = 3f;
    [SerializeField] public float detectionRadius = 5f;
    [SerializeField] public float attackCooldown = 1f;
    [SerializeField] public int attackDamage = 10;
    [SerializeField] public Transform[] waypoints;

    private Transform player;
    private int currentWaypoint = 0;
    private float attackTimer;
    private bool isChasing = false;

    void Start()
    {
        player = FindObjectOfType<PlayerController>()?.transform;
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRadius)
        {
            isChasing = true;
        }
        else if (isChasing && distanceToPlayer > detectionRadius * 1.5f)
        {
            isChasing = false;
        }

        if (isChasing)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }

        attackTimer -= Time.deltaTime;
    }

    void Patrol()
    {
        if (waypoints.Length == 0) return;

        Transform target = waypoints[currentWaypoint];
        transform.position = Vector2.MoveTowards(transform.position, target.position, patrolSpeed * Time.deltaTime);

        Vector2 direction = (target.position - transform.position).normalized;
        if (direction != Vector2.zero)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.flipX = direction.x < 0;
        }

        if (Vector2.Distance(transform.position, target.position) < 0.1f)
        {
            currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        }
    }

    void ChasePlayer()
    {
        transform.position = Vector2.MoveTowards(transform.position, player.position, chaseSpeed * Time.deltaTime);

        Vector2 direction = (player.position - transform.position).normalized;
        if (direction != Vector2.zero)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.flipX = direction.x < 0;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.GetComponent<PlayerController>() != null && attackTimer <= 0)
        {
            HealthSystem health = collision.collider.GetComponent<HealthSystem>();
            if (health != null)
            {
                health.TakeDamage(attackDamage);
                attackTimer = attackCooldown;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
