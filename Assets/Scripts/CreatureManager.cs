using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the creature population. Spawns the first generation, receives
/// offspring requests from Creature, and enforces a population cap to keep
/// frame-rate stable.
/// </summary>
public class CreatureManager : MonoBehaviour
{
    public static CreatureManager Instance { get; private set; }

    [Header("Population")]
    public int   startingPopulation = 20;
    public int   populationCap      = 80;

    [Header("References")]
    public Sprite creatureSprite; // Assign a Circle sprite in the Inspector

    // Runtime
    private Vector2         mapHalfSize;
    private List<Creature>  creatures = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Called by Main after the map and food are ready.</summary>
    public void Initialise(Vector2 mapSize)
    {
        mapHalfSize = mapSize / 2f;

        for (int i = 0; i < startingPopulation; i++)
        {
            Vector2 pos = new(
                Random.Range(-mapHalfSize.x, mapHalfSize.x),
                Random.Range(-mapHalfSize.y, mapHalfSize.y));

            SpawnCreature(Genome.Random(), pos, 0);
        }
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    void SpawnCreature(Genome genome, Vector2 position, int generation)
    {
        GameObject go = new("Creature");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = creatureSprite != null ? creatureSprite : FoodSpawner.CreateCircleSprite_Public();

        Creature c = go.AddComponent<Creature>();
        c.Initialise(genome, mapHalfSize, generation);
        creatures.Add(c);
    }

    /// <summary>Called by a Creature when it reproduces.</summary>
    public void SpawnOffspring(Genome genome, Vector2 position, int generation)
    {
        if (creatures.Count >= populationCap) return;
        SpawnCreature(genome, position, generation);
    }

    /// <summary>Called by a Creature just before it destroys itself.</summary>
    public void OnCreatureDied(Creature c)
    {
        creatures.Remove(c);
    }

    // ── Public info ───────────────────────────────────────────────────────────

    public int Population => creatures.Count;
}