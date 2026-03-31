using UnityEngine;

public class MapView : MonoBehaviour
{
    [Header("Background")]
    public Color backgroundColor = new (0.60f, 0.60f, 0.60f, 1f);

    private GameObject backgroundQuad;

    void Awake()
    {
        backgroundQuad = new ("MapBackground");
        backgroundQuad.transform.SetParent(transform);

        MeshFilter   meshFilter   = backgroundQuad.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = backgroundQuad.AddComponent<MeshRenderer>();

        meshFilter.mesh             = CreateQuadMesh();
        meshRenderer.material       = new (Shader.Find("Sprites/Default"));
        meshRenderer.material.color = backgroundColor;
        meshRenderer.sortingOrder   = -1;
    }

    public void DrawMap(Vector2 mapSize)
    {
        backgroundQuad.transform.localPosition = Vector3.zero;
        backgroundQuad.transform.localScale    = new (mapSize.x, mapSize.y, 1f);
    }

    static Mesh CreateQuadMesh()
    {
        Mesh mesh = new() { name = "BackgroundQuad" };

        mesh.vertices  = new Vector3[]
        {
            new (-0.5f, -0.5f, 0f),
            new ( 0.5f, -0.5f, 0f),
            new ( 0.5f,  0.5f, 0f),
            new (-0.5f,  0.5f, 0f),
        };
        mesh.triangles = new [] { 0, 1, 2, 0, 2, 3 };
        mesh.uv        = new [] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };

        mesh.RecalculateNormals();
        return mesh;
    }
}