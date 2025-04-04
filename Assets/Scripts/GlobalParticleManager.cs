using UnityEngine;
using Random = UnityEngine.Random; // Explicitly state usage of UnityEngine.Random

// Nox Was Here. Painting the sky with fire and cosmic debris. (Corrected)
[AddComponentMenu("Nox/Effects/Global Particle Manager")]
public class GlobalParticleManager : MonoBehaviour
{
    // == CONFIGURATION: Assign materials and tweak parameters in the Inspector ==
    [Header("Sky Fire Effect")]
    [SerializeField] private Material skyFireMaterial;
    [SerializeField] private float fireEmissionRate = 5f;
    [SerializeField] private float fireParticleSize = 1.5f;
    [SerializeField] private float fireParticleLifetime = 8.0f;
    [SerializeField] private float fireParticleSpeed = 0.5f;
    [SerializeField] private Gradient fireColorOverLifetime; // Use gradient editor for nice fades/changes
    [SerializeField] private float fireEmitterRadius = 50f; // Large area emitter
    [SerializeField] private float fireEmitterHeight = 75f; // Position high in the sky

    [Header("Meteorite Effect")]
    [SerializeField] private Material meteoriteMaterial;
    [SerializeField] [Tooltip("How many meteorites per burst.")]
    private int meteoriteBurstCount = 1;
    [SerializeField] [Tooltip("Time between meteorite bursts (seconds).")]
    private float meteoriteBurstInterval = 10.0f;
    [SerializeField] [Tooltip("Random variation added to burst interval (+/- this value).")]
    private float meteoriteIntervalRandomness = 5.0f;
    [SerializeField] private float meteoriteParticleSize = 0.3f;
    [SerializeField] private float meteoriteParticleLifetime = 5.0f;
    [SerializeField] private float meteoriteParticleSpeed = 40.0f; // Fast streaks
    [SerializeField] private Color meteoriteStartColor = Color.white;
    [SerializeField] private float meteoriteEmitterSize = 100f; // Spawn zone size
    [SerializeField] private float meteoriteEmitterHeight = 100f; // Start high
    [SerializeField] [Tooltip("Enable trails for meteorites? Requires appropriate material setup.")]
    private bool enableMeteoriteTrails = true;
    [SerializeField] [Range(0f, 1f)] private float trailRatio = 0.8f; // How much of the lifetime is trailed


    // == SYSTEM REFERENCES ==
    private ParticleSystem _skyFireSystem;
    private ParticleSystem _meteoriteSystem;
    private float _nextMeteoriteBurstTime = 0f;


    void Start()
    {
        // It's cleaner to put particle systems on child objects.
        InitializeSkyFire();
        InitializeMeteorites();
        if (_meteoriteSystem != null) // Only calculate if initialization was successful
        {
             CalculateNextMeteoriteBurstTime();
        }
    }

    void Update()
    {
        // Handle meteorite burst timing manually for randomness
        if (_meteoriteSystem != null && Time.time >= _nextMeteoriteBurstTime && meteoriteBurstCount > 0)
        {
            // Emit a burst
            _meteoriteSystem.Emit(meteoriteBurstCount);
            // Schedule the next burst
            CalculateNextMeteoriteBurstTime();
        }
    }

    void InitializeSkyFire()
    {
        if (skyFireMaterial == null)
        {
            Debug.LogError("[Nox] GlobalParticleManager: Sky Fire Material is NOT assigned. Cannot create sky fire.", this);
            return;
        }

        // Create a child object to hold the particle system
        GameObject fireEffectObject = new GameObject("SkyFireEffect");
        fireEffectObject.transform.SetParent(this.transform);
        fireEffectObject.transform.localPosition = Vector3.zero; // Position relative to manager

        _skyFireSystem = fireEffectObject.AddComponent<ParticleSystem>();
        // **FIX:** Get the renderer Unity automatically added, instead of trying to add another one.
        var renderer = fireEffectObject.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) // Safety check, though GetComponent should find it if AddComponent<ParticleSystem> succeeded
        {
             Debug.LogError("[Nox] Failed to get ParticleSystemRenderer for SkyFireEffect.", this);
             Destroy(fireEffectObject); // Clean up incomplete object
             return;
        }
        renderer.material = skyFireMaterial;
        // Optional: Adjust render settings like sort mode if needed (e.g., By Distance)

        var main = _skyFireSystem.main;
        main.loop = true;
        main.startLifetime = fireParticleLifetime;
        main.startSpeed = fireParticleSpeed;
        main.startSize = fireParticleSize;
        main.maxParticles = (int)(fireEmissionRate * fireParticleLifetime) + 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.01f; // Slight upward drift, looks like heat rising or embers

        var emission = _skyFireSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = fireEmissionRate;

        var shape = _skyFireSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere; // Emit from a large sphere volume
        shape.radius = fireEmitterRadius;
        shape.position = new Vector3(0, fireEmitterHeight, 0); // Center the emitter high up
        // Allow emission from the volume, not just the surface:
        shape.radiusThickness = 1f; // Emit from volume (0 = surface only, 1 = full volume)
        shape.randomDirectionAmount = 1f; // Emit in random directions within the sphere

