using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RabbitShooterGame : MonoBehaviour
{
    [Header("Gameplay Settings")]
    [SerializeField] private int enemyCount = 6;
    [SerializeField] private int treeCount = 8;
    [SerializeField] private int houseCount = 2;

    private Sprite rabbitSprite;
    private Sprite wolfSprite;
    private Sprite carrotBulletSprite;
    private Sprite heartSprite;
    private Sprite ammoSprite;
    private Sprite treeSprite;
    private Sprite houseSprite;
    private Sprite backgroundSprite;
    private GameObject bulletPrefab;

    private readonly Vector2 worldBounds = new Vector2(16f, 9f);

    void Start()
    {
        InitGame();
    }

    public void RestartGame()
    {
        StartCoroutine(RestartCoroutine());
    }

    System.Collections.IEnumerator RestartCoroutine()
    {
        CleanupGame();
        yield return null;
        InitGame();
    }

    void CleanupGame()
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var obj in roots)
        {
            if (obj == gameObject) continue;
            if (obj.name == "Main Camera") continue;
            if (obj.GetComponent<MCPUnityBridge.MCPBridge>() != null) continue;
            Destroy(obj);
        }

        GameManager.ResetInstance();
        AudioManager.ResetInstance();

        bulletPrefab = null;
    }

    void InitGame()
    {
        LoadSprites();
        CreateBackground();
        CreateGameManager();
        CreateAudioManager();
        CreateBulletPrefab();
        GameObject player = CreatePlayer();
        CreateEnemies();
        CreateCollectibles();
        CreateObstacles();
        GameObject canvas = CreateUI(player);
        CreateMobileControls(player, canvas);

        AudioManager.Instance?.PlayStartSound();
        AudioManager.Instance?.PlayBGM();
    }

    void LoadSprites()
    {
        rabbitSprite = Resources.Load<Sprite>("Sprites/Rabbit");
        wolfSprite = Resources.Load<Sprite>("Sprites/Wolf");
        carrotBulletSprite = Resources.Load<Sprite>("Sprites/CarrotBullet");
        heartSprite = Resources.Load<Sprite>("Sprites/Heart");
        ammoSprite = Resources.Load<Sprite>("Sprites/Ammo");
        treeSprite = Resources.Load<Sprite>("Sprites/Tree");
        houseSprite = Resources.Load<Sprite>("Sprites/House");
        backgroundSprite = Resources.Load<Sprite>("Sprites/GrassBackground");
    }

    void CreateBackground()
    {
        Camera.main.orthographic = true;
        Camera.main.orthographicSize = worldBounds.y;
        Camera.main.transform.position = new Vector3(0, 0, -10);
        Camera.main.backgroundColor = new Color(0.13f, 0.55f, 0.13f);

        GameObject bg = new GameObject("Background");
        var sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = backgroundSprite;
        sr.sortingOrder = -10;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(worldBounds.x * 4f, worldBounds.y * 4f);
        bg.transform.position = new Vector3(0, 0, 10);
    }

    void CreateGameManager()
    {
        GameObject gmObj = new GameObject("GameManager");
        gmObj.AddComponent<GameManager>();
    }

    void CreateAudioManager()
    {
        if (AudioManager.Instance != null) return;
        GameObject audioObj = new GameObject("AudioManager");
        audioObj.AddComponent<AudioManager>();
    }

    void CreateBulletPrefab()
    {
        bulletPrefab = new GameObject("BulletPrefab");
        bulletPrefab.SetActive(false);
        bulletPrefab.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

        var sr = bulletPrefab.AddComponent<SpriteRenderer>();
        sr.sprite = carrotBulletSprite;
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
        sr.sprite = rabbitSprite;
        sr.sortingOrder = 10;

        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var boxCol = player.AddComponent<BoxCollider2D>();
        boxCol.size = new Vector2(0.8f, 0.8f);

        player.transform.localScale = new Vector3(1f, 1f, 1f);

        player.AddComponent<HealthSystem>();

        var controller = player.AddComponent<PlayerController>();
        controller.moveSpeed = 6f;
        controller.maxAmmo = 30;
        controller.fireRate = 0.15f;
        controller.minBounds = new Vector2(-worldBounds.x, -worldBounds.y);
        controller.maxBounds = new Vector2(worldBounds.x, worldBounds.y);

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(player.transform);
        firePoint.transform.localPosition = new Vector3(0, 0.5f, 0);
        controller.firePoint = firePoint.transform;

        GameObject poolObj = new GameObject("BulletPool");
        var pool = poolObj.AddComponent<ObjectPool>();
        pool.prefab = bulletPrefab;
        pool.poolSize = 30;
        controller.bulletPool = pool;

        return player;
    }

    void CreateEnemies()
    {
        List<Vector3> positions = new List<Vector3>
        {
            new Vector3(-13, 7, 0),
            new Vector3(13, 7, 0),
            new Vector3(-13, -7, 0),
            new Vector3(13, -7, 0),
            new Vector3(0, 7, 0),
            new Vector3(0, -7, 0)
        };

        GameManager gm = FindObjectOfType<GameManager>();

        for (int i = 0; i < Mathf.Min(enemyCount, positions.Count); i++)
        {
            GameObject enemy = new GameObject($"Enemy_{i}");
            enemy.transform.position = positions[i];
            enemy.transform.localScale = new Vector3(1f, 1f, 1f);

            var sr = enemy.AddComponent<SpriteRenderer>();
            sr.sprite = wolfSprite;
            sr.sortingOrder = 9;

            var rb = enemy.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var boxCol = enemy.AddComponent<BoxCollider2D>();
            boxCol.size = new Vector2(0.8f, 0.8f);

            var health = enemy.AddComponent<HealthSystem>();
            health.GetType().GetField("maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(health, 50);

            var ai = enemy.AddComponent<EnemyAI>();
            ai.patrolSpeed = 2f;
            ai.chaseSpeed = 3.5f;
            ai.detectionRadius = 8f;
            ai.attackDamage = 10;

            Transform[] waypoints = new Transform[3];
            for (int w = 0; w < 3; w++)
            {
                GameObject wp = new GameObject($"Enemy{i}_WP{w}");
                float angle = w * 120f * Mathf.Deg2Rad;
                wp.transform.position = positions[i] + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 4f;
                waypoints[w] = wp.transform;
            }
            ai.waypoints = waypoints;

            enemy.AddComponent<DestroyOnDeath>();

            if (gm != null)
            {
                gm.RegisterEnemy(health);
            }
        }
    }

    void CreateCollectibles()
    {
        Vector3[] healthPositions = new Vector3[] { new Vector3(-10, -6, 0), new Vector3(10, 6, 0) };
        Vector3[] ammoPositions = new Vector3[] { new Vector3(10, -6, 0), new Vector3(-10, 6, 0) };

        foreach (var pos in healthPositions)
        {
            GameObject pickup = new GameObject("HealthPickup");
            pickup.transform.position = pos;
            pickup.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            var sr = pickup.AddComponent<SpriteRenderer>();
            sr.sprite = heartSprite;
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
            pickup.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            var sr = pickup.AddComponent<SpriteRenderer>();
            sr.sprite = ammoSprite;
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
            new Vector3(-14, 5, 0), new Vector3(14, -5, 0),
            new Vector3(-8, -8, 0), new Vector3(8, 8, 0),
            new Vector3(-14, -2, 0), new Vector3(14, 2, 0),
            new Vector3(0, 8, 0), new Vector3(0, -8, 0)
        };

        Vector3[] housePositions = new Vector3[]
        {
            new Vector3(-12, 6, 0), new Vector3(12, -6, 0)
        };

        foreach (var pos in treePositions)
        {
            GameObject tree = new GameObject("Tree");
            tree.transform.position = pos;
            tree.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            var sr = tree.AddComponent<SpriteRenderer>();
            sr.sprite = treeSprite;
            sr.sortingOrder = 7;

            var col = tree.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);
            tree.AddComponent<Obstacle>();
        }

        foreach (var pos in housePositions)
        {
            GameObject house = new GameObject("House");
            house.transform.position = pos;
            house.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            var sr = house.AddComponent<SpriteRenderer>();
            sr.sprite = houseSprite;
            sr.sortingOrder = 7;

            var col = house.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);
            house.AddComponent<Obstacle>();
        }
    }

    GameObject CreateUI(GameObject player)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        GameObject canvas = new GameObject("Canvas");
        var canvasComp = canvas.AddComponent<Canvas>();
        canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComp.sortingOrder = 100;
        canvas.AddComponent<CanvasScaler>();
        canvas.AddComponent<GraphicRaycaster>();

        // HealthBar
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

        // Ammo Text
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

        // Enemy Text
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

        // Win Panel
        GameObject winPanel = CreatePanel(canvas.transform, "WinPanel", Color.green, "YOU WIN!");
        winPanel.SetActive(false);
        AddRestartButton(winPanel);

        // Lose Panel
        GameObject losePanel = CreatePanel(canvas.transform, "LosePanel", Color.red, "GAME OVER");
        losePanel.SetActive(false);
        AddRestartButton(losePanel);

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

        // Player death -> GameManager
        var playerHealth = player.GetComponent<HealthSystem>();
        playerHealth.OnDeath += () => FindObjectOfType<GameManager>()?.OnPlayerDeath();

        return canvas;
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

    void AddRestartButton(GameObject panel)
    {
        GameObject buttonObj = new GameObject("RestartButton");
        buttonObj.transform.SetParent(panel.transform);

        RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0, -80);
        btnRect.sizeDelta = new Vector2(200, 60);

        Image btnImg = buttonObj.AddComponent<Image>();
        btnImg.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = btnImg;
        button.onClick.AddListener(() => GameManager.Instance?.RestartGame());

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(buttonObj.transform);
        RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 28;
        btnText.color = Color.black;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.text = "RESTART";
    }

    void CreateMobileControls(GameObject player, GameObject canvas)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        VirtualJoystick moveJoystick = CreateJoystick(canvas.transform, "MoveJoystick", new Vector2(130, 130), new Vector2(0, 0));
        controller.moveJoystick = moveJoystick;

        ShootButton shootBtn = CreateShootButton(canvas.transform, "ShootButton", new Vector2(-100, 130), new Vector2(1, 0));
        controller.shootButton = shootBtn;
    }

    VirtualJoystick CreateJoystick(Transform parent, string name, Vector2 anchoredPosition, Vector2 anchorMinMax)
    {
        GameObject bgObj = new GameObject(name + "_Bg");
        bgObj.transform.SetParent(parent);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = anchorMinMax;
        bgRect.anchorMax = anchorMinMax;
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = anchoredPosition;
        bgRect.sizeDelta = new Vector2(180, 180);

        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = CreateCircleSprite(128, new Color(1f, 1f, 1f, 0.2f));

        GameObject handleObj = new GameObject(name + "_Handle");
        handleObj.transform.SetParent(bgObj.transform);
        RectTransform handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(70, 70);

        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.sprite = CreateCircleSprite(64, new Color(1f, 1f, 1f, 0.6f));

        VirtualJoystick joystick = bgObj.AddComponent<VirtualJoystick>();
        joystick.background = bgRect;
        joystick.handle = handleRect;

        return joystick;
    }

    ShootButton CreateShootButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 anchorMinMax)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = anchorMinMax;
        btnRect.anchorMax = anchorMinMax;
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = anchoredPosition;
        btnRect.sizeDelta = new Vector2(120, 120);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.sprite = CreateCircleSprite(128, new Color(1f, 0.3f, 0.3f, 0.5f));

        ShootButton shootBtn = btnObj.AddComponent<ShootButton>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.color = new Color(1f, 1f, 1f, 0.9f);
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "FIRE";

        return shootBtn;
    }

    Sprite CreateCircleSprite(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = dist <= radius ? color : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}

