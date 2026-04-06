using UnityEngine;

public class Main : MonoBehaviour
{
    [Header("References")]
    public Map            map;
    public FoodSpawner    foodSpawner;
    public CreatureManager creatureManager;

    private CameraController cameraController;

    void Start()
    {
        cameraController = GetComponent<CameraController>();

        // 1. Generate the map first — FoodSpawner needs BiomeMap to be ready.
        map.Generate();

        // 2. Set camera bounds.
        cameraController.SetMapBounds(map.size);

        // 3. Initialise food (reads BiomeMap to find Bloom tiles).
        foodSpawner.Initialise(map.GetComponent<MapGenerator>(), map.size);

        // 4. Spawn the starting creature population.
        creatureManager.Initialise(map.size);
    }
}