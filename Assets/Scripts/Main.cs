using UnityEngine;

public class Main : MonoBehaviour
{
    [Header("References")]
    public Map map;

    private CameraController cameraController;

    void Start()
    {
        cameraController = GetComponent<CameraController>();
        map.Generate();
        cameraController.SetMapBounds(map.size);
    }
}