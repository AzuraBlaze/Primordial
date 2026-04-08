using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the creature population. Spawns the first generation, receives
/// offspring requests from Creature, and enforces a population cap.
/// </summary>
public class CreatureManager : MonoBehaviour
{
    public static CreatureManager Instance { get; private set; }

    [Header("Population")]
    public int startingPopulation = 20;
    public int populationCap      = 80;

    [Header("References")]
    public Sprite creatureSprite;

    private Vector2        mapHalfSize;
    private List<Creature> creatures = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

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

    public void SpawnOffspring(Genome genome, Vector2 position, int generation)
    {
        if (creatures.Count >= populationCap) return;
        SpawnCreature(genome, position, generation);
    }

    public void OnCreatureDied(Creature c)
    {
        creatures.Remove(c);
    }

    /// <summary>Read-only view of all living creatures (used by Creature AI).</summary>
    public IReadOnlyList<Creature> GetAllCreatures() => creatures;

    public int Population => creatures.Count;
}