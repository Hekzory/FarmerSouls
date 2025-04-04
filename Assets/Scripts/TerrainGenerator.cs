using UnityEngine;
using System.Collections.Generic;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    [Tooltip("Width of the terrain.")]
    public int width = 256;

    [Tooltip("Length of the terrain.")]
    public int length = 256;

    [Tooltip("Scale of the Perlin noise. Smaller values produce larger features.")]
    public float scale = 20f;

    [Tooltip("Height multiplier. Controls the maximum height of the terrain.")]
    public float heightMultiplier = 20f;

    [Tooltip("Offset for the X-coordinate in the Perlin noise calculation.")]
    public float xOffset = 0f;

    [Tooltip("Offset for the Z-coordinate in the Perlin noise calculation.")]
    public float zOffset = 0f;

    [Tooltip("The material to apply to the generated terrain.")]
    public Material terrainMaterial;

    [Tooltip("Optional GameObject to parent the terrain to. If not assigned, it parents to this GameObject.")]
    public GameObject parentGameObject;

    [Tooltip("Layer to assign to the generated terrain, usually for camera collision.")]
    public LayerMask terrainLayer;

    [Header("Garden Bed Settings")]
    [Tooltip("Prefab of the garden bed to place on the terrain.")]
    public GameObject gardenBedPrefab;

    [Tooltip("Number of garden beds to generate.")]
    public int numberOfGardenBeds = 10;

    [Tooltip("Minimum distance between garden beds.")]
    public float minDistanceBetweenBeds = 10f;
    
    [Tooltip("Whether to align garden beds to the terrain slope.")]
    public bool alignToSlope = true;
    
    [Tooltip("Maximum slope angle (in degrees) where garden beds can be placed.")]
    [Range(0, 90)]
    public float maxSlopeAngle = 30f;

    // Cached terrain data
    private TerrainData terrainData;
    private GameObject terrainGameObject;
    private Terrain terrain;
    
    // List to store spawned garden beds
    private List<GameObject> spawnedGardenBeds = new List<GameObject>();

    void Start()
    {
        GenerateTerrain();
        if (gardenBedPrefab != null)
        {
            PlaceGardenBeds();
        }
        else
        {
            Debug.LogWarning("Garden bed prefab not assigned. No garden beds will be generated.");
        }
    }

    void GenerateTerrain()
    {
        // Create a new TerrainData object
        terrainData = new TerrainData();
        terrainData.heightmapResolution = width + 1;
        terrainData.size = new Vector3(width, heightMultiplier, length);

        // Apply Perlin noise to the terrain
        float[,] heights = new float[width + 1, length + 1];
        for (int z = 0; z <= length; z++)
        {
            for (int x = 0; x <= width; x++)
            {
                float xCoord = (float)x / width * scale + xOffset;
                float zCoord = (float)z / length * scale + zOffset;
                float sample = Mathf.PerlinNoise(xCoord, zCoord);
                heights[x, z] = sample;
            }
        }

        terrainData.SetHeights(0, 0, heights);

        // Create a new Terrain GameObject
        terrainGameObject = Terrain.CreateTerrainGameObject(terrainData);
        terrain = terrainGameObject.GetComponent<Terrain>();
        terrain.materialTemplate = terrainMaterial;

        // Set the terrain to the specified layer for camera collision
        if (terrainLayer != 0)
        {
            // Convert LayerMask to layer index
            int layerIndex = GetLayerFromMask(terrainLayer);
            if (layerIndex != -1)
            {
                terrainGameObject.layer = layerIndex;
            }
            else
            {
                Debug.LogWarning("Invalid layer mask specified. Using default layer.");
            }
        }

        // Parent the terrain to the specified GameObject or this one
        if (parentGameObject != null)
        {
            terrainGameObject.transform.parent = parentGameObject.transform;
        }
        else
        {
            terrainGameObject.transform.parent = transform;
        }

        // Optional: Center the terrain
        terrainGameObject.transform.position = new Vector3(-width / 2f, 0, -length / 2f);
    }
    
    // Helper method to get the first layer index from a layer mask
    private int GetLayerFromMask(LayerMask mask)
    {
        int bitmask = mask.value;
        
        // Find the index of the first layer that is included in the mask
        for (int i = 0; i < 32; i++)
        {
            if (((1 << i) & bitmask) != 0)
            {
                return i;
            }
        }
        
        return -1; // No layer found
    }
    
    void PlaceGardenBeds()
    {
        // Create a container for the garden beds
        GameObject gardenBedsContainer = new GameObject("Garden Beds");
        gardenBedsContainer.transform.parent = terrainGameObject.transform;
        
        int attempts = 0;
        int maxAttempts = numberOfGardenBeds * 10; // Limit attempts to avoid infinite loops
        
        while (spawnedGardenBeds.Count < numberOfGardenBeds && attempts < maxAttempts)
        {
            attempts++;
            
            // Get a random position within the terrain bounds
            float randomX = Random.Range(0, width);
            float randomZ = Random.Range(0, length);
            
            // Convert to world position
            Vector3 worldPos = terrainGameObject.transform.position + new Vector3(randomX, 0, randomZ);
            
            // Get the height at this position
            float terrainHeight = terrain.SampleHeight(worldPos);
            worldPos.y = terrainHeight;
            
            // Check if the position is valid
            if (IsValidPlacementPosition(worldPos))
            {
                // Instantiate the garden bed
                GameObject gardenBed = Instantiate(gardenBedPrefab, worldPos, Quaternion.identity, gardenBedsContainer.transform);
                
                // Apply random rotation around Y axis
                float randomYRotation = Random.Range(0, 360f);
                
                if (alignToSlope)
                {
                    // Get normal of the terrain at this point to align the garden bed to the slope
                    Vector3 terrainNormal = GetTerrainNormal(worldPos);
                    
                    // Create a rotation that aligns the up direction with the terrain normal
                    Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, terrainNormal);
                    
                    // Combine with the random Y rotation
                    gardenBed.transform.rotation = slopeRotation * Quaternion.Euler(0, randomYRotation, 0);
                }
                else
                {
                    // Just apply random Y rotation
                    gardenBed.transform.rotation = Quaternion.Euler(0, randomYRotation, 0);
                }
                
                spawnedGardenBeds.Add(gardenBed);
            }
        }
        
        if (attempts >= maxAttempts && spawnedGardenBeds.Count < numberOfGardenBeds)
        {
            Debug.LogWarning($"Could only place {spawnedGardenBeds.Count} garden beds after maximum attempts. Try decreasing minimum distance or maximum slope angle.");
        }
    }
    
    bool IsValidPlacementPosition(Vector3 position)
    {
        // Check if the position is too steep
        Vector3 normal = GetTerrainNormal(position);
        float slopeAngle = Vector3.Angle(normal, Vector3.up);
        
        if (slopeAngle > maxSlopeAngle)
        {
            return false;
        }
        
        // Check if the position is too close to other garden beds
        foreach (GameObject bed in spawnedGardenBeds)
        {
            if (Vector3.Distance(position, bed.transform.position) < minDistanceBetweenBeds)
            {
                return false;
            }
        }
        
        return true;
    }
    
    Vector3 GetTerrainNormal(Vector3 worldPos)
    {
        // Get the local position relative to the terrain
        Vector3 terrainLocalPos = worldPos - terrainGameObject.transform.position;
        
        // Calculate normalized position (0-1) on the terrain
        float normalizedX = Mathf.Clamp01(terrainLocalPos.x / terrainData.size.x);
        float normalizedZ = Mathf.Clamp01(terrainLocalPos.z / terrainData.size.z);
        
        // Get the normal at this position
        return terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
    }
    
    // Optional method to regenerate just the garden beds without recreating the terrain
    public void RegenerateGardenBeds()
    {
        ClearGardenBeds();
        PlaceGardenBeds();
    }
    
    // Optional method to clear all garden beds
    public void ClearGardenBeds()
    {
        foreach (GameObject bed in spawnedGardenBeds)
        {
            if (bed != null)
            {
                DestroyImmediate(bed);
            }
        }
        
        spawnedGardenBeds.Clear();
        
        // Also destroy the container if it exists
        GameObject container = terrainGameObject.transform.Find("Garden Beds")?.gameObject;
        if (container != null)
        {
            DestroyImmediate(container);
        }
    }
}