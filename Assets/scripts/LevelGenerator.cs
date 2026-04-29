using UnityEngine;
using UnityEngine.UI;

public class LevelGenerator : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite playerSprite;
    [SerializeField] private Sprite enemySprite;
    [SerializeField] private Sprite bulletSprite;
    [SerializeField] private Sprite healthPickupSprite;
    [SerializeField] private Sprite ammoPickupSprite;
    [SerializeField] private Sprite treeSprite;
    [SerializeField] private Sprite houseSprite;

    private GameObject bulletPrefab;

    void Start()
    {
        if (playerSprite == null || enemySprite == null || bulletSprite == null)
        {
            Debug.LogError("[LevelGenerator] 请把所有 Sprite 素材拖到脚本的字段上！");
            return;
        }

        CreateGameManager();
        CreateBulletPrefab();
        GameObject player = CreatePlayer();
        CreateEnemies();
        CreateCollectibles();
        CreateObstacles();
        CreateUI(player);
    }

    void CreateBulletPrefab()
    {
        bulletPrefab = new GameObject("BulletPrefab");
        bulletPrefab.SetActive(false);

        var sr = bulletPrefab.AddComponent<SpriteRenderer>();
        sr.sprite = bulletSprite;
        sr.sortingOrder = 5;

        var col = bulletPrefab.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.15f;

        var rb = bulletPrefab.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;

        bulletPrefab.AddComponent<Projectile>();
    }

    GameObject CreatePlayer()
    {
        GameObject player = new GameObject("Player");
        player.transform.position = Vector3.zero;

        var sr = player.AddComponent<SpriteRenderer>();
        sr.sprite = playerSprite;
        sr.sortingOrder = 10;

        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        player.AddComponent<BoxCollider2D>();
        player.AddComponent<HealthSystem>();

        var controller = player.AddComponent<PlayerController>();
        controller.moveSpeed = 5f;
        controller.maxAmmo = 30;
        controller.fireRate = 0.15f;

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(player.transform);
        firePoint.transform.localPosition = new Vector3(0, 0.5f, 0);
        controller.firePoint = firePoint.transform;

        GameObject poolObj = new GameObject("BulletPool");
        var pool = poolObj.AddComponent<ObjectPool>();
        pool.prefab = bulletPrefab;
        pool.poolSize = 30;
        controller.bulletPool = pool;

        Camera.main.orthographicSize = 8;
        var camFollow = Camera.main.gameObject.AddComponent<CameraFollow>();
        camFollow.target = player.transform;
        camFollow.smoothSpeed = 5f;

        return player;
    }

    void CreateEnemies()
    {
        Vector3[] enemyPositions = new Vector3[]
        {
            new Vector3(-6, 4, 0),
            new Vector3(6, 4, 0),
            new Vector3(-6, -4, 0),
            new Vector3(6, -4, 0),
            new Vector3(0, 6, 0),
            new Vector3(0, -6, 0)
        };

        GameManager gm = FindObjectOfType<GameManager>();

        for (int i = 0; i < enemyPositions.Length; i++)
        {
            GameObject enemy = new GameObject($"Enemy_{i}");
            enemy.transform.position = enemyPositions[i];

            var sr = enemy.AddComponent<SpriteRenderer>();
            sr.sprite = enemySprite;
            sr.sortingOrder = 9;

            var rb = enemy.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            enemy.AddComponent<BoxCollider2D>();
            enemy.AddComponent<HealthSystem>();

            var ai = enemy.AddComponent<EnemyAI>();
            ai.patrolSpeed = 2f;
            ai.chaseSpeed = 3f;
            ai.detectionRadius = 5f;
            ai.attackDamage = 10;

            // 创建巡逻点
            Transform[] waypoints = new Transform[3];
            for (int w = 0; w < 3; w++)
            {
                GameObject wp = new GameObject($"Enemy{i}_WP{w}");
                float angle = w * 120f * Mathf.Deg2Rad;
                wp.transform.position = enemyPositions[i] + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 2f;
                waypoints[w] = wp.transform;
            }
            ai.waypoints = waypoints;

            if (gm != null)
            {
                HealthSystem health = enemy.GetComponent<HealthSystem>();
                gm.RegisterEnemy(health);
            }
        }
    }

    void CreateCollectibles()
    {
        Vector3[] healthPositions = new Vector3[] { new Vector3(-5, -5, 0), new Vector3(5, 5, 0) };
        Vector3[] ammoPositions = new Vector3[] { new Vector3(5, -5, 0), new Vector3(-5, 5, 0) };

        foreach (var pos in healthPositions)
        {
            GameObject pickup = new GameObject("HealthPickup");
            pickup.transform.position = pos;

            var sr = pickup.AddComponent<SpriteRenderer>();
            sr.sprite = healthPickupSprite;
            sr.sortingOrder = 8;

            var col = pickup.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            var c = pickup.AddComponent<Collectible>();
            c.type = CollectibleType.Health;
            c.amount = 25;
        }

        foreach (var pos in ammoPositions)
        {
            GameObject pickup = new GameObject("AmmoPickup");
            pickup.transform.position = pos;

            var sr = pickup.AddComponent<SpriteRenderer>();
            sr.sprite = ammoPickupSprite;
            sr.sortingOrder = 8;

            var col = pickup.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            var c = pickup.AddComponent<Collectible>();
            c.type = CollectibleType.Ammo;
            c.amount = 10;
        }
    }

    void CreateObstacles()
    {
        Vector3[] treePositions = new Vector3[]
        {
            new Vector3(-3, 2, 0), new Vector3(3, -2, 0),
            new Vector3(-2, -3, 0), new Vector3(2, 3, 0),
            new Vector3(-7, 0, 0), new Vector3(7, 0, 0),
            new Vector3(0, 7, 0), new Vector3(0, -7, 0)
        };

        Vector3[] housePositions = new Vector3[]
        {
            new Vector3(-4, 5, 0), new Vector3(4, -5, 0)
        };

        foreach (var pos in treePositions)
        {
            GameObject tree = new GameObject("Tree");
            tree.transform.position = pos;

            var sr = tree.AddComponent<SpriteRenderer>();
            sr.sprite = treeSprite;
            sr.sortingOrder = 7;

            tree.AddComponent<BoxCollider2D>();
            tree.AddComponent<Obstacle>();
        }

        foreach (var pos in housePositions)
        {
            GameObject house = new GameObject("House");
            house.transform.position = pos;

            var sr = house.AddComponent<SpriteRenderer>();
            sr.sprite = houseSprite;
            sr.sortingOrder = 7;

            house.AddComponent<BoxCollider2D>();
            house.AddComponent<Obstacle>();
        }
    }

    void CreateGameManager()
    {
        GameObject gmObj = new GameObject("GameManager");
        gmObj.AddComponent<GameManager>();
    }

    void CreateUI(GameObject player)
    {
        GameObject canvas = new GameObject("Canvas");
        var canvasComp = canvas.AddComponent<Canvas>();
        canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComp.sortingOrder = 100;
        canvas.AddComponent<CanvasScaler>();
        canvas.AddComponent<GraphicRaycaster>();

        // 血条
        GameObject healthBar = new GameObject("HealthBar");
        healthBar.transform.SetParent(canvas.transform);
        RectTransform hbRect = healthBar.AddComponent<RectTransform>();
        hbRect.anchorMin = new Vector2(0, 1);
        hbRect.anchorMax = new Vector2(0, 1);
        hbRect.pivot = new Vector2(0, 1);
        hbRect.anchoredPosition = new Vector2(20, -20);
        hbRect.sizeDelta = new Vector2(200, 30);

        GameObject background = new GameObject("Background");
        background.transform.SetParent(healthBar.transform);
        RectTransform bgRect = background.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = background.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        GameObject fillArea = new GameObject("Fill");
        fillArea.transform.SetParent(healthBar.transform);
        RectTransform fillRect = fillArea.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillArea.AddComponent<Image>();
        fillImg.color = Color.red;

        Slider slider = healthBar.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.maxValue = 1;
        slider.value = 1;

        // 弹药文本
        GameObject ammoTextObj = new GameObject("AmmoText");
        ammoTextObj.transform.SetParent(canvas.transform);
        RectTransform ammoRect = ammoTextObj.AddComponent<RectTransform>();
        ammoRect.anchorMin = new Vector2(0, 1);
        ammoRect.anchorMax = new Vector2(0, 1);
        ammoRect.pivot = new Vector2(0, 1);
        ammoRect.anchoredPosition = new Vector2(20, -60);
        ammoRect.sizeDelta = new Vector2(200, 30);
        Text ammoText = ammoTextObj.AddComponent<Text>();
        ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ammoText.fontSize = 24;
        ammoText.color = Color.white;
        ammoText.text = "Ammo: 30 / 30";

        // 敌人数文本
        GameObject enemyTextObj = new GameObject("EnemyText");
        enemyTextObj.transform.SetParent(canvas.transform);
        RectTransform enemyRect = enemyTextObj.AddComponent<RectTransform>();
        enemyRect.anchorMin = new Vector2(1, 1);
        enemyRect.anchorMax = new Vector2(1, 1);
        enemyRect.pivot = new Vector2(1, 1);
        enemyRect.anchoredPosition = new Vector2(-20, -20);
        enemyRect.sizeDelta = new Vector2(200, 30);
        Text enemyText = enemyTextObj.AddComponent<Text>();
        enemyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        enemyText.fontSize = 24;
        enemyText.color = Color.white;
        enemyText.alignment = TextAnchor.UpperRight;
        enemyText.text = "Enemies: 0 / 0";

        // 胜利面板
        GameObject winPanel = CreatePanel(canvas.transform, "WinPanel", Color.green, "YOU WIN!");
        winPanel.SetActive(false);

        // 失败面板
        GameObject losePanel = CreatePanel(canvas.transform, "LosePanel", Color.red, "GAME OVER");
        losePanel.SetActive(false);

        // UIManager
        UIManager uiManager = canvas.AddComponent<UIManager>();
        uiManager.healthBar = slider;
        uiManager.ammoText = ammoText;
        uiManager.enemyText = enemyText;
        uiManager.winPanel = winPanel;
        uiManager.losePanel = losePanel;
        uiManager.player = player.GetComponent<PlayerController>();
        uiManager.playerHealth = player.GetComponent<HealthSystem>();
        uiManager.gameManager = FindObjectOfType<GameManager>();
    }

    GameObject CreatePanel(Transform parent, string name, Color panelColor, string message)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.7f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(panel.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(400, 100);

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 48;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = message;

        return panel;
    }
}
