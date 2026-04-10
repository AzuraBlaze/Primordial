using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed       = 3f;
    public float zoomSmoothSpeed = 8f;

    [Header("View Settings")]
    public float viewPadding = 5f;

    [Header("Follow Settings")]
    [Tooltip("How quickly the camera slides toward a followed creature.")]
    public float followSmoothSpeed = 6f;
    [Tooltip("Orthographic size used when locking onto a creature.")]
    public float followZoom = 8f;
    [Tooltip("How many screen pixels the mouse must drag before panning breaks the follow lock.")]
    public float panBreakThreshold = 12f;

    private Camera  cam;
    private Vector3 dragOrigin;
    private Vector3 dragOriginScreen;
    private bool    isDragging;
    private bool    panBrokeFollow;
    private float   targetZoom;
    private float   minZoom;
    private float   maxZoom;
    private Vector2 paddedHalfSize;

    private Creature followTarget;

    /* ======================================== Public API ======================================== */

    /// <summary>
    /// Begin following a creature. The camera zooms to followZoom and tracks
    /// the creature each frame until ReleaseFollow() is called or the player pans.
    /// </summary>
    public void BeginFollow(Creature creature)
    {
        followTarget = creature;
        targetZoom   = Mathf.Clamp(followZoom, minZoom, maxZoom);
    }

    /// <summary>Release the follow lock without changing zoom or position.</summary>
    public void ReleaseFollow()
    {
        followTarget = null;
    }

    /* ======================================== Unity ======================================== */

    void Awake()
    {
        cam        = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
    }

    void Update()
    {
        HandleFollow();
        HandleZoom();
        HandlePan();
    }

    /* ======================================== Follow ======================================== */

    void HandleFollow()
    {
        if (followTarget != null && followTarget.isDead)
            followTarget = null;

        if (followTarget == null) return;

        Vector3 goal = new (
            followTarget.transform.position.x,
            followTarget.transform.position.y,
            transform.position.z);

        transform.position = Vector3.Lerp(transform.position, goal, Time.deltaTime * followSmoothSpeed);
        ClampCameraPosition();
    }

    /* ======================================== Zoom ======================================== */

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0f)
            targetZoom -= scroll * zoomSpeed * targetZoom;

        targetZoom           = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomSmoothSpeed);

        ClampCameraPosition();
    }

    /* ======================================== Pan ======================================== */

    void HandlePan()
    {
        if (Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0)
        {
            dragOrigin       = cam.ScreenToWorldPoint(Input.mousePosition);
            dragOriginScreen = Input.mousePosition;
            isDragging       = true;
            panBrokeFollow   = false;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging     = false;
            panBrokeFollow = false;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            // Only begin panning once the mouse has moved beyond the threshold,
            // so that a plain click to select a creature does not break follow.
            float screenDelta = Vector3.Distance(Input.mousePosition, dragOriginScreen);
            if (screenDelta < panBreakThreshold) return;

            if (!panBrokeFollow)
            {
                panBrokeFollow = true;
                followTarget   = null;
                // Refresh dragOrigin to avoid a position jump when the threshold
                // is first crossed.
                dragOrigin = cam.ScreenToWorldPoint(dragOriginScreen);
            }

            Vector3 currentMouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 delta             = dragOrigin - currentMouseWorld;
            transform.position       += delta;
            ClampCameraPosition();
        }
    }

    /* ======================================== Helpers ======================================== */

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
                 paddedHalfSize.y - verticalExtent);

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