using UnityEngine;

/// <summary>
/// Handles the HUD and the creature inspection/editing panel.
///
/// Features:
///   - Population counter + day/night clock (top-left)
///   - Temperature overlay toggle button (top-left)
///   - Click a creature to open its genome panel
///   - All genome traits are editable via sliders
///   - Stats (hunger, age) shown read-only
///   - "Randomise" button re-rolls the genome
/// </summary>
public class InspectorUI : MonoBehaviour
{
    [Header("Appearance")]
    public int   fontSize        = 15;
    public Color panelBackground = new (0.05f, 0.05f, 0.10f, 0.80f);
    public Color textColor       = new (0.95f, 0.93f, 0.85f, 1f);
    public Color accentColor     = new (0.40f, 0.85f, 0.55f, 1f);
    public Color headerColor     = new (0.75f, 0.90f, 1.00f, 1f);
    public Color sliderTrack     = new (0.25f, 0.25f, 0.30f, 1f);

    /* ======================================== Runtime ======================================== */
    private Camera cam;
    private CameraController cameraController;
    private Creature selected;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle buttonStyle;
    private GUIStyle sliderStyle;
    private GUIStyle thumbStyle;
    private Texture2D bgTex;
    private Texture2D accentTex;
    private Texture2D trackTex;
    private Texture2D thumbTex;
    private bool stylesBuilt;

    // Editing state - local copy of the genome being edited
    private Genome editGenome;
    private bool isEditing; // true when a creature is selected and we are showing sliders

    // Scroll position for the panel
    private Vector2 scrollPos;

    // Panel layout constants
    const int PanelW    = 240;
    const int SliderH   = 18;
    const int RowH      = 26;
    const int PanelPadX = 10;
    const int PanelPadY = 8;

    void Start()
    {
        cam = Camera.main;
        cameraController = GetComponent<CameraController>();
    }

    void Update()
    {
        HandleClick();
    }

    /* ======================================== Click Detection ======================================== */
    void HandleClick()
    {
        if (!Input.GetMouseButtonUp(0)) return;

        // Don't steal clicks that land inside the panel
        if (isEditing && selected != null)
        {
            int panelX = Screen.width - PanelW - 12;
            if (Input.mousePosition.x > panelX) return; // click was in panel area
        }

        Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point    = new (worldPos.x, worldPos.y);

        float    pickRadius  = 0.8f;
        Creature closest     = null;
        float    closestDist = pickRadius;

        foreach (Creature c in FindObjectsByType<Creature>(FindObjectsSortMode.None))
        {
            if (c == null || c.isDead) continue;
            float d = Vector2.Distance(point, (Vector2)c.transform.position);
            if (d < closestDist) { closestDist = d; closest = c; }
        }

        if (closest != null && closest != selected)
        {
            selected   = closest;
            editGenome = selected.genome; // copy
            isEditing  = true;
            if (cameraController != null) cameraController.BeginFollow(selected);
        }
        else
        {
            selected  = null;
            isEditing = false;
            if (cameraController != null) cameraController.ReleaseFollow();
        }
    }

    /* ======================================== GUI ======================================== */
    void OnGUI()
    {
        if (!stylesBuilt) BuildStyles();

        DrawHUD();
        if (isEditing && selected != null && !selected.isDead)
            DrawInspectorPanel();
        else
        {
            selected  = null;
            isEditing = false;
            if (cameraController != null) cameraController.ReleaseFollow();
        }
    }

    /* ======================================== HUD ======================================== */
    void DrawHUD()
    {
        int pop = CreatureManager.Instance != null ? CreatureManager.Instance.Population : 0;

        // Day/night info
        string timeStr = DayNightCycle.Instance != null
            ? $"  {DayNightCycle.Instance.ClockString()} ({DayNightCycle.Instance.TimeLabel()})"
            : "";

        string hudText = $"Population: {pop}{timeStr}";

        GUI.Box(new Rect(10, 10, 310, 30), GUIContent.none, boxStyle);
        GUI.Label(new Rect(18, 14, 300, 26), hudText, labelStyle);

        // Temperature overlay toggle
        if (TemperatureMap.Instance != null)
        {
            if (GUI.Button(new Rect(10, 46, 180, 26), "Toggle Heat Map", buttonStyle))
                TemperatureMap.Instance.ToggleOverlay();
        }

        // Instruction
        GUIStyle small = new (labelStyle) { fontSize = 12 };
        small.normal.textColor = new (0.7f, 0.7f, 0.7f, 1f);
        GUI.Label(new Rect(10, Screen.height - 26, 320, 22),
            "Click creature to inspect & edit its genome", small);
    }

