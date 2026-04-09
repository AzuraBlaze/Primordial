using UnityEngine;

/// <summary>
/// Drives a day/night cycle and exposes the current phase as a static
/// property so Creature and other systems can read it cheaply.
///
/// Phase 0/1 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk.
///
/// The old full-screen darkness overlay has been removed; day/night is
/// communicated purely through the HUD clock drawn by InspectorUI.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Timing")]
    [Tooltip("Duration of one full day in real seconds.")]
    public float dayDuration = 120f;

    [Tooltip("Starting phase (0=midnight, 0.5=noon).")]
    [Range(0f, 1f)]
    public float startPhase = 0.25f;

    /* ======================================== Public (read-only) ======================================== */
    /// <summary>Current phase in [0,1). 0/1 = midnight, 0.5 = noon.</summary>
    public float Phase { get; private set; }

    /// <summary>True when the sun is above the horizon (phase in [0.2, 0.8]).</summary>
    public bool IsDay => Phase > 0.2f && Phase < 0.8f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Phase = startPhase;
    }

    // SetMapBounds is kept so Main.cs does not need changes.
    public void SetMapBounds(Vector2 mapSize, float padding = 5f) { }

    void Update()
    {
        Phase = Mathf.Repeat(Phase + Time.deltaTime / dayDuration, 1f);
    }

    /* ======================================== Public Helpers ======================================== */

    /// <summary>Returns a short human-readable label for the current time of day.</summary>
    public string TimeLabel()
    {
        if (Phase < 0.15f || Phase > 0.85f) return "Night";
        if (Phase < 0.30f) return "Dawn";
        if (Phase < 0.70f) return "Day";
        return "Dusk";
    }

    /// <summary>Simulated clock in HH:MM format (24-hour).</summary>
    public string ClockString()
    {
        int totalMinutes = Mathf.FloorToInt(Phase * 24f * 60f);
        int h = totalMinutes / 60;
        int m = totalMinutes % 60;
        return $"{h:D2}:{m:D2}";
    }
}