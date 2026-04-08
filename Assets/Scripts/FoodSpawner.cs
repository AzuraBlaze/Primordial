using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns plant food on Bloom tiles at startup, periodically respawns it,
/// and drops meat food when a creature dies. Maintains a simple object pool
/// to avoid per-frame allocations.
/// </summary>
public class FoodSpawner : MonoBehaviour
{
    public static FoodSpawner Instance { get; private set; }

    [Header("Plant Food")]
    [Tooltip("How many plant pellets exist in the world at any one time.")]
    public int   plantFoodCap      = 120;
    [Tooltip("Seconds between respawn attempts.")]
    public float respawnInterval   = 3f;
    [Tooltip("How many pellets to try to add each respawn tick.")]
    public int   respawnBatchSize  = 8;

    [Header("References")]
    [Tooltip("Leave blank to use procedural sprite.")]
    public Sprite plantSprite; // Optional override
    [Tooltip("Leave blank to use procedural sprite.")]
    public Sprite meatSprite;  // Optional override

    /* ======================================== Runtime ======================================== */
    private MapGenerator         mapGenerator;
    private Vector2              mapSize;
    private List<Food>           activePlantFood = new();
    private Queue<Food>          poolPlant       = new();
    private Queue<Food>          poolMeat        = new();
    private List<Vector2>        bloomTiles      = new();
    private float                respawnTimer;

    /* ======================================== Cached Procedural Sprites (created once) ======================================== */
    private static Sprite s_PlantSprite;
    private static Sprite s_MeatSprite;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Called by Main after the map has been generated.</summary>
    public void Initialise(MapGenerator generator, Vector2 size)
    {
        mapGenerator = generator;
        mapSize      = size;

        CacheBloomTiles();
        ScatterInitialFood();
    }

    void Update()
    {
        respawnTimer += Time.deltaTime;
        if (respawnTimer >= respawnInterval)
        {
            respawnTimer = 0f;
            RespawnPlantFood();
        }
    }

    /* ======================================== Plant Food ======================================== */
    void CacheBloomTiles()
    {
        bloomTiles.Clear();
        Biome[,]   biomeMap = mapGenerator.BiomeMap;
        Vector2Int res      = mapGenerator.Resolution;

        for (int py = 0; py < res.y; py++)
        {
            for (int px = 0; px < res.x; px++)
            {
                if (biomeMap[px, py] == Biome.Bloom)
                {
                    float wx = ((px + 0.5f) / res.x) * mapSize.x - mapSize.x * 0.5f;
                    float wy = ((py + 0.5f) / res.y) * mapSize.y - mapSize.y * 0.5f;
                    bloomTiles.Add(new (wx, wy));
                }
            }
        }
    }

    void ScatterInitialFood()
    {
        if (bloomTiles.Count == 0) return;

        int count = Mathf.Min(plantFoodCap, bloomTiles.Count);
        for (int i = 0; i < count; i++)
            SpawnPlantPellet();
    }

    void RespawnPlantFood()
    {
        if (bloomTiles.Count == 0) return;

        int deficit = plantFoodCap - activePlantFood.Count;
        int toSpawn = Mathf.Min(respawnBatchSize, deficit);
        for (int i = 0; i < toSpawn; i++)
            SpawnPlantPellet();
    }

    void SpawnPlantPellet()
    {
        if (bloomTiles.Count == 0) return;

        Vector2 pos = bloomTiles[Random.Range(0, bloomTiles.Count)];
        pos += Random.insideUnitCircle * 0.4f;

        Food f = GetFromPool(Food.FoodType.Plant);
        f.Initialise(pos, Food.FoodType.Plant, Random.Range(0.3f, 1f));
        activePlantFood.Add(f);
    }

    /* ======================================== Meat Food ======================================== */

    /// <summary>Drop meat at a position when a creature dies (called by Creature).</summary>
    public void SpawnMeatPellet(Vector2 position, float nutrition)
    {
        Food f = GetFromPool(Food.FoodType.Meat);
        f.Initialise(position, Food.FoodType.Meat, Mathf.Clamp01(nutrition));
    }

    /* ======================================== Object Pool ======================================== */