    /* ======================================== Inspector Panel ======================================== */
    void DrawInspectorPanel()
    {
        // Estimate content height: header + 13 trait rows + stat rows + buttons
        int traitCount = 11; // all non-visual traits
        int contentH   = PanelPadY * 2
                       + RowH * 3          // header block
                       + traitCount * RowH
                       + RowH * 3          // stats
                       + RowH * 2          // buttons
                       + 10;

        int panelH = Mathf.Min(contentH, Screen.height - 20);
        int panelX = Screen.width - PanelW - 12;
        int panelY = 10;

        GUI.Box(new Rect(panelX, panelY, PanelW, panelH), GUIContent.none, boxStyle);

        // Color swatch
        Color prev = GUI.color;
        GUI.color  = editGenome.ToColor();
        GUI.Box(new Rect(panelX - 14, panelY + 4, 10, 10), GUIContent.none, boxStyle);
        GUI.color  = prev;

        // Scrollable inner area
        Rect outerRect = new (panelX + 2, panelY + 2, PanelW - 4, panelH - 4);
        Rect innerRect = new (0, 0, PanelW - 20, contentH);
        scrollPos = GUI.BeginScrollView(outerRect, scrollPos, innerRect, false, false);

        float y  = PanelPadY;
        float lx = PanelPadX;
        float lw = PanelW - PanelPadX * 2 - 16; // subtract scrollbar width

        /* ======================================== Header ======================================== */
        GUI.Label(new Rect(lx, y, lw, 22), $"CREATURE  Gen {selected.generation}", headerStyle);
        y += 22;

        string dietLabel = editGenome.diet < 0.33f ? "Herbivore"
                         : editGenome.diet < 0.67f ? "Omnivore" : "Carnivore";
        string timeLabel = editGenome.daylightPref < 0.35f ? "Nocturnal"
                         : editGenome.daylightPref > 0.65f ? "Diurnal" : "Crepuscular";
        GUI.Label(new Rect(lx, y, lw, 18), $"{dietLabel}  |  {timeLabel}", labelStyle);
        y += 20;

        DrawDivider(lx, y, lw); y += 6;

        /* ======================================== Stats (read-only) ======================================== */
        DrawStatRow(ref y, lx, lw, "Hunger",   $"{selected.hunger:P0}");
        DrawStatRow(ref y, lx, lw, "Age",      $"{selected.age:F0} / {editGenome.MaxAge:F0} s");
        DrawStatRow(ref y, lx, lw, "Activity", GetActivityStr());

        DrawDivider(lx, y, lw); y += 6;

        /* ======================================== Editable Traits ======================================== */
        GUI.Label(new Rect(lx, y, lw, 18), "GENOME", headerStyle);
        y += 22;

        editGenome.speed         = DrawTraitSlider(ref y, lx, lw, "Speed",      editGenome.speed);
        editGenome.size          = DrawTraitSlider(ref y, lx, lw, "Size",       editGenome.size);
        editGenome.lifespan      = DrawTraitSlider(ref y, lx, lw, "Lifespan",   editGenome.lifespan);
        editGenome.diet          = DrawTraitSlider(ref y, lx, lw, "Diet",       editGenome.diet);
        editGenome.fertility     = DrawTraitSlider(ref y, lx, lw, "Fertility",  editGenome.fertility);
        editGenome.vision        = DrawTraitSlider(ref y, lx, lw, "Vision",     editGenome.vision);
        editGenome.aggression    = DrawTraitSlider(ref y, lx, lw, "Aggression", editGenome.aggression);
        editGenome.fear          = DrawTraitSlider(ref y, lx, lw, "Fear",       editGenome.fear);
        editGenome.flocking      = DrawTraitSlider(ref y, lx, lw, "Flocking",   editGenome.flocking);
        editGenome.tempTolerance = DrawTraitSlider(ref y, lx, lw, "Temp Tol",   editGenome.tempTolerance);
        editGenome.daylightPref  = DrawTraitSlider(ref y, lx, lw, "Day Pref",   editGenome.daylightPref);

        DrawDivider(lx, y, lw); y += 6;

        // Hue/saturation (visual)
        editGenome.hue        = DrawTraitSlider(ref y, lx, lw, "Hue",        editGenome.hue);
        editGenome.saturation = DrawTraitSlider(ref y, lx, lw, "Saturation", editGenome.saturation);

        y += 4;
        DrawDivider(lx, y, lw); y += 8;

        /* ======================================== Buttons ======================================== */
        float bw = (lw - 4) / 2f;

        if (GUI.Button(new Rect(lx, y, bw, 24), "Apply", buttonStyle))
            selected.SetGenome(editGenome);

        if (GUI.Button(new Rect(lx + bw + 4, y, bw, 24), "Randomise", buttonStyle))
        {
            editGenome = Genome.Random();
            selected.SetGenome(editGenome);
        }
        y += 28;

        if (GUI.Button(new Rect(lx, y, lw, 24), "Deselect", buttonStyle))
        {
            selected  = null;
            isEditing = false;
            if (cameraController != null) cameraController.ReleaseFollow();
        }

        GUI.EndScrollView();
    }

