using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] public float moveSpeed = 5f;
    [SerializeField] public Transform firePoint;
    [SerializeField] public ObjectPool bulletPool;
    [SerializeField] public int maxAmmo = 30;
    [SerializeField] public float fireRate = 0.15f;
    [SerializeField] public Vector2 minBounds = new Vector2(-15f, -15f);
    [SerializeField] public Vector2 maxBounds = new Vector2(15f, 15f);
    [SerializeField] public VirtualJoystick moveJoystick;
    [SerializeField] public ShootButton shootButton;

    private int currentAmmo;
    private float fireCooldown;
    private Camera mainCamera;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;

    void Start()
    {
        mainCamera = Camera.main;
        currentAmmo = maxAmmo;
    }

    void Update()
    {
        HandleMovement();
        HandleAiming();
        HandleShooting();
    }

    void HandleMovement()
    {
        Vector2 movement;

        if (moveJoystick != null && moveJoystick.InputDirection.magnitude > 0.01f)
        {
            movement = moveJoystick.InputDirection;
        }
        else
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            movement = new Vector2(horizontal, vertical).normalized;
        }

        transform.position += (Vector3)(movement * moveSpeed * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
        pos.y = Mathf.Clamp(pos.y, minBounds.y, maxBounds.y);
        transform.position = pos;
    }

    void HandleAiming()
    {
        Vector2 direction = GetAimDirection();

        if (direction.magnitude < 0.01f) return;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.flipX = direction.x < 0;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        firePoint.rotation = Quaternion.Euler(0, 0, angle);
    }

    Vector2 GetAimDirection()
    {
        // PC 端：优先使用鼠标瞄准
        if (shootButton == null)
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            return (mousePos - transform.position).normalized;
        }

        // 手机端：自动瞄准最近的敌人
        EnemyAI nearest = null;
        float nearestDist = 25f;

        foreach (var enemy in FindObjectsOfType<EnemyAI>())
        {
            if (enemy == null) continue;
            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        if (nearest != null)
        {
            return (nearest.transform.position - transform.position).normalized;
        }

        // 没有敌人时保持当前朝向
        return firePoint.up;
    }

    void HandleShooting()
    {
        fireCooldown -= Time.deltaTime;

        bool shouldShoot = (shootButton != null && shootButton.IsPressed) || Input.GetMouseButton(0);

        if (shouldShoot && fireCooldown <= 0 && currentAmmo > 0)
        {
            Shoot();
            fireCooldown = fireRate;
        }
    }

    void Shoot()
    {
        GameObject bullet = bulletPool.GetFromPool();
        bullet.transform.position = firePoint.position;
        bullet.transform.rotation = firePoint.rotation;

        Projectile projectile = bullet.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Initialize(firePoint.up);
        }

        currentAmmo--;
        AudioManager.Instance?.PlayShootSound();
    }

    public void AddAmmo(int amount)
    {
        currentAmmo = Mathf.Min(currentAmmo + amount, maxAmmo);
    }
}
