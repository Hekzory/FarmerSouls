using UnityEngine;

// Nox Was Here. Architecting atmospheric grit for your digital soil. (Revised)
[AddComponentMenu("Nox/Effects/Tilage Dirt Particles")]
public class TilageDirtParticles : MonoBehaviour
{
    // == CONFIGURATION: Tweak these in the Inspector ==
    [Header("Core Settings")]
    [Tooltip("Assign a simple particle material. A soft dot or smoke texture works well. Use Particles/Standard Unlit shader perhaps.")]
    [SerializeField] private Material particleMaterial;

    [Tooltip("How many dirt specks appear per second?")]
    [SerializeField] private float emissionRate = 10f;

    [Tooltip("Base size of the dirt particles.")]
    [SerializeField] private float particleSize = 0.05f;

    [Tooltip("How long, in seconds, each particle lives.")]
    [SerializeField] private float particleLifetime = 2.0f;

    [Tooltip("Maximum speed particles will drift at.")]
    [SerializeField] private float particleSpeed = 0.1f;

    [Tooltip("Color of the dirt particles. Earthy tones recommended.")]
    [SerializeField] private Color particleColor = new Color(0.4f, 0.3f, 0.2f, 0.5f); // Default: Semi-transparent brown

    [Header("Shape & Placement")]
    [Tooltip("How far out from the base the particles should spawn.")]
    [SerializeField] private float emissionRadius = 0.6f; // Slightly larger than typical tile half-width

    [Tooltip("How high above the pivot point the emission shape is centered.")]
    [SerializeField] private float emissionHeightOffset = 0.05f; // Just above ground level

    [Tooltip("Thickness of the emission shape ring/disk. 0=edge, 1=filled.")]
    [SerializeField] [Range(0f, 1f)] private float shapeThickness = 0.1f; // Use 0 for a flat circle edge, >0 towards 1 for filled disk


    // == SYSTEM REFERENCE ==
    private ParticleSystem _particleSystem;
    private bool _isInitialized = false;

    void Start()
    {
        InitializeParticleSystem();
    }

    // Optional: If you need to dynamically turn effects on/off
    // void OnEnable()
    // {
    //     if (_isInitialized && _particleSystem != null && !_particleSystem.isPlaying)
    //     {
    //         _particleSystem.Play();
    //     }
    // }

    // void OnDisable()
    // {
    //     if (_isInitialized && _particleSystem != null && _particleSystem.isPlaying)
    //     {
    //         _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    //     }
    // }

    void InitializeParticleSystem()
    {
        if (_isInitialized) return;

        // Ensure we have a material.
        if (particleMaterial == null)
        {
            Debug.LogError($"[Nox] TilageDirtParticles on {gameObject.name}: Particle Material is NOT assigned. Dirt remains unseen.", this);
            return;
        }

        // Attempt to find an existing ParticleSystem. If not found, create one.
        if (!TryGetComponent<ParticleSystem>(out _particleSystem))
        {
            _particleSystem = gameObject.AddComponent<ParticleSystem>();
        }

        // --- Configure the Particle System ---
        var main = _particleSystem.main; // Get the main module
        main.loop = true;
        main.startLifetime = particleLifetime;
        main.startSpeed = particleSpeed;
        main.startSize = particleSize;
        main.startColor = particleColor;
        main.maxParticles = (int)(emissionRate * particleLifetime) + 50; // Estimate needed capacity
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Particles stay where they spawn
        main.gravityModifier = 0.02f; // Slight pull downwards, or keep 0 for floating dust

        // --- Configure Emission ---
        var emission = _particleSystem.emission; // Get emission module
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        // --- Configure Shape ---
        var shape = _particleSystem.shape; // Get shape module
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle; // Emit from a circle around the base
        shape.radius = emissionRadius;
        shape.radiusThickness = shapeThickness; // Controls emission from edge (0) to area (1)
        shape.position = new Vector3(0, emissionHeightOffset, 0); // Position emitter slightly above the pivot
        shape.rotation = new Vector3(-90, 0, 0); // Orient circle to be flat on XZ plane
        shape.alignToDirection = false;
        //shape.randomDirectionAmount = 1f; // Keep this commented unless you want chaotic directions instead of normal emission

        // --- Configure Renderer ---
        // Use GetComponent instead of AddComponent if Renderer might already exist from manual setup
        var renderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) { renderer = gameObject.AddComponent<ParticleSystemRenderer>(); } // Ensure it exists
        renderer.material = particleMaterial;

        // --- Final Steps ---
        _particleSystem.Play(); // Start the effect
        _isInitialized = true; // Mark as initialized
    }

    // Optional: Visualize the emission radius in the editor
    void OnDrawGizmosSelected()
    {
        if (!enabled) return;

        Gizmos.color = new Color(particleColor.r, particleColor.g, particleColor.b, 0.5f);
        Vector3 center = transform.position + transform.rotation * new Vector3(0, emissionHeightOffset, 0); // Account for object rotation
        Vector3 normal = transform.up; // Use object's up direction for orientation

        // Draw the outer circle (represents radius)
        DrawGizmoCircle(center, normal, emissionRadius);

        // Visualize thickness: Draw inner circle if thickness < 1 and > 0
        if (shapeThickness > 0 && shapeThickness < 1)
        {
            // Radius thickness is interpolated between edge (0) and center (1).
            // A value of 0.1 means emission happens between radius*0.9 and radius*1.0.
            float innerRadius = emissionRadius * (1 - shapeThickness);
            if(innerRadius > 0.01f) // Avoid drawing tiny/zero radius circle
            {
                 DrawGizmoCircle(center, normal, innerRadius);
            }
        }
        // If thickness is 0, only outer circle matters (edge emission).
        // If thickness is 1, emission happens across the whole area up to the outer circle.

        Gizmos.color = Color.white; // Reset color
    }

    // Helper to draw a circle Gizmo aligned to a normal vector
    private void DrawGizmoCircle(Vector3 center, Vector3 normal, float radius)
    {
        if (radius <= 0) return;
        // Ensure the normal is normalized for accurate rotation
        normal.Normalize();
        // Create a rotation that aligns the Z axis (forward) with the normal
        Quaternion rotation = Quaternion.LookRotation(normal) * Quaternion.Euler(90, 0, 0); // Adjust to align circle plane

        int segments = 32;
        Vector3 startPoint = center + rotation * Vector3.right * radius; // Start point on the circle
        Vector3 lastPoint = startPoint;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * 2f * Mathf.PI; // Angle in radians
            // Calculate point on XY plane relative to center, then rotate
            Vector3 nextPointRelative = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            Vector3 nextPoint = center + rotation * nextPointRelative;
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}