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
    public Sprite foodSprite; // Assign a Circle sprite in the Inspector

    // Runtime
    private MapGenerator         mapGenerator;
    private Vector2              mapSize;
    private List<Food>           activePlantFood = new();
    private Queue<Food>          pool            = new();
    private List<Vector2>        bloomTiles      = new();
    private float                respawnTimer;

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

    // ── Plant food ────────────────────────────────────────────────────────────

    void CacheBloomTiles()
    {
        bloomTiles.Clear();
        Biome[,] biomeMap  = mapGenerator.BiomeMap;
        Vector2Int res     = mapGenerator.Resolution;

        for (int py = 0; py < res.y; py++)
        {
            for (int px = 0; px < res.x; px++)
            {
                if (biomeMap[px, py] == Biome.Bloom)
                {
                    // Convert pixel index → world position (matches MapGenerator's formula)
                    float wx = ((px + 0.5f) / res.x) * mapSize.x - mapSize.x * 0.5f;
                    float wy = ((py + 0.5f) / res.y) * mapSize.y - mapSize.y * 0.5f;
                    bloomTiles.Add(new Vector2(wx, wy));
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
        // Jitter slightly so pellets don't stack exactly on tile centres
        pos += Random.insideUnitCircle * 0.4f;

        Food f = GetFromPool();
        f.Initialise(pos, Food.FoodType.Plant, Random.Range(0.3f, 1f));
        activePlantFood.Add(f);
    }

    // ── Meat food ─────────────────────────────────────────────────────────────

    /// <summary>Drop meat at a position when a creature dies (called by Creature).</summary>
    public void SpawnMeatPellet(Vector2 position, float nutrition)
    {
        Food f = GetFromPool();
        f.Initialise(position, Food.FoodType.Meat, Mathf.Clamp01(nutrition));
    }

    // ── Pool ──────────────────────────────────────────────────────────────────

    public void ReturnToPool(Food food)
    {
        activePlantFood.Remove(food);
        food.gameObject.SetActive(false);
        pool.Enqueue(food);
    }

    Food GetFromPool()
    {
        if (pool.Count > 0)
            return pool.Dequeue();

        // Create a new food object
        GameObject go = new("Food");
        go.transform.SetParent(transform);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (foodSprite != null)
            sr.sprite = foodSprite;
        else
            sr.sprite = CreateCircleSprite(); // fallback procedural circle

        go.AddComponent<Food>();
        go.SetActive(false);
        return go.GetComponent<Food>();
    }

    // ── Procedural circle sprite fallback ─────────────────────────────────────

    /// <summary>Public accessor so CreatureManager can reuse the same fallback.</summary>
    public static Sprite CreateCircleSprite_Public() => CreateCircleSprite();

    static Sprite CreateCircleSprite()
    {
        int    size    = 32;
        float  radius  = size / 2f;
        var    tex     = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color  clear   = new(0, 0, 0, 0);
        Color  white   = Color.white;

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