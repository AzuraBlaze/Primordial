using UnityEngine;

/// <summary>
/// Generates a Perlin-noise-based temperature map over the world and
/// optionally shows a semi-transparent heat overlay.
///
/// Temperature is [0,1]: 0 = freezing, 0.5 = temperate, 1 = scorching.
/// Creatures sample their tile temperature via <see cref="SampleTemperature"/>.
/// </summary>
public class TemperatureMap : MonoBehaviour
{
    public static TemperatureMap Instance { get; private set; }

    [Header("Generation")]
    [Tooltip("Perlin noise scale (larger = broader hot/cold regions).")]
    public float noiseScale = 0.08f;

    [Tooltip("Random seed offset. Change to get different maps.")]
    public float seedOffset = 47.3f;

    [Header("Overlay")]
    [Tooltip("Show the temperature overlay.")]
    public bool showOverlay = false;

    [Tooltip("Maximum alpha of the colour overlay (0 = invisible).")]
    [Range(0f, 0.5f)]
    public float overlayAlpha = 0.25f;

    public Color coldColor = new(0.2f, 0.5f, 1.0f, 1f);
    public Color hotColor  = new(1.0f, 0.2f, 0.1f, 1f);

    // ── Private ───────────────────────────────────────────────────────────────
    private Vector2    mapSize;
    private Vector2    mapOffset;   // world-space bottom-left corner
    private GameObject overlayQuad;
    private Texture2D  heatTexture;
    private bool       overlayVisible;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Call this after the map is generated.</summary>
    public void Initialise(Vector2 size)
    {
        mapSize   = size;
        mapOffset = -size * 0.5f;

        GenerateOverlayTexture();
        BuildOverlayQuad();
        SetOverlayVisible(showOverlay);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sample temperature [0,1] at a world position.
    /// Uses the same Perlin function as the texture so it is perfectly aligned.
    /// </summary>
    public float SampleTemperature(Vector2 worldPos)
    {
        float nx = (worldPos.x - mapOffset.x) / mapSize.x;
        float ny = (worldPos.y - mapOffset.y) / mapSize.y;
        return RawNoise(nx, ny);
    }

    /// <summary>Toggle the visual overlay on/off.</summary>
    public void SetOverlayVisible(bool visible)
    {
        overlayVisible = visible;
        if (overlayQuad != null)
            overlayQuad.SetActive(visible);
    }

    public void ToggleOverlay() => SetOverlayVisible(!overlayVisible);

    // ── Generation ────────────────────────────────────────────────────────────

    float RawNoise(float nx, float ny)
    {
        // Two octaves for more interesting shapes
        float v  = Mathf.PerlinNoise(nx * noiseScale * 100f + seedOffset,
                                     ny * noiseScale * 100f + seedOffset);
        float v2 = Mathf.PerlinNoise(nx * noiseScale * 200f + seedOffset + 13.7f,
                                     ny * noiseScale * 200f + seedOffset + 29.1f);
        return Mathf.Clamp01(v * 0.7f + v2 * 0.3f);
    }

    void GenerateOverlayTexture()
    {
        int w = Mathf.RoundToInt(mapSize.x);
        int h = Mathf.RoundToInt(mapSize.y);

        if (heatTexture != null) Destroy(heatTexture);

        heatTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color[] pixels = new Color[w * h];
        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                float t = RawNoise((float)px / w, (float)py / h);
                Color c = Color.Lerp(coldColor, hotColor, t);
                c.a = overlayAlpha;
                pixels[py * w + px] = c;
            }
        }

        heatTexture.SetPixels(pixels);
        heatTexture.Apply();
    }

    void BuildOverlayQuad()
    {
        overlayQuad = new GameObject("TemperatureOverlay");
        overlayQuad.transform.SetParent(transform);
        overlayQuad.transform.localPosition = new Vector3(0f, 0f, -0.4f);
        overlayQuad.transform.localScale    = new Vector3(mapSize.x, mapSize.y, 1f);

        MeshFilter mf     = overlayQuad.AddComponent<MeshFilter>();
        MeshRenderer mr   = overlayQuad.AddComponent<MeshRenderer>();

        mf.mesh = CreateQuad();

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = heatTexture;
        mat.color       = Color.white;
        mr.material     = mat;
        mr.sortingOrder = 0; // just above the map background (-1)
    }

    static Mesh CreateQuad()
    {
        Mesh mesh = new() { name = "TempOverlayQuad" };
        mesh.vertices  = new Vector3[] {
            new(-0.5f, -0.5f, 0f), new(0.5f, -0.5f, 0f),
            new( 0.5f,  0.5f, 0f), new(-0.5f, 0.5f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.uv        = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.RecalculateNormals();
        return mesh;
    }
}