    /* ======================================== Layout Helpers ======================================== */

    void DrawStatRow(ref float y, float x, float w, string label, string value)
    {
        GUI.Label(new Rect(x, y, w * 0.55f, 18), label, labelStyle);
        GUIStyle valStyle = new (labelStyle);
        valStyle.normal.textColor = accentColor;
        valStyle.alignment        = TextAnchor.MiddleRight;
        GUI.Label(new Rect(x + w * 0.45f, y, w * 0.55f, 18), value, valStyle);
        y += RowH - 4;
    }

    float DrawTraitSlider(ref float y, float x, float w, string label, float value)
    {
        GUI.Label(new Rect(x, y, w * 0.40f, 18), label, labelStyle);
        float newVal = GUI.HorizontalSlider(
            new Rect(x + w * 0.40f, y + 3, w * 0.45f, SliderH),
            value, 0f, 1f, sliderStyle, thumbStyle
        );

        GUIStyle numStyle = new (labelStyle);
        numStyle.alignment = TextAnchor.MiddleRight;
        numStyle.fontSize  = 12;
        GUI.Label(new Rect(x + w * 0.86f, y, w * 0.14f, 18), $"{newVal:F2}", numStyle);

        y += RowH;
        return newVal;
    }

    void DrawDivider(float x, float y, float w)
    {
        Color prev = GUI.color;
        GUI.color  = new (0.3f, 0.3f, 0.35f, 0.8f);
        GUI.Box(new Rect(x, y, w, 1), GUIContent.none);
        GUI.color  = prev;
    }

    string GetActivityStr()
    {
        if (DayNightCycle.Instance == null) return "-";
        float a = selected.genome.DaylightActivity(DayNightCycle.Instance.Phase);
        if (a < 0.15f) return "Sleeping";
        return $"Awake ({a:P0})";
    }

    /* ======================================== Style Builders ======================================== */
    void BuildStyles()
    {
        bgTex     = MakeTex(panelBackground);
        accentTex = MakeTex(accentColor);
        trackTex  = MakeTex(sliderTrack);
        thumbTex  = MakeTex(accentColor);

        boxStyle = new (GUI.skin.box)
        {
            normal = { background = bgTex }
        };

        labelStyle = new (GUI.skin.label)
        {
            fontSize = fontSize,
            wordWrap = false,
            normal   = { textColor = textColor },
        };

        headerStyle = new (labelStyle)
        {
            fontSize  = fontSize,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = headerColor },
        };

        buttonStyle = new (GUI.skin.button)
        {
            fontSize = fontSize - 1,
            normal   = { textColor = textColor,   background = bgTex },
            hover    = { textColor = accentColor, background = bgTex },
            active   = { textColor = textColor,   background = accentTex },
        };

        sliderStyle = new (GUI.skin.horizontalSlider);
        sliderStyle.normal.background = trackTex;
        sliderStyle.fixedHeight       = 4f;

        thumbStyle = new (GUI.skin.horizontalSliderThumb);
        thumbStyle.normal.background = thumbTex;
        thumbStyle.fixedWidth        = 10f;
        thumbStyle.fixedHeight       = 14f;

        stylesBuilt = true;
    }

    static Texture2D MakeTex(Color c)
    {
        Texture2D t = new (1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}