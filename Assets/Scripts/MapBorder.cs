using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class MapBorder : MonoBehaviour
{
    [Header("Visuals")]
    public Color borderColor = Color.white;
    public float borderWidth = 0.2f;

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = 1;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    public void DrawBorder(Vector2 mapSize)
    {
        float halfWidth  = mapSize.x / 2f;
        float halfHeight = mapSize.y / 2f;

        lineRenderer.positionCount = 4;
        lineRenderer.SetPositions(new Vector3[]
        {
            new(-halfWidth, -halfHeight, 0f),
            new( halfWidth, -halfHeight, 0f),
            new( halfWidth,  halfHeight, 0f),
            new(-halfWidth,  halfHeight, 0f),
        });

        lineRenderer.startWidth = borderWidth;
        lineRenderer.endWidth   = borderWidth;
        lineRenderer.startColor = borderColor;
        lineRenderer.endColor   = borderColor;
    }
}