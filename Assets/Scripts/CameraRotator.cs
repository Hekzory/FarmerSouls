using UnityEngine;

public class Rotator : MonoBehaviour
{
    [Tooltip("The target object that the camera will spectate.")]
    public Transform targetObject; // Assign the object to follow here

    public float radius = 5f, rotationSpeed = 30f, angleChangeSpeed = 20f, zoomSpeed = 1f;

    [Tooltip("The camera that should always look at the center point.")]
    public Camera mainCamera; // Assign your main camera here

    private float _currentAngle = 0f, _currentElevationAngle = 0f;

    void Start()
    {
        mainCamera = mainCamera == null ? Camera.main : mainCamera;
        if (!mainCamera) Debug.LogError("Rotator: No camera!");
        if (!targetObject) Debug.LogError("Rotator: No target!");
    }

    void Update()
    {
        if (!targetObject || !mainCamera) return;
        HandleInput();
        UpdatePositionAndRotation();
    }

    void HandleInput()
    {
        _currentAngle += Input.GetAxis("Horizontal") * rotationSpeed * Time.deltaTime;
        _currentElevationAngle = Mathf.Clamp(_currentElevationAngle + Input.GetAxis("Vertical") * angleChangeSpeed * Time.deltaTime, -89f, 89f);
        radius = Mathf.Clamp(radius - Input.mouseScrollDelta.y * zoomSpeed, 2f, 20f);
    }

    void UpdatePositionAndRotation()
    {
        float angleRad = _currentAngle * Mathf.Deg2Rad;
        float elevationRad = _currentElevationAngle * Mathf.Deg2Rad;

        float cosElevation = Mathf.Cos(elevationRad);
        float x = targetObject.position.x + radius * Mathf.Cos(angleRad) * cosElevation;
        float y = targetObject.position.y + radius * Mathf.Sin(elevationRad);
        float z = targetObject.position.z + radius * Mathf.Sin(angleRad) * cosElevation;

        transform.position = new Vector3(x, y, z);
        transform.LookAt(targetObject);
    }
}