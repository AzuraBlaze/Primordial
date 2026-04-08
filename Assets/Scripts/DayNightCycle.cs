using UnityEngine;

/// <summary>
/// Drives a day/night cycle and renders an overlay quad that dims the scene
/// to represent night. Also exposes the current day phase as a static
/// property so Creature and other systems can read it cheaply.
///
/// Phase 0/1 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk.
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

    [Header("Night Darkness")]
    [Tooltip("Maximum alpha of the darkness overlay at midnight (0 = never dark).")]
    [Range(0f, 0.85f)]
    public float maxDarknessAlpha = 0.55f;

    [Header("Colors")]
    public Color dawnColor  = new (1.0f, 0.75f, 0.50f, 1f);
    public Color noonColor  = new (1.0f, 1.00f, 1.00f, 1f);
    public Color duskColor  = new (1.0f, 0.60f, 0.35f, 1f);
    public Color nightColor = new (0.1f, 0.15f, 0.40f, 1f);

    /* ======================================== Public (read-only) ======================================== */
    /// <summary>Current phase in [0,1). 0/1 = midnight, 0.5 = noon.</summary>
    public float Phase { get; private set; }

    /// <summary>True when the sun is above the horizon (phase in [0.2, 0.8]).</summary>
    public bool IsDay => Phase > 0.2f && Phase < 0.8f;

    /* ======================================== Private ======================================== */
    private GameObject overlayQuad;
    private MeshRenderer overlayRenderer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Phase = startPhase;
        CreateOverlay();
    }

    void CreateOverlay()
    {
        overlayQuad = new ("DayNightOverlay");
        overlayQuad.transform.SetParent(transform);

        // Place it far in front of the map background but behind creatures
        overlayQuad.transform.localPosition = new (0f, 0f, -0.5f);

        MeshFilter mf = overlayQuad.AddComponent<MeshFilter>();
        overlayRenderer = overlayQuad.AddComponent<MeshRenderer>();

        mf.mesh = CreateFullscreenQuad();

        // Use a transparent-capable shader
        Material mat = new (Shader.Find("Sprites/Default"));
        mat.color = new (0f, 0f, 0f, 0f);
        overlayRenderer.material = mat;
        overlayRenderer.sortingOrder = 10; // above everything except UI
    }

    public void SetMapBounds(Vector2 mapSize, float padding = 5f)
    {
        float w = mapSize.x + padding * 2f;
        float h = mapSize.y + padding * 2f;
        overlayQuad.transform.localScale = new (w, h, 1f);
    }

    void Update()
    {
        Phase = Mathf.Repeat(Phase + Time.deltaTime / dayDuration, 1f);
        ApplyOverlay();
    }

    void ApplyOverlay()
    {
        // Compute darkness: deepest at midnight, zero at noon
        // Use a smooth curve: darkness = cos(phase * 2pi) mapped to [0,1]
        float darknessT = Mathf.Clamp01((Mathf.Cos(Phase * Mathf.PI * 2f) + 1f) * 0.5f);
        // darknessT = 1 at midnight, 0 at noon

        // Tint color: blend through dawn/noon/dusk/night based on phase
        Color tint = PhaseToColor(Phase);
        tint.a = darknessT * maxDarknessAlpha;
        overlayRenderer.material.color = tint;
    }

    Color PhaseToColor(float p)
    {
        // p: 0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk
        if (p < 0.25f)
        {
            // midnight --> dawn
            return Color.Lerp(nightColor, dawnColor, p / 0.25f);
        }
        else if (p < 0.5f)
        {
            // dawn --> noon
            return Color.Lerp(dawnColor, noonColor, (p - 0.25f) / 0.25f);
        }
        else if (p < 0.75f)
        {
            // noon --> dusk
            return Color.Lerp(noonColor, duskColor, (p - 0.5f) / 0.25f);
        }
        else
        {
            // dusk --> midnight
            return Color.Lerp(duskColor, nightColor, (p - 0.75f) / 0.25f);
        }
    }

    static Mesh CreateFullscreenQuad()
    {
        Mesh mesh = new() { name = "DayNightQuad" };
        mesh.vertices  = new Vector3[] {
            new (-0.5f, -0.5f, 0f), new (0.5f, -0.5f, 0f),
            new ( 0.5f,  0.5f, 0f), new (-0.5f, 0.5f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.uv        = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.RecalculateNormals();
        return mesh;
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