using UnityEngine;

public class Main : MonoBehaviour
{
    [Header("References")]
    public Map             map;
    public FoodSpawner     foodSpawner;
    public CreatureManager creatureManager;
    public DayNightCycle   dayNightCycle;
    public TemperatureMap  temperatureMap;

    private CameraController cameraController;

    void Start()
    {
        cameraController = GetComponent<CameraController>();

        // 1. Generate the map first — everything else depends on it.
        map.Generate();

        // 2. Camera bounds.
        cameraController.SetMapBounds(map.size);

        // 3. Day/Night overlay bounds.
        if (dayNightCycle != null)
            dayNightCycle.SetMapBounds(map.size);

        // 4. Temperature map.
        if (temperatureMap != null)
            temperatureMap.Initialise(map.size);

        // 5. Food (reads BiomeMap for Bloom tiles).
        foodSpawner.Initialise(map.GetComponent<MapGenerator>(), map.size);

        // 6. Creature population.
        creatureManager.Initialise(map.size);
    }
}