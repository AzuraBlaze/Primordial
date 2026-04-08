using UnityEngine;

/// <summary>
/// All heritable data for a creature. Kept as a plain struct so it is
/// cheap to copy when producing offspring.
///
/// Traits (all [0,1] unless noted):
///   speed          - movement speed multiplier
///   size           - body size (also affects food value on death)
///   lifespan       - [0,1] maps to 60–300 s maximum age
///   diet           - 0 = pure herbivore, 1 = pure carnivore
///   fertility      - reproduction frequency
///   vision         - radius within which food/threats are detected
///   aggression     - tendency to chase and attack other creatures
///   fear           - tendency to flee perceived threats
///   flocking       - tendency to steer toward nearby same-species peers
///   tempTolerance  - preferred temperature range width (high = tolerates more)
///   daylightPref   - 0 = nocturnal, 1 = diurnal (0.5 = crepuscular)
///   hue            - visual hue (HSV)
///   saturation     - visual saturation (HSV)
/// </summary>
[System.Serializable]
public struct Genome
{
    [Range(0f, 1f)] public float speed;
    [Range(0f, 1f)] public float size;
    [Range(0f, 1f)] public float lifespan;
    [Range(0f, 1f)] public float diet;
    [Range(0f, 1f)] public float fertility;
    [Range(0f, 1f)] public float vision;
    [Range(0f, 1f)] public float aggression;
    [Range(0f, 1f)] public float fear;
    [Range(0f, 1f)] public float flocking;
    [Range(0f, 1f)] public float tempTolerance;
    [Range(0f, 1f)] public float daylightPref;
    [Range(0f, 1f)] public float hue;
    [Range(0f, 1f)] public float saturation;

    /* ======================================== Derived Values ======================================== */

    /// <summary>Maximum age in seconds. Gene maps [0,1] => [60, 300].</summary>
    public float MaxAge => Mathf.Lerp(60f, 300f, lifespan);

    /// <summary>Vision range in world units. Gene maps [0,1] => [3, 14].</summary>
    public float VisionRange => Mathf.Lerp(3f, 14f, vision);

    /* ======================================== Public Methods ======================================== */

    /// <summary>
    /// Comfort factor [0,1] for a given temperature [0,1].
    /// Creatures prefer 0.5 ± tolerance window; outside it they take stress.
    /// tempTolerance [0,1] maps window half-width to [0.1, 0.4].
    /// </summary>
    public float TemperatureComfort(float temp)
    {
        float window = Mathf.Lerp(0.1f, 0.4f, tempTolerance);
        float dist   = Mathf.Abs(temp - 0.5f); // ideal temp is 0.5
        return Mathf.Clamp01(1f - Mathf.Max(0f, dist - window) / 0.5f);
    }

    /// <summary>
    /// Activity multiplier based on current day phase [0,1] and daylightPref.
    /// Returns 1 when fully active, approaches 0 when sleeping.
    /// </summary>
    public float DaylightActivity(float dayPhase)
    {
        // dayPhase: 0/1 = midnight, 0.5 = noon
        // daylightPref: 0 = nocturnal, 1 = diurnal, 0.5 = crepuscular
        float preferredPhase = daylightPref; // diurnal => noon (0.5), nocturnal => midnight (0 or 1)
        // Convert nocturnal so they peak at midnight
        float diff = Mathf.Abs(dayPhase - preferredPhase);
        if (diff > 0.5f) diff = 1f - diff; // wrap around
        return Mathf.Clamp01(1f - diff * 2f);
    }

    public static Genome Random()
    {
        return new()
        {
            speed         = UnityEngine.Random.Range(0.2f, 0.8f),
            size          = UnityEngine.Random.Range(0.2f, 0.8f),
            lifespan      = UnityEngine.Random.Range(0.2f, 0.8f),
            diet          = UnityEngine.Random.Range(0f,   1f),
            fertility     = UnityEngine.Random.Range(0.2f, 0.8f),
            vision        = UnityEngine.Random.Range(0.2f, 0.8f),
            aggression    = UnityEngine.Random.Range(0f,   0.6f),
            fear          = UnityEngine.Random.Range(0f,   0.6f),
            flocking      = UnityEngine.Random.Range(0f,   0.8f),
            tempTolerance = UnityEngine.Random.Range(0.2f, 0.8f),
            daylightPref  = UnityEngine.Random.Range(0.2f, 0.8f),
            hue           = UnityEngine.Random.Range(0f,   1f),
            saturation    = UnityEngine.Random.Range(0.4f, 1f),
        };
    }

    

    public Genome Mutate(float mutationStrength = 0.08f)
    {
        return new()
        {
            speed         = Mathf.Clamp01(speed        + Delta(mutationStrength)),
            size          = Mathf.Clamp01(size         + Delta(mutationStrength)),
            lifespan      = Mathf.Clamp01(lifespan     + Delta(mutationStrength)),
            diet          = Mathf.Clamp01(diet         + Delta(mutationStrength)),
            fertility     = Mathf.Clamp01(fertility    + Delta(mutationStrength)),
            vision        = Mathf.Clamp01(vision       + Delta(mutationStrength)),
            aggression    = Mathf.Clamp01(aggression   + Delta(mutationStrength)),
            fear          = Mathf.Clamp01(fear         + Delta(mutationStrength)),
            flocking      = Mathf.Clamp01(flocking     + Delta(mutationStrength)),
            tempTolerance = Mathf.Clamp01(tempTolerance+ Delta(mutationStrength)),
            daylightPref  = Mathf.Clamp01(daylightPref + Delta(mutationStrength)),
            hue           = Mathf.Repeat (hue          + Delta(mutationStrength * 0.5f), 1f),
            saturation    = Mathf.Clamp01(saturation   + Delta(mutationStrength * 0.5f)),
        };
    }

    static float Delta(float s) => (UnityEngine.Random.value + UnityEngine.Random.value - 1f) * s;

    public Color ToColor() =>
        Color.HSVToRGB(hue, Mathf.Lerp(0.5f, 1f, saturation), 0.85f);
}