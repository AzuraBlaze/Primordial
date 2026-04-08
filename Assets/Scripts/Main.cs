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

        // Generate the map first (everything else depends on it)
        map.Generate();

        // Camera bounds
        cameraController.SetMapBounds(map.size);

        // Day/Night overlay bounds
        if (dayNightCycle != null)
            dayNightCycle.SetMapBounds(map.size);

        // Temperature map
        if (temperatureMap != null)
            temperatureMap.Initialise(map.size);

        // Food
        foodSpawner.Initialise(map.GetComponent<MapGenerator>(), map.size);

        // Creature population
        creatureManager.Initialise(map.size);
    }
}