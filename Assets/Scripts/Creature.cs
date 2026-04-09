using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Creature : MonoBehaviour
{
    /* ======================================== Public ======================================== */
    public Genome genome     { get; private set; }
    public float  hunger     { get; private set; }   // 0 = starving, 1 = full
    public float  age        { get; private set; }   // seconds alive
    public int    generation { get; private set; }
    public bool   isDead     { get; private set; }

    /* ======================================== Adjustable ======================================== */
    const float BaseSpeed         = 3f;
    const float HungerDecayRate   = 0.04f;
    const float EatRange          = 0.6f;
    const float ReproduceHunger   = 0.75f;
    const float ReproduceCooldown = 12f;
    const float MinReproduceAge   = 6f;
    const float WanderRadius      = 5f;
    const float ArrivalThreshold  = 0.3f;

    // Temperature stress
    const float TempStressRate    = 0.05f;   // hunger lost per second at full discomfort
    // Aggression
    const float AttackRange       = 0.8f;
    const float AttackDamage      = 0.25f;   // hunger drain dealt to victim
    const float AttackCooldown    = 2f;
    // Sleep
    const float SleepThreshold    = 0.15f;   // activity below this = sleeping
    const float SleepHealRate     = 0.02f;   // hunger recovered while sleeping

    /* ======================================== Private ======================================== */
    private SpriteRenderer sr;
    private Vector2        mapHalfSize;
    private Vector2        wanderTarget;
    private float          reproduceCooldownTimer;
    private float          attackCooldownTimer;
    private Food           targetFood;
    private Creature       targetPrey;
    private Creature       fleeTarget;

    /* ======================================== Init ======================================== */
    public void Initialise(Genome g, Vector2 mapHalf, int gen = 0)
    {
        genome       = g;
        mapHalfSize  = mapHalf;
        generation   = gen;
        hunger       = Random.Range(0.5f, 1f);
        age          = 0f;
        isDead       = false;
        reproduceCooldownTimer = Random.Range(0f, ReproduceCooldown);

        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 1 + Random.Range(0, 100);

        ApplyGenomeVisuals();
        PickWanderTarget();
    }

    // Allow the inspector to push a new genome at runtime
    public void SetGenome(Genome g)
    {
        genome = g;
        ApplyGenomeVisuals();
    }

    void ApplyGenomeVisuals()
    {
        sr.color = genome.ToColor();
        float radius = Mathf.Lerp(0.2f, 0.7f, genome.size);
        transform.localScale = Vector3.one * radius * 2f;
    }

    /* ======================================== Updates ======================================== */
    void Update()
    {
        if (isDead) return;

        age += Time.deltaTime;

        // Lifespan: die of old age
        if (age >= genome.MaxAge) { Die(); return; }

        // Base hunger decay
        hunger = Mathf.Max(0f, hunger - HungerDecayRate * Time.deltaTime);
        if (hunger <= 0f) { Die(); return; }

        // Temperature stress
        ApplyTemperatureStress();

        // Day/Night activity
        float activity = GetActivityLevel();
        bool  sleeping = activity < SleepThreshold;

        if (sleeping)
        {
            // Recover a tiny bit of hunger while sleeping
            hunger = Mathf.Min(1f, hunger + SleepHealRate * Time.deltaTime);
        }
        else
        {
            attackCooldownTimer   += Time.deltaTime;
            reproduceCooldownTimer += Time.deltaTime;

            SeekOrWander(activity);
            TryEat();
            TryAttack();
            TryReproduce();
        }
    }

    /* ======================================== Day/Night ======================================== */
    float GetActivityLevel()
    {
        if (DayNightCycle.Instance == null) return 1f;
        return genome.DaylightActivity(DayNightCycle.Instance.Phase);
    }

    /* ======================================== Temperature ======================================== */
    void ApplyTemperatureStress()
    {
        if (TemperatureMap.Instance == null) return;
        float temp    = TemperatureMap.Instance.SampleTemperature(transform.position);
        float comfort = genome.TemperatureComfort(temp);
        float stress  = 1f - comfort;
        hunger = Mathf.Max(0f, hunger - stress * TempStressRate * Time.deltaTime);
    }

    /* ======================================== Movement ======================================== */
    void SeekOrWander(float activityMultiplier)
    {
        // Determine highest-priority target
        Vector2 moveTarget = wanderTarget;
        bool    hasPriority = false;

        // Fear: flee threats
        fleeTarget = FindThreat();
        if (fleeTarget != null)
        {
            Vector2 away = (Vector2)transform.position - (Vector2)fleeTarget.transform.position;
            moveTarget   = (Vector2)transform.position + away.normalized * WanderRadius;
            moveTarget.x = Mathf.Clamp(moveTarget.x, -mapHalfSize.x, mapHalfSize.x);
            moveTarget.y = Mathf.Clamp(moveTarget.y, -mapHalfSize.y, mapHalfSize.y);
            hasPriority  = true;
        }

        // Aggression: chase prey (only if not fleeing)
        if (!hasPriority && genome.aggression > 0.3f && hunger < 0.7f)
        {
            targetPrey = FindPrey();
            if (targetPrey != null)
            {
                moveTarget  = (Vector2)targetPrey.transform.position;
                hasPriority = true;
            }
        }
        else if (hasPriority || genome.aggression <= 0.3f)
        {
            targetPrey = null;
        }

        // Food seeking (hunger-based)
        if (!hasPriority && hunger < 0.6f)
        {
            Food nearby = FindNearestSuitableFood();
            if (nearby != null)
            {
                targetFood  = nearby;
                moveTarget  = (Vector2)nearby.transform.position;
                hasPriority = true;
            }
        }

        // Flocking (when well-fed and not otherwise occupied)
        if (!hasPriority && genome.flocking > 0.2f && hunger > 0.5f)
        {
            Vector2 flockCenter;
            if (TryGetFlockCenter(out flockCenter))
            {
                moveTarget  = flockCenter;
                hasPriority = true;
            }
        }

        // If nothing special, clear prey/food targets
        if (!hasPriority)
        {
            targetFood = null;
            targetPrey = null;
        }

        // Move toward moveTarget
        Vector2 pos   = transform.position;
        Vector2 delta = moveTarget - pos;

        if (delta.magnitude < ArrivalThreshold && !hasPriority)
        {
            PickWanderTarget();
        }
        else if (delta.magnitude > ArrivalThreshold)
        {
            float speed  = Mathf.Lerp(0.5f, 1f, genome.speed) * BaseSpeed * activityMultiplier;
            Vector2 newPos = pos + delta.normalized * speed * Time.deltaTime;
            newPos.x = Mathf.Clamp(newPos.x, -mapHalfSize.x, mapHalfSize.x);
            newPos.y = Mathf.Clamp(newPos.y, -mapHalfSize.y, mapHalfSize.y);
            transform.position = new (newPos.x, newPos.y, 0f);
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

        wanderTarget = new (
            Mathf.Clamp(candidate.x, -mapHalfSize.x, mapHalfSize.x),
            Mathf.Clamp(candidate.y, -mapHalfSize.y, mapHalfSize.y)
        );
    }

    /* ======================================== Food ======================================== */
    void TryEat()
    {
        if (targetFood == null || !targetFood.gameObject.activeSelf) { targetFood = null; return; }

        float dist = Vector2.Distance(transform.position, targetFood.transform.position);
        if (dist > EatRange) return;

        bool  isPlant  = targetFood.foodType == Food.FoodType.Plant;
        float dietFit  = isPlant ? (1f - genome.diet) : genome.diet;
        float gain     = targetFood.nutritionValue * Mathf.Lerp(0.4f, 1.2f, dietFit);

        hunger = Mathf.Min(1f, hunger + gain);
        targetFood.ConsumedBy(this);
        targetFood = null;
        PickWanderTarget();
    }

    Food FindNearestSuitableFood()
    {
        if (FoodSpawner.Instance == null) return null;
        Transform spawnerT = FoodSpawner.Instance.transform;
        Food      best     = null;
        float     bestScore = float.MinValue;
        Vector2   myPos    = transform.position;
        float     range    = genome.VisionRange;

        for (int i = 0; i < spawnerT.childCount; i++)
        {
            GameObject child = spawnerT.GetChild(i).gameObject;
            if (!child.activeSelf) continue;
            Food f = child.GetComponent<Food>();
            if (f == null) continue;
            float dist = Vector2.Distance(myPos, (Vector2)f.transform.position);
            if (dist > range) continue;
            bool  isPlant  = f.foodType == Food.FoodType.Plant;
            float dietFit  = isPlant ? (1f - genome.diet) : genome.diet;
            float score    = dietFit / (dist + 0.1f);
            if (score > bestScore) { bestScore = score; best = f; }
        }
        return best;
    }

    /* ======================================== Aggression/Fear ======================================== */
    Creature FindPrey()
    {
        // Look for smaller or similar-sized creatures within vision range
        float range  = genome.VisionRange;
        Vector2 myPos = transform.position;

        Creature best  = null;
        float    bestDist = range;

        foreach (Creature c in CreatureManager.Instance?.GetAllCreatures() ?? new List<Creature>())
        {
            if (c == null || c == this || c.isDead) continue;
            float dist = Vector2.Distance(myPos, (Vector2)c.transform.position);
            if (dist > range) continue;
            // Only attack creatures that are smaller (size advantage)
            if (c.genome.size > genome.size * 1.2f) continue;
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        return best;
    }

    Creature FindThreat()
    {
        if (genome.fear < 0.15f) return null; // fearless creatures don't flee
        float range   = genome.VisionRange;
        Vector2 myPos = transform.position;

        Creature worst = null;
        float    closestDist = range;

        foreach (Creature c in CreatureManager.Instance?.GetAllCreatures() ?? new List<Creature>())
        {
            if (c == null || c == this || c.isDead) continue;
            if (c.genome.aggression < 0.3f) continue; // only fear aggressive ones
            if (c.genome.size < genome.size * 0.9f) continue; // not threatened by smaller ones
            float dist = Vector2.Distance(myPos, (Vector2)c.transform.position);
            if (dist < closestDist) { closestDist = dist; worst = c; }
        }
        return worst;
    }

    void TryAttack()
    {
        if (targetPrey == null || targetPrey.isDead) { targetPrey = null; return; }
        if (attackCooldownTimer < AttackCooldown) return;
        if (genome.aggression < 0.3f) return;

        float dist = Vector2.Distance(transform.position, targetPrey.transform.position);
        if (dist > AttackRange) return;

        attackCooldownTimer = 0f;
        targetPrey.TakeDamage(AttackDamage * genome.aggression);
    }

    /// <summary>Receive hunger damage from an attacker.</summary>
    public void TakeDamage(float amount)
    {
        hunger = Mathf.Max(0f, hunger - amount);
        if (hunger <= 0f) Die();
    }

    /* ======================================== Flocking ======================================== */
    bool TryGetFlockCenter(out Vector2 center)
    {
        center = Vector2.zero;
        int count = 0;
        float range = genome.VisionRange * genome.flocking;
        Vector2 myPos = transform.position;

        foreach (Creature c in CreatureManager.Instance?.GetAllCreatures() ?? new List<Creature>())
        {
            if (c == null || c == this || c.isDead) continue;
            // Same rough species: similar hue
            if (Mathf.Abs(c.genome.hue - genome.hue) > 0.15f) continue;
            float dist = Vector2.Distance(myPos, (Vector2)c.transform.position);
            if (dist > range) continue;
            center += (Vector2)c.transform.position;
            count++;
        }

        if (count == 0) return false;
        center /= count;
        return true;
    }

    /* ======================================== Reproduction ======================================== */
    void TryReproduce()
    {
        if (hunger < ReproduceHunger) return;
        if (age    < MinReproduceAge) return;
        float cooldown = ReproduceCooldown / Mathf.Lerp(0.5f, 2f, genome.fertility);
        if (reproduceCooldownTimer < cooldown) return;

        reproduceCooldownTimer = 0f;
        hunger -= 0.3f;

        Vector2 offset   = Random.insideUnitCircle * 0.8f;
        Vector2 childPos = (Vector2)transform.position + offset;
        childPos.x = Mathf.Clamp(childPos.x, -mapHalfSize.x, mapHalfSize.x);
        childPos.y = Mathf.Clamp(childPos.y, -mapHalfSize.y, mapHalfSize.y);

        CreatureManager.Instance?.SpawnOffspring(genome.Mutate(), childPos, generation + 1);
    }

    /* ======================================== Death ======================================== */
    void Die()
    {
        if (isDead) return;
        isDead = true;
        FoodSpawner.Instance?.SpawnMeatPellet(transform.position, genome.size * 0.8f);
        CreatureManager.Instance?.OnCreatureDied(this);
        Destroy(gameObject);
    }

    /* ======================================== Summary (for InspectorUI) ======================================== */
    public string GetGenomeSummary()
    {
        string dietLabel = genome.diet < 0.33f ? "Herbivore"
                         : genome.diet < 0.67f ? "Omnivore"
                         :                       "Carnivore";
        string timeLabel = genome.daylightPref < 0.35f ? "Nocturnal"
                         : genome.daylightPref > 0.65f ? "Diurnal"
                         :                               "Crepuscular";
        float activity   = GetActivityLevel();
        bool  sleeping   = activity < SleepThreshold;

        return
            $"Gen {generation}  |  Age {age:F0}/{genome.MaxAge:F0}s\n" +
            $"Diet:      {dietLabel}\n" +
            $"Speed:     {genome.speed:P0}   Vision: {genome.VisionRange:F1}u\n" +
            $"Size:      {genome.size:P0}   Fertility: {genome.fertility:P0}\n" +
            $"Aggr:      {genome.aggression:P0}   Fear: {genome.fear:P0}\n" +
            $"Flocking:  {genome.flocking:P0}   {timeLabel}\n" +
            $"Hunger:    {hunger:P0}   {(sleeping ? "Sleeping" : "Awake")}\n" +
            $"Temp Tol:  {genome.tempTolerance:P0}";
    }
}