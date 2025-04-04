using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float jumpForce = 8.0f;
    public float gravity = 20.0f;
    public float rotationSpeed = 720f; // Smooth rotation speed

    [Header("Camera Settings")]
    public float cameraSwitchCooldown = 0.5f;
    public float mouseSensitivity = 2.0f;
    public float thirdPersonOrbitDistance = 3.0f;
    public float minYAngle = -80f;
    public float maxYAngle = 80f;

    [Header("Visual Settings")]
    public Material skyboxMaterial;
    [Tooltip("Assign the GameObject that holds the player's visible mesh renderers.")]
    public GameObject playerVisuals;

    private CharacterController characterController;
    private Vector3 moveDirection = Vector3.zero;
    private Camera firstPersonCamera;
    private Camera thirdPersonCamera;
    private bool isFirstPerson = false;
    private float lastCameraSwitchTime = 0;

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

        CreateCameras();
        SwitchCamera();

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
        if (skyboxMaterial != null)
        {
            Skybox fpSkybox = fpCamObj.AddComponent<Skybox>();
            fpSkybox.material = skyboxMaterial;
            firstPersonCamera.clearFlags = CameraClearFlags.Skybox;
        }
        firstPersonCamera.transform.SetParent(transform);
        firstPersonCamera.transform.localPosition = new Vector3(0, 2f, 0);
        fpCamObj.AddComponent<AudioListener>();

        GameObject tpCamObj = new GameObject("ThirdPersonCamera");
        thirdPersonCamera = tpCamObj.AddComponent<Camera>();
        if (skyboxMaterial != null)
        {
            Skybox tpSkybox = tpCamObj.AddComponent<Skybox>();
            tpSkybox.material = skyboxMaterial;
            thirdPersonCamera.clearFlags = CameraClearFlags.Skybox; // Ensure clear flags are set
        }
        thirdPersonCamera.transform.SetParent(transform);
        thirdPersonCamera.transform.localPosition = new Vector3(0, 2f, -thirdPersonOrbitDistance);
        tpCamObj.AddComponent<AudioListener>();
    }

    void HandleCameraSwitching()
    {
        if (Input.GetKeyDown(KeyCode.C) && Time.time - lastCameraSwitchTime > cameraSwitchCooldown)
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
        firstPersonCamera.enabled = isFirstPerson;
        thirdPersonCamera.enabled = !isFirstPerson;

        firstPersonCamera.GetComponent<AudioListener>().enabled = isFirstPerson;
        thirdPersonCamera.GetComponent<AudioListener>().enabled = !isFirstPerson;

        if (playerVisuals != null)
        {
            // Disable visuals in first person, enable in third person
            //playerVisuals.SetActive(!isFirstPerson);

            // Alternative: If you only want to disable Renderers, not the whole GameObject:
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

        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, minYAngle, maxYAngle);

        if (isFirstPerson)
        {
            rotationY += mouseX; //Accumulate in first person
            firstPersonCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation = Quaternion.Euler(0, rotationY, 0);
        }
        else
        {
            // Accumulate rotationY *even* in third-person.
            rotationY += mouseX;

            Quaternion rotation = Quaternion.Euler(rotationX, rotationY, 0);
            Vector3 negDistance = new Vector3(0.0f, 0.0f, -thirdPersonOrbitDistance);
            Vector3 position = rotation * negDistance + transform.position + Vector3.up * 1.0f;

            thirdPersonCamera.transform.rotation = rotation;
            thirdPersonCamera.transform.position = position;
        }
    }

    void HandleMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 inputDirection = new Vector3(horizontalInput, 0, verticalInput).normalized; // Get normalized input

        if (characterController.isGrounded)
        {
            if (isFirstPerson)
            {
                moveDirection = transform.forward * verticalInput + transform.right * horizontalInput;
                moveDirection = moveDirection.normalized * moveSpeed;
            }
            else
            {
                Vector3 cameraForward = thirdPersonCamera.transform.forward;
                cameraForward.y = 0;
                cameraForward.Normalize();

                Vector3 cameraRight = thirdPersonCamera.transform.right;
                cameraRight.y = 0;
                cameraRight.Normalize();

                moveDirection = cameraForward * verticalInput + cameraRight * horizontalInput;
                moveDirection = moveDirection.normalized * moveSpeed;
            }

            if (Input.GetButton("Jump"))
            {
                moveDirection.y = jumpForce;
            }
        }
        else
        {
            if (!isFirstPerson)
            {
                Vector3 cameraForward = thirdPersonCamera.transform.forward;
                cameraForward.y = 0;
                cameraForward.Normalize();

                Vector3 cameraRight = thirdPersonCamera.transform.right;
                cameraRight.y = 0;
                cameraRight.Normalize();

                Vector3 horizontalMovement = cameraForward * verticalInput + cameraRight * horizontalInput;
                horizontalMovement = horizontalMovement.normalized * moveSpeed;

                moveDirection.x = horizontalMovement.x;
                moveDirection.z = horizontalMovement.z;
            }
            else
            {
                Vector3 horizontalMovement = transform.forward * verticalInput + transform.right * horizontalInput;
                horizontalMovement = horizontalMovement.normalized * moveSpeed;
                moveDirection.x = horizontalMovement.x;
                moveDirection.z = horizontalMovement.z;
            }
        }

        // --- Rotation Logic (Unified) ---

        if (!isFirstPerson) // Apply only to third-person
        {
            if (inputDirection != Vector3.zero) // Only rotate if there's input
            {
                // 1. Calculate target angle based on input *relative to the camera*.
                Vector3 cameraForward = thirdPersonCamera.transform.forward;
                cameraForward.y = 0;
                cameraForward.Normalize();

                Vector3 cameraRight = thirdPersonCamera.transform.right;
                cameraRight.y = 0;
                cameraRight.Normalize();
                Vector3 relativeInputDir = cameraForward * inputDirection.z + cameraRight * inputDirection.x;

                // 2. Get the target Y rotation (angle)
                float targetRotationY = Mathf.Atan2(relativeInputDir.x, relativeInputDir.z) * Mathf.Rad2Deg;


                // 3. Smoothly interpolate the *current* rotationY towards the target.
                rotationY = Mathf.MoveTowardsAngle(rotationY, targetRotationY, rotationSpeed * Time.deltaTime);
            }
                // 4. Apply the accumulated rotationY to the player.
                 transform.rotation = Quaternion.Euler(0, rotationY, 0);
        }

        moveDirection.y -= gravity * Time.deltaTime;
        characterController.Move(moveDirection * Time.deltaTime);
    }
}