        var col = _skyFireSystem.colorOverLifetime;
        col.enabled = true;
        col.color = fireColorOverLifetime; // Assign the gradient set in Inspector

        _skyFireSystem.Play();
    }

    void InitializeMeteorites()
    {
        if (meteoriteMaterial == null)
        {
            Debug.LogError("[Nox] GlobalParticleManager: Meteorite Material is NOT assigned. Cannot create meteorites.", this);
            return;
        }

         // Create a child object
        GameObject meteoriteEffectObject = new GameObject("MeteoriteEffect");
        meteoriteEffectObject.transform.SetParent(this.transform);
        meteoriteEffectObject.transform.localPosition = Vector3.zero;

        _meteoriteSystem = meteoriteEffectObject.AddComponent<ParticleSystem>();
        // **FIX:** Get the renderer Unity automatically added.
        var renderer = meteoriteEffectObject.GetComponent<ParticleSystemRenderer>();
         if (renderer == null)
        {
             Debug.LogError("[Nox] Failed to get ParticleSystemRenderer for MeteoriteEffect.", this);
             Destroy(meteoriteEffectObject);
             return;
        }
        renderer.material = meteoriteMaterial;
        renderer.trailMaterial = enableMeteoriteTrails ? meteoriteMaterial : null; // Use same material for trail or none

        var main = _meteoriteSystem.main;
        main.loop = false; // We trigger bursts manually
        main.startLifetime = meteoriteParticleLifetime;
        main.startSpeed = meteoriteParticleSpeed;
        main.startSize = meteoriteParticleSize;
        main.startColor = meteoriteStartColor;
        main.maxParticles = meteoriteBurstCount * 10; // Increased buffer for overlapping bursts
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f; // Pulled downwards by gravity

        var emission = _meteoriteSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0; // No continuous emission
        // Configure burst possibility (though we trigger manually via Emit)
        // Note: The Burst struct itself cannot be directly added/modified like this via script easily after initial setup.
        // Manual Emit() in Update is the reliable way here.

        var shape = _meteoriteSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere; // Spawn point within a large sphere high up
        shape.radius = meteoriteEmitterSize / 2f;
        shape.position = new Vector3(0, meteoriteEmitterHeight, 0); // Center spawn area
        // Make them shoot downwards predominantly
        shape.rotation = new Vector3(90, 0, 0); // Point emitter generally downwards
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random; // Pick random direction within the arc
        shape.arc = 30; // Cone angle for initial direction (degrees)
        shape.arcSpread = 0f; // No spread within the arc selection itself
        // Remove randomDirectionAmount if using Arc to specify direction
        // shape.randomDirectionAmount = 0.2f; // Less needed if using arc shape emission

        // Optional: Add trails if enabled
        if (enableMeteoriteTrails)
        {
            var trails = _meteoriteSystem.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = trailRatio;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.5f); // Trail fades relatively quickly
            trails.minVertexDistance = 0.2f;
            trails.worldSpace = true;
            // Configure other trail settings (color, width) as needed via the trail material or specific modules
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f * meteoriteParticleSize, 0f); // Tapering width relative to particle size
            trails.colorOverTrail = new Gradient(); // Simple white-to-transparent fade maybe
            // Setup a basic gradient for trail color (can be customized further)
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white * 0.8f, 0.0f), new GradientColorKey(Color.white * 0.5f, 1.0f) }, // Slightly dimmer trail
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            trails.colorOverTrail = grad;
        }

        // Don't call Play() here, bursts are manual via Emit() in Update()
    }

    void CalculateNextMeteoriteBurstTime()
    {
        float randomOffset = Random.Range(-meteoriteIntervalRandomness, meteoriteIntervalRandomness);
        _nextMeteoriteBurstTime = Time.time + meteoriteBurstInterval + randomOffset;
        // Ensure interval is not negative or too small if randomness is large
        if (_nextMeteoriteBurstTime < Time.time + 0.2f) // Ensure a tiny delay at least
        {
             _nextMeteoriteBurstTime = Time.time + 0.2f;
        }
    }

    // Optional: Visualize the emitter zones in the editor
    void OnDrawGizmosSelected()
    {
        // Visualize Sky Fire Emitter
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Semi-transparent orange
        Vector3 fireCenter = transform.position + new Vector3(0, fireEmitterHeight, 0);
        Gizmos.DrawWireSphere(fireCenter, fireEmitterRadius);

        // Visualize Meteorite Spawn Zone
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // Semi-transparent yellow
        Vector3 meteoriteCenter = transform.position + new Vector3(0, meteoriteEmitterHeight, 0);
        Gizmos.DrawWireSphere(meteoriteCenter, meteoriteEmitterSize / 2f);
         // Indicate general downward direction (approximating the emission cone)
        Quaternion downRotation = Quaternion.Euler(90, 0, 0);
        Vector3 direction = transform.rotation * downRotation * Vector3.forward; // Base direction
        Gizmos.DrawRay(meteoriteCenter, direction.normalized * 15f); // Show the main downward axis

        Gizmos.color = Color.white; // Reset color
    }
}