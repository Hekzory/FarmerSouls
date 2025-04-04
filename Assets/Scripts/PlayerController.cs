using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float jumpForce = 8.0f;
    [SerializeField] private float gravity = 20.0f;
    [SerializeField] private float rotationSpeed = 720f; // Smooth rotation speed

    [Header("Camera Settings")]
    [SerializeField] private float cameraSwitchCooldown = 0.5f;
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float thirdPersonOrbitDistance = 3.0f;
    [SerializeField] private float minYAngle = -80f;
    [SerializeField] private float maxYAngle = 80f;
    [SerializeField] private LayerMask cameraCollisionMask; // Layers the camera will collide with

    [Header("Visual Settings")]
    [SerializeField] private Material skyboxMaterial;
    [Tooltip("Assign the GameObject that holds the player's visible mesh renderers.")]
    [SerializeField] private GameObject playerVisuals;

    [Header("Input Settings")]
    [SerializeField] private KeyCode cameraSwitchKey = KeyCode.C;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    private CharacterController characterController;
    private Vector3 moveDirection = Vector3.zero;
    private Camera firstPersonCamera;
    private Camera thirdPersonCamera;
    private bool isFirstPerson = false;
    private float lastCameraSwitchTime = 0;
    private Renderer[] playerRenderers;
    private float currentCameraDistance;
    private Vector3 cameraVelocity = Vector3.zero;

    // Camera Rotation Variables
    private float rotationX = 0.0f; // Vertical rotation (around X axis)
    private float rotationY = 0.0f; // Horizontal rotation (around Y axis)

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("CharacterController not found! Attach one to the player.");
            enabled = false;
            return;
        }

        // Cache renderers for performance
        if (playerVisuals != null)
        {
            playerRenderers = playerVisuals.GetComponentsInChildren<Renderer>();
        }
        else
        {
            Debug.LogWarning("Player Visuals GameObject not assigned in PlayerController. Cannot toggle visibility.", this);
        }

        CreateCameras();
        SwitchCamera();
        currentCameraDistance = thirdPersonOrbitDistance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleCameraRotation();
        HandleMovement();
        HandleCameraSwitching();
    }

    void CreateCameras()
    {
        GameObject fpCamObj = new GameObject("FirstPersonCamera");
        firstPersonCamera = fpCamObj.AddComponent<Camera>();
        firstPersonCamera.transform.SetParent(transform);
        firstPersonCamera.transform.localPosition = new Vector3(0, 2f, 0);
        fpCamObj.AddComponent<AudioListener>();

        GameObject tpCamObj = new GameObject("ThirdPersonCamera");
        thirdPersonCamera = tpCamObj.AddComponent<Camera>();
        thirdPersonCamera.transform.SetParent(transform);
        thirdPersonCamera.transform.localPosition = new Vector3(0, 2f, -thirdPersonOrbitDistance);
        tpCamObj.AddComponent<AudioListener>();

        // Apply skybox if available
        if (skyboxMaterial != null)
        {
            ApplySkyboxToCamera(firstPersonCamera);
            ApplySkyboxToCamera(thirdPersonCamera);
        }
    }

    private void ApplySkyboxToCamera(Camera camera)
    {
        Skybox skybox = camera.gameObject.AddComponent<Skybox>();
        skybox.material = skyboxMaterial;
        camera.clearFlags = CameraClearFlags.Skybox;
    }

    void HandleCameraSwitching()
    {
        if (Input.GetKeyDown(cameraSwitchKey) && Time.time - lastCameraSwitchTime > cameraSwitchCooldown)
        {
            isFirstPerson = !isFirstPerson;
            SwitchCamera();

            if (isFirstPerson)
            {
                rotationX = 0;
                rotationY = transform.eulerAngles.y;
            }
            else
            {
                rotationX = 0;
            }
            lastCameraSwitchTime = Time.time;
        }
    }

    void SwitchCamera()
    {
        if (firstPersonCamera == null || thirdPersonCamera == null)
        {
            Debug.LogError("Camera references lost. Recreating cameras.");
            CreateCameras();
        }

        firstPersonCamera.enabled = isFirstPerson;
        thirdPersonCamera.enabled = !isFirstPerson;

        firstPersonCamera.GetComponent<AudioListener>().enabled = isFirstPerson;
        thirdPersonCamera.GetComponent<AudioListener>().enabled = !isFirstPerson;

        TogglePlayerVisibility();
    }

    private void TogglePlayerVisibility()
    {
        if (playerVisuals != null && playerRenderers != null)
        {
            // Use cached renderers
            foreach (Renderer rend in playerRenderers)
            {
                rend.enabled = !isFirstPerson;
            }
        }
        else if (playerVisuals != null)
        {
            // Fallback if renderers weren't cached for some reason
            Renderer[] renderers = playerVisuals.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                rend.enabled = !isFirstPerson;
            }
        }
        else
        {
            // Optional warning if not assigned
            Debug.LogWarning("Player Visuals GameObject not assigned in PlayerController. Cannot toggle visibility.", this);
        }
    }

    void HandleCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Apply vertical rotation with clamping
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, minYAngle, maxYAngle);

        // Accumulate horizontal rotation
        rotationY += mouseX;

        if (isFirstPerson)
        {
            // First person camera handling
            firstPersonCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation = Quaternion.Euler(0, rotationY, 0);
        }
        else
        {
            // Third person camera handling
            Quaternion rotation = Quaternion.Euler(rotationX, rotationY, 0);
            
            // Calculate ideal camera position
            Vector3 idealPosition = GetIdealCameraPosition(rotation);
            
            // Check for collisions and adjust position
            Vector3 adjustedPosition = HandleCameraCollision(idealPosition);
            
            thirdPersonCamera.transform.rotation = rotation;
            thirdPersonCamera.transform.position = adjustedPosition;
        }
    }
    
    private Vector3 GetIdealCameraPosition(Quaternion rotation)
    {
        Vector3 direction = new Vector3(0.0f, 0.0f, -thirdPersonOrbitDistance);
        return rotation * direction + transform.position + Vector3.up * 1.0f;
    }
    
    private Vector3 HandleCameraCollision(Vector3 idealPosition)
    {
        // Origin point for our raycast (player position + slight height offset)
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        
        // Direction and distance to the ideal camera position
        Vector3 directionToIdealPos = idealPosition - origin;
        float distanceToIdealPos = directionToIdealPos.magnitude;
        
        // Draw debug ray to visualize camera collision detection in Scene view
        Debug.DrawRay(origin, directionToIdealPos, Color.red);
        
        // Cast a ray to check for collisions
        RaycastHit hit;
        if (Physics.Raycast(origin, directionToIdealPos.normalized, out hit, thirdPersonOrbitDistance, cameraCollisionMask))
        {
            // Found a collision - use 90% of hit distance to avoid clipping (instant adjustment)
            float safeDistance = hit.distance * 0.9f;
            return origin + directionToIdealPos.normalized * safeDistance;
        }
        else
        {
            // No collision - use full distance (instant adjustment)
            return idealPosition;
        }
    }

    void HandleMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 inputDirection = new Vector3(horizontalInput, 0, verticalInput).normalized;

        if (characterController.isGrounded)
        {
            // Ground movement handling
            moveDirection = CalculateMovementDirection(horizontalInput, verticalInput);

            if (Input.GetButton("Jump") || Input.GetKey(jumpKey))
            {
                moveDirection.y = jumpForce;
            }
        }
        else
        {
            // Air movement handling (keeps Y velocity but updates XZ direction)
            Vector3 horizontalMovement = CalculateMovementDirection(horizontalInput, verticalInput);
            moveDirection.x = horizontalMovement.x;
            moveDirection.z = horizontalMovement.z;
        }

        // Handle rotation in third-person mode
        if (!isFirstPerson && inputDirection != Vector3.zero)
        {
            HandleThirdPersonRotation(inputDirection);
        }

        // Apply gravity and move character
        moveDirection.y -= gravity * Time.deltaTime;
        characterController.Move(moveDirection * Time.deltaTime);
    }

    private Vector3 CalculateMovementDirection(float horizontalInput, float verticalInput)
    {
        if (isFirstPerson)
        {
            Vector3 direction = transform.forward * verticalInput + transform.right * horizontalInput;
            return direction.normalized * moveSpeed;
        }
        else
        {
            Vector3 cameraForward = thirdPersonCamera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            Vector3 cameraRight = thirdPersonCamera.transform.right;
            cameraRight.y = 0;
            cameraRight.Normalize();

            Vector3 direction = cameraForward * verticalInput + cameraRight * horizontalInput;
            return direction.normalized * moveSpeed;
        }
    }

    private void HandleThirdPersonRotation(Vector3 inputDirection)
    {
        // Calculate target angle based on input relative to the camera
        Vector3 cameraForward = thirdPersonCamera.transform.forward;
        Vector3 cameraRight = thirdPersonCamera.transform.right;
        
        // Zero out Y components and normalize
        cameraForward.y = 0;
        cameraForward.Normalize();
        cameraRight.y = 0;
        cameraRight.Normalize();
        
        // Determine movement direction relative to camera
        Vector3 relativeInputDir = cameraForward * inputDirection.z + cameraRight * inputDirection.x;

        // Calculate target rotation angle
        float targetRotationY = Mathf.Atan2(relativeInputDir.x, relativeInputDir.z) * Mathf.Rad2Deg;

        // Smoothly interpolate current rotation towards target
        rotationY = Mathf.MoveTowardsAngle(rotationY, targetRotationY, rotationSpeed * Time.deltaTime);
        
        // Apply rotation to the player
        transform.rotation = Quaternion.Euler(0, rotationY, 0);
    }
}