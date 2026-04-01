using UnityEngine;

public class MapView : MonoBehaviour
{
    [Header("Biome Colours")]
    public Color sedimentColor = new(0.59f, 0.44f, 0.27f, 1f); // muddy brown
    public Color brineColor    = new(0.18f, 0.42f, 0.65f, 1f); // deep teal-blue
    public Color bloomColor    = new(0.27f, 0.55f, 0.25f, 1f); // earthy green

    private MapGenerator mapGenerator;
    private GameObject   backgroundQuad;
    private Texture2D    biomeTexture;

    void Awake()
    {
        mapGenerator = GetComponent<MapGenerator>();

        backgroundQuad = new("MapBackground");
        backgroundQuad.transform.SetParent(transform);

        MeshFilter   meshFilter   = backgroundQuad.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = backgroundQuad.AddComponent<MeshRenderer>();

        meshFilter.mesh           = CreateQuadMesh();
        meshRenderer.material     = new(Shader.Find("Sprites/Default"));
        meshRenderer.sortingOrder = -1;
    }

    public void DrawMap(Vector2 mapSize)
    {
        backgroundQuad.transform.localPosition = Vector3.zero;
        backgroundQuad.transform.localScale    = new(mapSize.x, mapSize.y, 1f);

        BuildAndApplyTexture(mapGenerator.BiomeMap, mapGenerator.Resolution);
    }

    void BuildAndApplyTexture(Biome[,] biomeMap, Vector2Int resolution)
    {
        if (biomeTexture != null)
            Destroy(biomeTexture);

        biomeTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, mipChain: false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color[] pixels = new Color[resolution.x * resolution.y];

        for (int y = 0; y < resolution.y; y++)
        {
            for (int x = 0; x < resolution.x; x++)
            {
                pixels[y * resolution.x + x] = BiomeToColor(biomeMap[x, y]);
            }
        }

        biomeTexture.SetPixels(pixels);
        biomeTexture.Apply();

        MeshRenderer mr = backgroundQuad.GetComponent<MeshRenderer>();
        mr.material.mainTexture = biomeTexture;
        mr.material.color       = Color.white; // Let the texture supply the color
    }

    Color BiomeToColor(Biome biome) => biome switch
    {
        Biome.Sediment => sedimentColor,
        Biome.Brine    => brineColor,
        Biome.Bloom    => bloomColor,
        _              => Color.magenta, // Should be unreachable
    };

    static Mesh CreateQuadMesh()
    {
        Mesh mesh = new() { name = "BackgroundQuad" };

        mesh.vertices = new Vector3[]
        {
            new(-0.5f, -0.5f, 0f),
            new( 0.5f, -0.5f, 0f),
            new( 0.5f,  0.5f, 0f),
            new(-0.5f,  0.5f, 0f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.uv        = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };

        mesh.RecalculateNormals();
        return mesh;
    }
}