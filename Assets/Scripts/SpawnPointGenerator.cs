using UnityEngine;

public class SpawnPointGenerator : MonoBehaviour
{
    [Tooltip("The minimum X coordinate for the spawn point.")]
    public float minX = -10f;

    [Tooltip("The maximum X coordinate for the spawn point.")]
    public float maxX = 10f;

    [Tooltip("The minimum Z coordinate for the spawn point.")]
    public float minZ = -10f;

    [Tooltip("The maximum Z coordinate for the spawn point.")]
    public float maxZ = 10f;

    [Tooltip("The existing character object in the scene.")]
    public GameObject character;

    [Tooltip("The offset above the ground to spawn the character at. Prevents clipping.")]
    public float spawnHeightOffset = 1f;

    // The calculated spawn position. Public for your amusement.
    public Vector3 spawnPosition { get; private set; }

    void Start()
    {
        GenerateSpawnPoint();
        SpawnCharacter();
    }

    void GenerateSpawnPoint()
    {
        // Randomly generate coordinates within defined boundaries.
        float x = Random.Range(minX, maxX);
        float z = Random.Range(minZ, maxZ);
        float y = GetTerrainHeight(x, z);
        spawnPosition = new Vector3(x, y, z);
    }

    float GetTerrainHeight(float x, float z)
    {
        Terrain terrain = Terrain.activeTerrain;
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
            return height;
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spawnPosition, 0.5f);
    }
}