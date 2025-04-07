using UnityEngine;
using System.Collections;

public class SpawnPointGenerator : MonoBehaviour
{
    [Header("Spawn Area")]
    [Tooltip("The minimum X coordinate for the spawn point.")]
    public float minX = -10f;

    [Tooltip("The maximum X coordinate for the spawn point.")]
    public float maxX = 10f;

    [Tooltip("The minimum Z coordinate for the spawn point.")]
    public float minZ = -10f;

    [Tooltip("The maximum Z coordinate for the spawn point.")]
    public float maxZ = 10f;

    [Header("Character Setup")]
    [Tooltip("The existing character object in the scene.")]
    public GameObject character;

    [Tooltip("The offset above the ground to spawn the character at. Prevents clipping.")]
    public float spawnHeightOffset = 1f;
    
    [Header("Spawn Options")]
    [Tooltip("Maximum attempts to find a valid spawn point")]
    public int maxSpawnAttempts = 10;
    
    [Tooltip("Wait for terrain to be generated before spawning")]
    public bool waitForTerrain = true;
    
    [Tooltip("How long to wait (in seconds) for terrain to be generated")]
    public float terrainWaitTime = 0.5f;
    
    [Header("Collision Options")]
    [Tooltip("Should check for collisions when spawning")]
    public bool checkCollisions = true;
    
    [Tooltip("Radius to check for collisions")]
    public float collisionCheckRadius = 0.5f;
    
    [Tooltip("Layers to check for collisions")]
    public LayerMask collisionMask = -1; // Default to "Everything"
    
    [Tooltip("Force spawn even if all positions have collisions")]
    public bool forceSpawnIfNoValidPosition = true;

    // The calculated spawn position. Public for your amusement.
    public Vector3 spawnPosition { get; private set; }

    private Terrain terrain;

    void Awake()
    {
        // Validate spawn boundaries
        if (minX >= maxX || minZ >= maxZ)
        {
            Debug.LogError("Invalid spawn boundaries! Make sure minX < maxX and minZ < maxZ");
        }
    }

    void Start()
    {
        if (waitForTerrain)
        {
            StartCoroutine(WaitForTerrainAndSpawn());
        }
        else
        {
            FindTerrain();
            GenerateSpawnPoint();
            SpawnCharacter();
        }
    }
    
    private IEnumerator WaitForTerrainAndSpawn()
    {
        // Wait a frame to allow other Awake/Start methods to run
        yield return null;
        
        // Try to find the terrain
        FindTerrain();
        
        // If still no terrain, wait a bit longer
        if (terrain == null)
        {
            float elapsedTime = 0f;
            while (terrain == null && elapsedTime < terrainWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;
                FindTerrain();
            }
            
            if (terrain == null)
            {
                Debug.LogWarning("Could not find terrain after waiting " + terrainWaitTime + " seconds. Spawning at default height.");
            }
        }
        
        GenerateSpawnPoint();
        SpawnCharacter();
    }
    
    private bool FindTerrain()
    {
        terrain = Terrain.activeTerrain;
        
        if (terrain == null)
        {
            // Try to find terrain by tag or other means
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains.Length > 0)
            {
                terrain = terrains[0];
                Debug.Log("Found terrain through FindObjectsByType");
            }
        }
        
        return terrain != null;
    }

    /// <summary>
    /// Generates a new spawn point and respawns the character.
    /// </summary>
    /// <returns>The new spawn position</returns>
    public Vector3 RegenerateSpawnPoint()
    {
        FindTerrain();
        GenerateSpawnPoint();
        SpawnCharacter();
        return spawnPosition;
    }

    void GenerateSpawnPoint()
    {
        // Try to find terrain again if we don't have it yet
        if (terrain == null)
        {
            FindTerrain();
        }
        
        Vector3 bestPosition = Vector3.zero;
        bool foundValidPosition = false;
        
        // Attempt to find a valid spawn point
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Randomly generate coordinates within defined boundaries.
            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);
            float y = GetTerrainHeight(x, z);
            
            Vector3 testPosition = new Vector3(x, y, z);
            
            // Save the first position as a fallback
            if (attempt == 0 || bestPosition == Vector3.zero)
            {
                bestPosition = testPosition;
            }
            
            // Skip collision check if disabled
            if (!checkCollisions)
            {
                spawnPosition = testPosition;
                return;
            }
            
            // Check if the position is clear
            Vector3 checkPosition = testPosition + Vector3.up * spawnHeightOffset;
            if (!Physics.CheckSphere(checkPosition, collisionCheckRadius, collisionMask))
            {
                spawnPosition = testPosition;
                foundValidPosition = true;
                return;
            }
            
            // Debug visualization
            Debug.DrawLine(checkPosition, checkPosition + Vector3.up * 2, Color.red, 5f);
        }
        
        // If we couldn't find a clear spot
        if (!foundValidPosition)
        {
            if (forceSpawnIfNoValidPosition)
            {
                spawnPosition = bestPosition;
                Debug.LogWarning("Could not find a clear spawn position after " + maxSpawnAttempts + 
                    " attempts. Forcing spawn at position: " + spawnPosition);
            }
            else
            {
                // Generate a completely random position as last resort
                float fallbackX = Random.Range(minX, maxX);
                float fallbackZ = Random.Range(minZ, maxZ);
                float fallbackY = GetTerrainHeight(fallbackX, fallbackZ);
                spawnPosition = new Vector3(fallbackX, fallbackY, fallbackZ);
                Debug.LogWarning("Could not find a clear spawn position after " + maxSpawnAttempts + 
                    " attempts. Using random position: " + spawnPosition);
            }
        }
    }

    float GetTerrainHeight(float x, float z)
    {
        if (terrain != null)
        {
            //Get the world position.
            Vector3 terrainPosition = terrain.transform.position;
            //Normalize by subtracting position.
            float normalizedX = (x - terrainPosition.x) / terrain.terrainData.size.x;
            float normalizedZ = (z - terrainPosition.z) / terrain.terrainData.size.z;

            // Clamp values.
            normalizedX = Mathf.Clamp01(normalizedX);
            normalizedZ = Mathf.Clamp01(normalizedZ);

            // Sample the heightmap
            float height = terrain.terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
            return height + terrainPosition.y;
        }
        return 0f; // Return 0 if no terrain exists.
    }

    void SpawnCharacter()
    {
        if (character != null)
        {
            // Set the character's position to the calculated spawn point.
            Vector3 adjustedSpawnPosition = spawnPosition + Vector3.up * spawnHeightOffset;
            character.transform.position = adjustedSpawnPosition;
        }
        else
        {
            Debug.LogError("Character object not assigned in the inspector!");
        }
    }

    //Optional for debugging
    void OnDrawGizmos()
    {
        // Only draw if we have a valid spawn position
        if (spawnPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnPosition, 0.5f);
            
            // Draw the spawn area boundaries
            Gizmos.color = Color.green;
            Vector3 center = new Vector3((minX + maxX) * 0.5f, 0, (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
            Gizmos.DrawWireCube(center, size);
        }
    }
}