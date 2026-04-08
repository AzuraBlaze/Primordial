using UnityEngine;

public class MapView : MonoBehaviour
{
    [Header("Biome Colors")]
    public Color sedimentColor = new (0.59f, 0.44f, 0.27f, 1f); // muddy brown
    public Color brineColor    = new (0.18f, 0.42f, 0.65f, 1f); // deep teal-blue
    public Color bloomColor    = new (0.27f, 0.55f, 0.25f, 1f); // earthy green

    [Header("Border Smoothing")]
    [Tooltip("Radius in pixels of the box-blur applied to biome borders. " +
             "0 = no blur, 3-6 gives soft transitions, 10+ is very painterly.")]
    [Range(0, 16)]
    public int blurRadius = 5;

    private MapGenerator mapGenerator;
    private GameObject   backgroundQuad;
    private Texture2D    biomeTexture;

    void Awake()
    {
        mapGenerator = GetComponent<MapGenerator>();

        backgroundQuad = new ("Background");
        backgroundQuad.transform.SetParent(transform);

        MeshFilter   meshFilter   = backgroundQuad.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = backgroundQuad.AddComponent<MeshRenderer>();

        meshFilter.mesh           = CreateQuadMesh();
        meshRenderer.material     = new (Shader.Find("Sprites/Default"));
        meshRenderer.sortingOrder = -1;
    }

    public void DrawMap(Vector2 mapSize)
    {
        backgroundQuad.transform.localPosition = Vector3.zero;
        backgroundQuad.transform.localScale    = new (mapSize.x, mapSize.y, 1f);

        BuildAndApplyTexture(mapGenerator.BiomeMap, mapGenerator.Resolution);
    }

    void BuildAndApplyTexture(Biome[,] biomeMap, Vector2Int resolution)
    {
        if (biomeTexture != null)
            Destroy(biomeTexture);

        biomeTexture = new (resolution.x, resolution.y, TextureFormat.RGB24, mipChain: false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        int w = resolution.x;
        int h = resolution.y;

        // Convert biome map to a linear color array
        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = BiomeToColor(biomeMap[x, y]);

        // Box blur (separable, aka horizontal then vertical)
        if (blurRadius > 0)
            pixels = BoxBlurSeparable(pixels, w, h, blurRadius);

        biomeTexture.SetPixels(pixels);
        biomeTexture.Apply();

        MeshRenderer mr = backgroundQuad.GetComponent<MeshRenderer>();
        mr.material.mainTexture = biomeTexture;
        mr.material.color       = Color.white;
    }

    /// <summary>
    /// Two-pass separable box blur.  O(w*h*r) rather than O(w*h*r²).
    /// Each pass uses a sliding-window accumulator so the radius has almost
    /// no impact on performance.
    /// </summary>
    static Color[] BoxBlurSeparable(Color[] src, int w, int h, int r)
    {
        var tmp = new Color[src.Length];
        var dst = new Color[src.Length];

        // Horizontal pass: src => tmp
        for (int y = 0; y < h; y++)
        {
            int rowBase = y * w;

            // Initialise the accumulator for the first window
            Color acc = Color.black;
            int   windowSize = 0;

            for (int kx = 0; kx <= r && kx < w; kx++)
            {
                acc += src[rowBase + kx];
                windowSize++;
            }

            for (int x = 0; x < w; x++)
            {
                tmp[rowBase + x] = acc / windowSize;

                // Add the right edge of the next window
                int addX = x + r + 1;
                if (addX < w) { acc += src[rowBase + addX]; windowSize++; }

                // Remove the left edge that has fallen out
                int removeX = x - r;
                if (removeX >= 0) { acc -= src[rowBase + removeX]; windowSize--; }
            }
        }

        // Vertical pass: tmp => dst
        for (int x = 0; x < w; x++)
        {
            Color acc = Color.black;
            int   windowSize = 0;

            for (int ky = 0; ky <= r && ky < h; ky++)
            {
                acc += tmp[ky * w + x];
                windowSize++;
            }

            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = acc / windowSize;

                int addY = y + r + 1;
                if (addY < h) { acc += tmp[addY * w + x]; windowSize++; }

                int removeY = y - r;
                if (removeY >= 0) { acc -= tmp[removeY * w + x]; windowSize--; }
            }
        }

        return dst;
    }

    Color BiomeToColor(Biome biome) => biome switch
    {
        Biome.Sediment => sedimentColor,
        Biome.Brine    => brineColor,
        Biome.Bloom    => bloomColor,
        _              => Color.magenta,
    };

    static Mesh CreateQuadMesh()
    {
        Mesh mesh = new()
        {
            name = "BackgroundQuad",
            vertices = new Vector3[]
            {
                new (-0.5f, -0.5f, 0f),
                new ( 0.5f, -0.5f, 0f),
                new ( 0.5f,  0.5f, 0f),
                new (-0.5f,  0.5f, 0f),
            },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }
        };

        mesh.RecalculateNormals();
        return mesh;
    }
}