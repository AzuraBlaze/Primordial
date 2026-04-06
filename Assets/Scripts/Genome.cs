using UnityEngine;

/// <summary>
/// All heritable data for a creature. Kept as a plain struct so it is
/// cheap to copy when producing offspring.
/// </summary>
[System.Serializable]
public struct Genome
{
    [Range(0f, 1f)] public float speed;       // Movement speed multiplier
    [Range(0f, 1f)] public float size;        // Body-size multiplier (also affects food value)
    [Range(0f, 1f)] public float diet;        // 0 = pure herbivore, 1 = pure carnivore
    [Range(0f, 1f)] public float fertility;   // Likelihood and frequency of reproduction
    [Range(0f, 1f)] public float hue;         // Visual hue (HSV)
    [Range(0f, 1f)] public float saturation;  // Visual saturation (HSV)

    /// <summary>Creates a randomised genome for the first generation.</summary>
    public static Genome Random()
    {
        return new Genome
        {
            speed      = UnityEngine.Random.Range(0.2f, 0.8f),
            size       = UnityEngine.Random.Range(0.2f, 0.8f),
            diet       = UnityEngine.Random.Range(0f,   1f),
            fertility  = UnityEngine.Random.Range(0.2f, 0.8f),
            hue        = UnityEngine.Random.Range(0f,   1f),
            saturation = UnityEngine.Random.Range(0.4f, 1f),
        };
    }

    /// <summary>
    /// Returns a child genome by copying this one and applying per-trait
    /// Gaussian-like mutations.
    /// </summary>
    public Genome Mutate(float mutationStrength = 0.08f)
    {
        return new Genome
        {
            speed      = Mathf.Clamp01(speed      + MutationDelta(mutationStrength)),
            size       = Mathf.Clamp01(size       + MutationDelta(mutationStrength)),
            diet       = Mathf.Clamp01(diet       + MutationDelta(mutationStrength)),
            fertility  = Mathf.Clamp01(fertility  + MutationDelta(mutationStrength)),
            hue        = Mathf.Repeat (hue        + MutationDelta(mutationStrength * 0.5f), 1f),
            saturation = Mathf.Clamp01(saturation + MutationDelta(mutationStrength * 0.5f)),
        };
    }

    // Box-Muller approximation: sum of two uniforms gives a triangle distribution
    // centred on 0 — good enough for mutation without System.Random overhead.
    static float MutationDelta(float strength) =>
        (UnityEngine.Random.value + UnityEngine.Random.value - 1f) * strength;

    /// <summary>Converts genome traits into a body colour (HSV).</summary>
    public Color ToColor() =>
        Color.HSVToRGB(hue, Mathf.Lerp(0.5f, 1f, saturation), 0.85f);
}