using UnityEngine;

[RequireComponent(typeof(MapView))]
[RequireComponent(typeof(MapGenerator))]
public class Map : MonoBehaviour
{
    [Header("Map Settings")]
    public Vector2 size = new (50f, 50f);

    private MapGenerator mapGenerator;
    private MapView mapView;

    void Awake()
    {
        mapGenerator = GetComponent<MapGenerator>();
        mapView      = GetComponent<MapView>();
    }

    public void Generate()
    {
        mapGenerator.GenerateMap(size);
        mapView.DrawMap(size);
    }
}