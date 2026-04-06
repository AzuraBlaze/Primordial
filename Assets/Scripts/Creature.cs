using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single creature driven by its Genome. Handles movement (wander AI +
/// food-seek), hunger, reproduction, and death. Rendering uses a
/// SpriteRenderer sized and coloured from the genome.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Creature : MonoBehaviour
{
    // ── Public state (read by UI / CreatureManager) ───────────────────────────
    public Genome genome        { get; private set; }
    public float  hunger        { get; private set; } // 0 = starving, 1 = full
    public float  age           { get; private set; } // seconds alive
    public int    generation    { get; private set; }
    public bool   IsDead        { get; private set; }

    // ── Tuning constants ──────────────────────────────────────────────────────
    const float BaseSpeed          = 3f;   // world-units per second at speed=1
    const float HungerDecayRate    = 0.04f;// hunger lost per second
    const float EatRange           = 0.6f; // distance to consume food
    const float ReproduceHunger    = 0.75f;// min hunger to reproduce
    const float ReproduceCooldown  = 12f;  // seconds between births
    const float MinReproduceAge    = 6f;   // must be this old to reproduce
    const float WanderRadius       = 5f;   // max distance for a new wander target
    const float ArrivalThreshold   = 0.3f; // distance considered "reached"
    const float FoodSeekRange      = 6f;   // radius within which food is noticed

    // ── Private state ─────────────────────────────────────────────────────────
    private SpriteRenderer  sr;
    private Vector2         mapHalfSize;
    private Vector2         wanderTarget;
    private float           reproduceCooldownTimer;
    private Food            targetFood;

    // ── Initialisation ────────────────────────────────────────────────────────

    public void Initialise(Genome g, Vector2 mapHalf, int gen = 0)
    {
        genome       = g;
        mapHalfSize  = mapHalf;
        generation   = gen;
        hunger       = Random.Range(0.5f, 1f);
        age          = 0f;
        IsDead       = false;
        reproduceCooldownTimer = Random.Range(0f, ReproduceCooldown); // stagger first births

        sr           = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 1;

        ApplyGenomeVisuals();
        PickWanderTarget();
    }

    void ApplyGenomeVisuals()
    {
        // Body colour comes from hue/saturation genes
        sr.color = genome.ToColor();

        // Body size: genome.size [0,1] maps to world radius [0.2, 0.7]
        float radius = Mathf.Lerp(0.2f, 0.7f, genome.size);
        transform.localScale = Vector3.one * radius * 2f;

        // Give each creature a unique sorting offset so they don't z-fight
        sr.sortingOrder = 1 + Random.Range(0, 100);
    }

    // ── Unity update loop ─────────────────────────────────────────────────────

    void Update()
    {
        if (IsDead) return;

        age   += Time.deltaTime;
        hunger = Mathf.Max(0f, hunger - HungerDecayRate * Time.deltaTime);

        if (hunger <= 0f) { Die(); return; }

        reproduceCooldownTimer += Time.deltaTime;

        SeekOrWander();
        TryEat();
        TryReproduce();
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    void SeekOrWander()
    {
        // If hungry enough, look for food first
        if (hunger < 0.6f)
        {
            Food nearby = FoodSpawner.Instance != null
                ? FindNearestSuitableFood()
                : null;

            if (nearby != null)
            {
                targetFood = nearby;
                wanderTarget = (Vector2)nearby.transform.position;
            }
        }
        else
        {
            targetFood = null;
        }

        // Move toward current wander/seek target
        Vector2 pos   = transform.position;
        Vector2 delta = wanderTarget - pos;

        if (delta.magnitude < ArrivalThreshold)
            PickWanderTarget();
        else
        {
            float speed = Mathf.Lerp(0.5f, 1f, genome.speed) * BaseSpeed;
            Vector2 newPos = pos + delta.normalized * speed * Time.deltaTime;
            newPos.x = Mathf.Clamp(newPos.x, -mapHalfSize.x, mapHalfSize.x);
            newPos.y = Mathf.Clamp(newPos.y, -mapHalfSize.y, mapHalfSize.y);
            transform.position = new Vector3(newPos.x, newPos.y, 0f);
        }
    }

    void PickWanderTarget()
    {
        targetFood = null;
        Vector2 pos = transform.position;
        Vector2 candidate;
        int tries = 0;
        do {
            candidate = pos + Random.insideUnitCircle * WanderRadius;
            tries++;
        } while ((Mathf.Abs(candidate.x) > mapHalfSize.x ||
                  Mathf.Abs(candidate.y) > mapHalfSize.y) && tries < 10);

        wanderTarget = new Vector2(
            Mathf.Clamp(candidate.x, -mapHalfSize.x, mapHalfSize.x),
            Mathf.Clamp(candidate.y, -mapHalfSize.y, mapHalfSize.y));
    }

    // ── Eating ────────────────────────────────────────────────────────────────

    void TryEat()
    {
        if (targetFood == null || !targetFood.gameObject.activeSelf) { targetFood = null; return; }

        float dist = Vector2.Distance(transform.position, targetFood.transform.position);
        if (dist > EatRange) return;

        // Diet gene: herbivores (diet≈0) prefer plant, carnivores (diet≈1) prefer meat
        bool isPlant = targetFood.foodType == Food.FoodType.Plant;
        float dietFit = isPlant ? (1f - genome.diet) : genome.diet; // 0–1, higher = more efficient
        float gain    = targetFood.nutritionValue * Mathf.Lerp(0.4f, 1.2f, dietFit);

        hunger = Mathf.Min(1f, hunger + gain);
        targetFood.ConsumedBy(this);
        targetFood = null;
        PickWanderTarget();
    }

    Food FindNearestSuitableFood()
    {
        // Ask the spawner for active food objects within range
        // We walk all active food children of the spawner's transform.
        Transform spawnerT = FoodSpawner.Instance.transform;
        Food best     = null;
        float bestScore = float.MinValue;

        Vector2 myPos = transform.position;

        for (int i = 0; i < spawnerT.childCount; i++)
        {
            GameObject child = spawnerT.GetChild(i).gameObject;
            if (!child.activeSelf) continue;

            Food f = child.GetComponent<Food>();
            if (f == null) continue;

            float dist = Vector2.Distance(myPos, (Vector2)f.transform.position);
            if (dist > FoodSeekRange) continue;

            // Score: prefer close food that matches diet
            bool isPlant  = f.foodType == Food.FoodType.Plant;
            float dietFit = isPlant ? (1f - genome.diet) : genome.diet;
            float score   = dietFit / (dist + 0.1f);

            if (score > bestScore) { bestScore = score; best = f; }
        }

        return best;
    }

    // ── Reproduction ──────────────────────────────────────────────────────────

    void TryReproduce()
    {
        if (hunger < ReproduceHunger) return;
        if (age    < MinReproduceAge) return;
        if (reproduceCooldownTimer < ReproduceCooldown / Mathf.Lerp(0.5f, 2f, genome.fertility)) return;

        reproduceCooldownTimer = 0f;
        hunger -= 0.3f; // reproduction costs energy

        Vector2 offset = Random.insideUnitCircle * 0.8f;
        Vector2 childPos = (Vector2)transform.position + offset;
        childPos.x = Mathf.Clamp(childPos.x, -mapHalfSize.x, mapHalfSize.x);
        childPos.y = Mathf.Clamp(childPos.y, -mapHalfSize.y, mapHalfSize.y);

        CreatureManager.Instance?.SpawnOffspring(genome.Mutate(), childPos, generation + 1);
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // Drop meat proportional to body size
        FoodSpawner.Instance?.SpawnMeatPellet(transform.position, genome.size * 0.8f);
        CreatureManager.Instance?.OnCreatureDied(this);
        Destroy(gameObject);
    }

    // ── Public helpers (used by Inspector UI) ─────────────────────────────────

    /// <summary>Human-readable summary of this creature's genome.</summary>
    public string GetGenomeSummary()
    {
        string dietLabel = genome.diet < 0.33f ? "Herbivore"
                         : genome.diet < 0.67f ? "Omnivore"
                         :                        "Carnivore";
        return
            $"Gen {generation}\n" +
            $"Diet:      {dietLabel}\n" +
            $"Speed:     {genome.speed:P0}\n" +
            $"Size:      {genome.size:P0}\n" +
            $"Fertility: {genome.fertility:P0}\n" +
            $"Hunger:    {hunger:P0}\n" +
            $"Age:       {age:F1}s";
    }
}