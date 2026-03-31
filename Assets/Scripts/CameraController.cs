using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed = 3f;
    public float zoomSmoothSpeed = 8f;

    [Header("View Settings")]
    public float viewPadding = 5f;

    private Camera cam;
    private Vector3 dragOrigin;
    private bool isDragging;
    private float targetZoom;
    private float minZoom;
    private float maxZoom;
    private Vector2 paddedHalfSize;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0f)
            targetZoom -= scroll * zoomSpeed * targetZoom;

        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomSmoothSpeed);

        ClampCameraPosition();
    }

    void HandlePan()
    {
        if (Input.GetMouseButtonDown(0))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
        }

        if (Input.GetMouseButtonUp(0))
            isDragging = false;

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector3 currentMouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 delta = dragOrigin - currentMouseWorld;
            transform.position += delta;
            ClampCameraPosition();
        }
    }

    void ClampCameraPosition()
    {
        float verticalExtent   = cam.orthographicSize;
        float horizontalExtent = cam.orthographicSize * cam.aspect;

        float clampedX = (horizontalExtent > paddedHalfSize.x)
            ? 0f
            : Mathf.Clamp(
                transform.position.x,
                -paddedHalfSize.x + horizontalExtent,
                paddedHalfSize.x - horizontalExtent);
        
        float clampedY = (verticalExtent > paddedHalfSize.y)
            ? 0f
            : Mathf.Clamp(
                transform.position.y,
                -paddedHalfSize.y + verticalExtent,
                paddedHalfSize.y - verticalExtent
            );

        transform.position = new (clampedX, clampedY, transform.position.z);
    }

    public void SetMapBounds(Vector2 size)
    {
        paddedHalfSize = size / 2f + Vector2.one * viewPadding;
        minZoom        = Mathf.Min(size.x, size.y) / 10f;
        maxZoom        = Mathf.Max(size.x, size.y) / 2f + viewPadding;
        targetZoom     = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        ClampCameraPosition();
    }
}