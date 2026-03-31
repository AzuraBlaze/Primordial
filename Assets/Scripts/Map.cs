using UnityEngine;

public class Map : MonoBehaviour
{
    [Header("Map Settings")]
    public Vector2 size = new (50f, 50f);

    private MapGenerator mapGenerator;
    private MapBorder mapBorder;

    void Awake()
    {
        mapGenerator = GetComponent<MapGenerator>();
        mapBorder    = GetComponent<MapBorder>();
    }

    public void Generate()
    {
        mapGenerator.GenerateMap(size);
        mapBorder.DrawBorder(size);
    }
}