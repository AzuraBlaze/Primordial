using UnityEngine;

/// <summary>
/// Handles the HUD and the creature inspection/editing panel.
///
/// Features:
///   - Population counter (top-left)
///   - Temperature overlay toggle button (top-left)
///   - Day/Night arc clock HUD (top-right) with baked sun and moon textures
///   - Click a creature to open its genome panel
///   - All genome traits are editable via sliders
///   - Stats (hunger, age) shown read-only
///   - "Randomize" button re-rolls the genome
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

    [Header("Clock HUD")]
    [Tooltip("Diameter of the arc clock widget in pixels.")]
    public int clockSize = 80;

    /* ======================================== Runtime ======================================== */
    private Camera cam;
    private CameraController cameraController;
    private Creature selected;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle dividerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle sliderStyle;
    private GUIStyle thumbStyle;
    private GUIStyle clockLabelStyle;
    private GUIStyle clockTimeStyle;
    private Texture2D bgTex;
    private Texture2D accentTex;
    private Texture2D trackTex;
    private Texture2D thumbTex;

    // Clock textures -- rebuilt whenever clockSize changes
    private Texture2D clockArcTex;
    private Texture2D sunTex;
    private Texture2D moonTex;
    private int       clockTexSize;

    private bool stylesBuilt;

    // Editing state - local copy of the genome being edited
    private Genome editGenome;
    private bool isEditing; // true when a creature is selected and we are showing sliders

    // Scroll position for the panel
    private Vector2 scrollPos;

    // Panel layout constants
    const int PanelW    = 425;
    const int LabelH    = 25;   // label rect height, must exceed fontSize to avoid clipping
    const int SliderH   = 18;
    const int RowH      = 26;
    const int PanelPadX = 10;
    const int PanelPadY = 8;

    // Clock HUD margin from screen edge
    const int ClockMargin = 12;

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

        // Don't steal clicks that land inside the inspector panel
        if (isEditing && selected != null)
        {
            int panelX = Screen.width - PanelW - 12;
            if (Input.mousePosition.x > panelX) return;
        }

        // Don't steal clicks that land inside the clock HUD
        int clockX = Screen.width  - clockSize - ClockMargin;
        int clockY = ClockMargin;
        Vector2 mouse2D = new (Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (new Rect(clockX, clockY, clockSize, clockSize + 30).Contains(mouse2D)) return;

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
        DrawClockHUD();

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
        string hudText = $"Population: {pop}";

        GUI.Box(new Rect(10, 10, 200, 30), GUIContent.none, boxStyle);
        GUI.Label(new Rect(18, 14, 190, LabelH), hudText, labelStyle);

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

    /* ======================================== Clock HUD ======================================== */

    void DrawClockHUD()
    {
        if (DayNightCycle.Instance == null) return;

        float phase  = DayNightCycle.Instance.Phase;
        int   cx     = ClockMargin + 5;
        int   cy     = 80;
        int   totalH = clockSize + 36; // arc + two text rows

        // Background pill
        GUI.Box(new Rect(cx - 6, cy - 4, clockSize + 12, totalH + 8), GUIContent.none, boxStyle);

        // Rebuild baked textures when clockSize changes
        if (clockArcTex == null || clockTexSize != clockSize)
            RebuildClockTextures();

        // Draw the baked arc track
        GUI.DrawTexture(new Rect(cx, cy, clockSize, clockSize), clockArcTex);

        // Draw the sun or moon icon on the ring
        DrawCelestialBody(phase, cx, cy);

        // Time and label below the arc
        GUI.Label(new Rect(cx, cy + clockSize + 2,  clockSize, 18),
            DayNightCycle.Instance.ClockString(), clockTimeStyle);
        GUI.Label(new Rect(cx, cy + clockSize + 18, clockSize, 18),
            DayNightCycle.Instance.TimeLabel(), clockLabelStyle);
    }

    /* ======================================== Clock Texture Baking ======================================== */

    void RebuildClockTextures()
    {
        clockTexSize = clockSize;

        if (clockArcTex != null) Object.Destroy(clockArcTex);
        if (sunTex      != null) Object.Destroy(sunTex);
        if (moonTex     != null) Object.Destroy(moonTex);

        clockArcTex = BakeArcTexture(clockSize);
        sunTex      = BakeSunTexture(Mathf.RoundToInt(clockSize * 0.22f));
        moonTex     = BakeMoonTexture(Mathf.RoundToInt(clockSize * 0.22f));
    }

    /// <summary>
    /// Bakes the donut ring with four tick marks at the cardinal phase positions
    /// (midnight, dawn, noon, dusk).
    /// </summary>
    static Texture2D BakeArcTexture(int s)
    {
        Texture2D tex = new (s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color clear      = new (0f, 0f, 0f, 0f);
        Color trackColor = new (0.30f, 0.30f, 0.40f, 0.70f);
        Color tickColor  = new (0.60f, 0.60f, 0.70f, 0.90f);

        float cx     = (s - 1) * 0.5f;
        float cy     = (s - 1) * 0.5f;
        float outerR = s * 0.46f;
        float innerR = s * 0.38f;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx   = x - cx;
            float dy   = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            tex.SetPixel(x, y, (dist >= innerR && dist <= outerR) ? trackColor : clear);
        }

        // Tick marks at midnight (0), dawn (0.25), noon (0.5), dusk (0.75)
        float[] tickPhases = { 0f, 0.25f, 0.5f, 0.75f };
        foreach (float tp in tickPhases)
        {
            float angle = PhaseToAngleRad(tp);
            for (float r = innerR - 2f; r <= outerR + 2f; r += 0.5f)
            {
                int tx = Mathf.RoundToInt(cx + Mathf.Cos(angle) * r);
                int ty = Mathf.RoundToInt(cy + Mathf.Sin(angle) * r);
                if (tx >= 0 && tx < s && ty >= 0 && ty < s)
                    tex.SetPixel(tx, ty, tickColor);
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Bakes a sun icon: a filled circle core surrounded by eight short ray dashes,
    /// all in warm yellow-white.
    /// </summary>
    static Texture2D BakeSunTexture(int s)
    {
        Texture2D tex = new (s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color clear  = new (0f, 0f, 0f, 0f);
        Color center = new (1.00f, 0.95f, 0.55f, 1f);    // warm yellow core
        Color ray    = new (1.00f, 0.88f, 0.40f, 0.85f); // slightly dimmer rays

        float cx    = (s - 1) * 0.5f;
        float cy    = (s - 1) * 0.5f;
        float coreR = s * 0.30f;
        float rayIn = s * 0.40f;  // ray inner radius
        float rayOut= s * 0.50f;  // ray outer radius
        float rayW  = s * 0.10f;  // ray half-width in world-space units

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx   = x - cx;
            float dy   = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist <= coreR)
            {
                tex.SetPixel(x, y, center);
                continue;
            }

            // Eight rays at 45-degree increments
            bool inRay = false;
            if (dist >= rayIn && dist <= rayOut)
            {
                float angle = Mathf.Atan2(dy, dx);
                float snap  = Mathf.Round(angle / (Mathf.PI * 0.25f)) * (Mathf.PI * 0.25f);
                float delta = Mathf.Abs(Mathf.DeltaAngle(
                    angle * Mathf.Rad2Deg, snap * Mathf.Rad2Deg));
                inRay = delta * Mathf.Deg2Rad * dist < rayW;
            }

            tex.SetPixel(x, y, inRay ? ray : clear);
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Bakes a crescent moon icon: a filled circle with a smaller offset circle
    /// subtracted to create the crescent shape, in cool blue-white.
    /// </summary>
    static Texture2D BakeMoonTexture(int s)
    {
        Texture2D tex = new (s, s, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color clear = new (0f, 0f, 0f, 0f);
        Color fill  = new (0.80f, 0.90f, 1.00f, 1f);   // pale cool blue-white

        float cx      = (s - 1) * 0.5f;
        float cy      = (s - 1) * 0.5f;
        float moonR   = s * 0.40f;
        // The bite circle is offset upper-right, creating a crescent facing lower-left
        float biteR   = s * 0.34f;
        float biteOff = s * 0.22f;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx   = x - cx;
            float dy   = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist > moonR) { tex.SetPixel(x, y, clear); continue; }

            float bdx   = x - (cx + biteOff);
            float bdy   = y - (cy + biteOff);
            bool inBite = (bdx * bdx + bdy * bdy) <= biteR * biteR;

            tex.SetPixel(x, y, inBite ? clear : fill);
        }

        tex.Apply();
        return tex;
    }

    /* ======================================== Celestial Body Drawing ======================================== */

    void DrawCelestialBody(float phase, int clockX, int clockY)
    {
        int   s     = clockSize;
        float cx    = s * 0.5f;
        float cy    = s * 0.5f;
        float bodyR = s * 0.42f; // orbit radius -- midpoint of the ring

        float angle = PhaseToAngleRad(phase);

        // Position in texture space (origin bottom-left)
        float texX = cx + Mathf.Cos(angle) * bodyR;
        float texY = cy + Mathf.Sin(angle) * bodyR;

        bool      isDay = phase > 0.2f && phase < 0.8f;
        Texture2D icon  = isDay ? sunTex : moonTex;
        float     iconW = icon.width;
        float     iconH = icon.height;

        // Convert from texture space (Y=0 bottom) to GUI space (Y=0 top)
        float guiX = clockX + texX - iconW * 0.5f;
        float guiY = clockY + (s - texY) - iconH * 0.5f;

        GUI.DrawTexture(new Rect(guiX, guiY, iconW, iconH), icon);
    }

    /* ======================================== Phase -> Angle ======================================== */

    /// <summary>
    /// Converts a day phase [0,1] to a radian angle for placement on the clock ring.
    /// Noon (0.5) sits at the top (PI/2). Midnight (0/1) sits at the bottom (-PI/2).
    /// </summary>
    static float PhaseToAngleRad(float phase)
    {
        return Mathf.PI * 0.5f - phase * Mathf.PI * 2f;
    }

    /* ======================================== Inspector Panel ======================================== */
    void DrawInspectorPanel()
    {
        int traitCount = 11;
        int contentH   = PanelPadY * 2
                       + RowH * 3
                       + traitCount * RowH
                       + RowH * 3
                       + RowH * 2
                       + 80;

        int panelH = Mathf.Min(contentH, Screen.height - 20);
        int panelX = Screen.width - PanelW - 12;
        int panelY = 10;

        GUI.Box(new Rect(panelX, panelY, PanelW, panelH), GUIContent.none, boxStyle);

        // Color swatch
        Color prev = GUI.color;
        GUI.color  = editGenome.ToColor();
        GUI.Box(new Rect(panelX - 14, panelY + 4, 10, 10), GUIContent.none, boxStyle);
        GUI.color  = prev;

        Rect outerRect = new (panelX + 6, panelY + 6, PanelW - 12, panelH - 12);
        // Rect innerRect = new (0, 0, PanelW - 20, contentH);
        // scrollPos = GUI.BeginScrollView(outerRect, scrollPos, innerRect, false, false);
        GUI.BeginGroup(outerRect);

        float y  = PanelPadY;
        float lx = PanelPadX;
        float lw = PanelW - PanelPadX * 2 - 16;

        GUI.Label(new Rect(lx, y, lw, 22), $"CREATURE  Gen {selected.generation}", headerStyle);
        y += 22;

        string dietLabel = editGenome.diet < 0.33f ? "Herbivore"
                         : editGenome.diet < 0.67f ? "Omnivore" : "Carnivore";
        string timeLabel = editGenome.daylightPref < 0.35f ? "Nocturnal"
                         : editGenome.daylightPref > 0.65f ? "Diurnal" : "Crepuscular";
        GUI.Label(new Rect(lx, y, lw, LabelH), $"{dietLabel}  |  {timeLabel}", labelStyle);
        y += LabelH + 2;

        DrawDivider(lx, y, lw); y += 6;

        DrawStatRow(ref y, lx, lw, "Hunger",   $"{selected.hunger:P0}");
        DrawStatRow(ref y, lx, lw, "Age",      $"{selected.age:F0} / {editGenome.MaxAge:F0} s");
        DrawStatRow(ref y, lx, lw, "Activity", GetActivityStr());

        DrawDivider(lx, y, lw); y += 6;

        GUI.Label(new Rect(lx, y, lw, LabelH), "GENOME", headerStyle);
        y += LabelH + 4;

        editGenome.speed         = DrawTraitSlider(ref y, lx, lw, "Speed",                 editGenome.speed);
        editGenome.size          = DrawTraitSlider(ref y, lx, lw, "Size",                  editGenome.size);
        editGenome.lifespan      = DrawTraitSlider(ref y, lx, lw, "Lifespan",              editGenome.lifespan);
        editGenome.diet          = DrawTraitSlider(ref y, lx, lw, "Diet",                  editGenome.diet);
        editGenome.fertility     = DrawTraitSlider(ref y, lx, lw, "Fertility",             editGenome.fertility);
        editGenome.vision        = DrawTraitSlider(ref y, lx, lw, "Vision",                editGenome.vision);
        editGenome.aggression    = DrawTraitSlider(ref y, lx, lw, "Aggression",            editGenome.aggression);
        editGenome.fear          = DrawTraitSlider(ref y, lx, lw, "Fear",                  editGenome.fear);
        editGenome.flocking      = DrawTraitSlider(ref y, lx, lw, "Flocking",              editGenome.flocking);
        editGenome.tempTolerance = DrawTraitSlider(ref y, lx, lw, "Temperature Tolerance", editGenome.tempTolerance);
        editGenome.daylightPref  = DrawTraitSlider(ref y, lx, lw, "Day Preference",        editGenome.daylightPref);

        DrawDivider(lx, y, lw); y += 6;

        editGenome.hue        = DrawTraitSlider(ref y, lx, lw, "Hue",        editGenome.hue);
        editGenome.saturation = DrawTraitSlider(ref y, lx, lw, "Saturation", editGenome.saturation);

        y += 8;

        float bw = (lw - 4) / 2f;

        if (GUI.Button(new Rect(lx, y, bw, 24), "Apply", buttonStyle))
            selected.SetGenome(editGenome);

        if (GUI.Button(new Rect(lx + bw + 4, y, bw, 24), "Randomize", buttonStyle))
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

        GUI.EndGroup();
    }

    /* ======================================== Layout Helpers ======================================== */

    void DrawStatRow(ref float y, float x, float w, string label, string value)
    {
        GUI.Label(new Rect(x, y, w * 0.55f, LabelH), label, labelStyle);
        GUIStyle valStyle = new (labelStyle);
        valStyle.normal.textColor = accentColor;
        valStyle.alignment        = TextAnchor.MiddleRight;
        GUI.Label(new Rect(x + w * 0.45f, y, w * 0.55f, LabelH), value, valStyle);
        y += RowH - 4;
    }

    float DrawTraitSlider(ref float y, float x, float w, string label, float value)
    {
        GUI.Label(new Rect(x, y, w * 0.52f, LabelH), label, labelStyle);
        float newVal = GUI.HorizontalSlider(
            new Rect(x + w * 0.52f, y + 3, w * 0.34f, SliderH),
            value, 0f, 1f, sliderStyle, thumbStyle
        );

        GUIStyle numStyle = new (labelStyle);
        numStyle.alignment = TextAnchor.MiddleRight;
        numStyle.fontSize  = 12;
        GUI.Label(new Rect(x + w * 0.86f, y, w * 0.14f, LabelH), $"{newVal:F2}", numStyle);

        y += RowH;
        return newVal;
    }

    void DrawDivider(float x, float y, float w)
    {
        GUI.Box(new Rect(x, y, w, 1), GUIContent.none, dividerStyle);
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

        // Flat single-pixel style used exclusively by DrawDivider.
        dividerStyle = new GUIStyle();
        dividerStyle.normal.background = MakeTex(new Color(0.3f, 0.3f, 0.35f, 0.8f));

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

        clockTimeStyle = new (GUI.skin.label)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = textColor },
        };

        clockLabelStyle = new (GUI.skin.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.UpperCenter,
            normal    = { textColor = new Color(0.60f, 0.65f, 0.80f, 1f) },
        };

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