    public void ReturnToPool(Food food)
    {
        activePlantFood.Remove(food);
        food.gameObject.SetActive(false);

        if (food.foodType == Food.FoodType.Plant)
            poolPlant.Enqueue(food);
        else
            poolMeat.Enqueue(food);
    }

    Food GetFromPool(Food.FoodType type)
    {
        Queue<Food> pool = type == Food.FoodType.Plant ? poolPlant : poolMeat;

        if (pool.Count > 0)
            return pool.Dequeue();

        // Create a new food object with the appropriate sprite
        GameObject go = new ("Food");
        go.transform.SetParent(transform);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = type == Food.FoodType.Plant
            ? GetPlantSprite()
            : GetMeatSprite();

        go.AddComponent<Food>();
        go.SetActive(false);
        return go.GetComponent<Food>();
    }

    /* ======================================== Sprite Access ======================================== */

    Sprite GetPlantSprite()
    {
        if (plantSprite != null) return plantSprite;
        if (s_PlantSprite == null) s_PlantSprite = CreatePlantSprite();
        return s_PlantSprite;
    }

    Sprite GetMeatSprite()
    {
        if (meatSprite != null) return meatSprite;
        if (s_MeatSprite == null) s_MeatSprite = CreateMeatSprite();
        return s_MeatSprite;
    }

    /* ======================================== Sprite Generators ======================================== */

    /// <summary>
    /// Plant sprite: a four-pointed leaf/diamond shape with a small stem.
    /// Drawn in white so Food.cs can tint it with sr.color.
    /// </summary>
    static Sprite CreatePlantSprite()
    {
        const int size = 32;
        var tex = new (size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color clear = new (0, 0, 0, 0);
        Color white = Color.white;

        float cx = size / 2f - 0.5f;
        float cy = size / 2f;      // Center, slightly raised to leave room for stem

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx;
            float dy = y - cy;

            // Four-petal diamond: |dx|/a + |dy|/b <= 1
            float a = size * 0.38f;   // horizontal half-extent
            float b = size * 0.46f;   // vertical   half-extent
            bool inLeaf = (Mathf.Abs(dx) / a + Mathf.Abs(dy) / b) <= 1f;

            // Thin vertical stem below the center
            bool inStem = Mathf.Abs(dx) <= 1.2f && dy < 0f && dy > -(size * 0.38f);

            tex.SetPixel(x, y, (inLeaf || inStem) ? white : clear);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// Meat sprite: a rough teardrop / bone-chunk shape.
    /// Also drawn in white for tinting.
    /// </summary>
    static Sprite CreateMeatSprite()
    {
        const int size = 32;
        Texture2D tex = new (size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color clear = new (0, 0, 0, 0);
        Color white = Color.white;

        float cx = size / 2f - 0.5f;
        float cy = size / 2f - 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx;
            float dy = y - cy;

            // Main body: slightly squashed circle
            float bodyR = size * 0.34f;
            bool inBody = (dx * dx) / (bodyR * bodyR) + (dy * dy) / ((bodyR * 0.82f) * (bodyR * 0.82f)) <= 1f;

            // Top nub: smaller circle offset upward (teardrop / drumstick silhouette)
            float nubR  = size * 0.16f;
            float nubCy = cy + size * 0.26f;
            float ndx   = x - cx;
            float ndy   = y - nubCy;
            bool inNub  = ndx * ndx + ndy * ndy <= nubR * nubR;

            // Connecting neck between body and nub — a thin vertical band
            bool inNeck = Mathf.Abs(dx) <= size * 0.10f
                       && y > cy + size * 0.12f
                       && y < nubCy + nubR;

            tex.SetPixel(x, y, (inBody || inNub || inNeck) ? white : clear);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static public Sprite CreateCircleSprite()
    {
        int         size   = 32;
        float       radius = size / 2f;
        Texture2D   tex    = new (size, size, TextureFormat.RGBA32, false);
        Color       clear  = new (0, 0, 0, 0);
        Color       white  = Color.white;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - radius + 0.5f;
            float dy = y - radius + 0.5f;
            tex.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? white : clear);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}