using UnityEngine;

/// <summary>
/// Handles two UI tasks:
///   1. Population counter displayed top-left at all times.
///   2. Genome panel that appears when the player clicks a creature.
///
/// Uses Unity's immediate-mode GUI (OnGUI) so no Canvas setup is required.
/// </summary>
public class InspectorUI : MonoBehaviour
{
    [Header("Appearance")]
    public int   fontSize        = 16;
    public Color panelBackground = new(0f, 0f, 0f, 0.65f);
    public Color textColor       = new(0.95f, 0.93f, 0.85f, 1f);

    // Runtime
    private Camera    cam;
    private Creature  selected;
    private GUIStyle  labelStyle;
    private GUIStyle  boxStyle;
    private Texture2D bgTex;
    private bool      stylesBuilt;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        HandleClick();
    }

    // ── Click detection ───────────────────────────────────────────────────────

    void HandleClick()
    {
        if (!Input.GetMouseButtonUp(0)) return;

        // Ignore if the mouse has moved significantly (user was panning)
        // CameraController tracks this implicitly by whether isDragging is true,
        // but we approximate by checking if the mouse moved since button-down.
        // A simple physics overlap is sufficient here.
        Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point    = new(worldPos.x, worldPos.y);

        // Find the closest creature within a small pick radius
        float    pickRadius = 0.8f;
        Creature closest    = null;
        float    closestDist = pickRadius;

        foreach (Creature c in FindObjectsByType<Creature>(FindObjectsSortMode.None))
        {
            if (c == null || c.IsDead) continue;
            float d = Vector2.Distance(point, (Vector2)c.transform.position);
            if (d < closestDist) { closestDist = d; closest = c; }
        }

        // Toggle: clicking the same creature deselects it
        selected = (closest != null && closest != selected) ? closest : null;
    }

    // ── GUI rendering ─────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!stylesBuilt) BuildStyles();

        // ── Population HUD ────────────────────────────────────────────────────
        int pop = CreatureManager.Instance != null ? CreatureManager.Instance.Population : 0;
        GUI.Box(new Rect(10, 10, 200, 32), GUIContent.none, boxStyle);
        GUI.Label(new Rect(18, 14, 190, 28),
            $"🐾  Population: {pop}", labelStyle);

        // ── Instruction line (small, bottom-left) ────────────────────────────
        GUI.Label(new Rect(10, Screen.height - 28, 300, 24),
            "Click a creature to inspect its genome", labelStyle);

        // ── Genome panel ──────────────────────────────────────────────────────
        if (selected == null || selected.IsDead) { selected = null; return; }

        string summary = selected.GetGenomeSummary();

        // Measure required height (7 lines + padding)
        int lines       = 7;
        int lineH       = fontSize + 4;
        int panelW      = 210;
        int panelH      = lines * lineH + 24;
        int panelX      = Screen.width  - panelW - 12;
        int panelY      = 10;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none, boxStyle);
        GUI.Label(new Rect(panelX + 8, panelY + 8, panelW - 16, panelH - 16),
            summary, labelStyle);

        // Draw a coloured dot next to the panel matching the creature's colour
        Color prev     = GUI.color;
        GUI.color      = selected.genome.ToColor();
        GUI.Box(new Rect(panelX - 14, panelY + 4, 10, 10), GUIContent.none, boxStyle);
        GUI.color      = prev;
    }

    // ── Style helpers ─────────────────────────────────────────────────────────

    void BuildStyles()
    {
        bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, panelBackground);
        bgTex.Apply();

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = bgTex }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = fontSize,
            wordWrap  = true,
            normal    = { textColor = textColor },
        };

        stylesBuilt = true;
    }
}