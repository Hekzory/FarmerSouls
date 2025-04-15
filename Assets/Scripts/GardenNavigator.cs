using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))] // Ensures NavMeshAgent is present
public class GardenNavigator : MonoBehaviour
{
    [Tooltip("Tag used to identify destination points (the garden beds).")]
    public string destinationTag = "Interactable";

    [Tooltip("Time in seconds to wait at each destination.")]
    public float waitTime = 2.0f;

    private NavMeshAgent agent;
    private List<Transform> destinations = new List<Transform>();
    private int currentDestinationIndex = -1;
    private Coroutine movementCoroutine;
    private bool isInitialized = false;

    void Awake() // <--- Use Awake for GetComponent
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found on this GameObject.", this);
            this.enabled = false;
            return;
        }
    }

    void OnEnable()
    {
        // Subscribe to the event when the component becomes active
        TerrainGenerator.OnNavMeshReady += InitializeAgent;
        Debug.Log("Navigator subscribed to OnNavMeshReady.", this);
    }

    void OnDisable()
    {
        // Unsubscribe when the component becomes inactive or destroyed
        TerrainGenerator.OnNavMeshReady -= InitializeAgent;
        Debug.Log("Navigator unsubscribed from OnNavMeshReady.", this);

        // Optional: Stop the coroutine if the agent is disabled
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
        isInitialized = false; // Reset initialization flag if disabled
    }

    void InitializeAgent()
    {
        // Prevent running the initialization logic multiple times
        if (isInitialized) return;

        Debug.Log("OnNavMeshReady event received. Initializing agent...", this);

        if (agent == null) // Double check agent reference
        {
             Debug.LogError("Agent reference is null during InitializeAgent.", this);
             return;
        }
         if (!agent.isOnNavMesh)
        {
             // Agent might not be placed on NavMesh yet, try warping it?
             // Or maybe wait a frame? For now, log a warning.
             Debug.LogWarning("Agent is not on NavMesh when InitializeAgent is called. Waiting slightly might help, or check agent starting position.", this);
             // You could try warping it:
             // NavMeshHit hit;
             // if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas)) {
             //     agent.Warp(hit.position);
             // } else {
             //     Debug.LogError("Could not place agent on NavMesh even with SamplePosition.", this);
             //     return; // Cannot proceed if agent isn't on NavMesh
             // }
        }


        FindDestinations();

        if (destinations.Count > 0)
        {
            // Stop any previous movement coroutine just in case
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
            }
            movementCoroutine = StartCoroutine(PatrolRoutine());
            isInitialized = true; // Mark as initialized
            Debug.Log("Agent initialization complete. Starting patrol.", this);
        }
        else
        {
            Debug.LogWarning("No destinations found with tag: " + destinationTag + ". Agent will not move.", this);
            // Still mark as initialized to prevent trying again unless explicitly reset
            isInitialized = true;
        }
    }

    void FindDestinations()
    {
        destinations.Clear();
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(destinationTag);
        Debug.Log($"Found {taggedObjects.Length} GameObjects with tag '{destinationTag}'. Checking NavMesh proximity...");

        foreach (GameObject obj in taggedObjects)
        {
            NavMeshHit hit;
            // Check if the destination's position is near the NavMesh
            // Increase the search distance slightly if needed (e.g., 2.0f)
            if (NavMesh.SamplePosition(obj.transform.position, out hit, 2.0f, NavMesh.AllAreas))
            {
                destinations.Add(obj.transform);
                 Debug.Log($"Added valid destination: {obj.name} at {hit.position}");
            } else {
                 Debug.LogWarning($"Destination {obj.name} at {obj.transform.position} is too far from the baked NavMesh. Ignoring.", obj);
            }
        }

        Debug.Log($"Found {destinations.Count} valid destinations on the NavMesh.");
    }

    IEnumerator PatrolRoutine()
    {
        if (destinations.Count == 0) yield break; // Exit if no destinations

        while (true) // Loop forever
        {
            // Choose the next destination (simple sequential or random)
            currentDestinationIndex = (currentDestinationIndex + 1) % destinations.Count;
            // Or for random: currentDestinationIndex = Random.Range(0, destinations.Count);

            Transform targetDestination = destinations[currentDestinationIndex];
            Debug.Log($"Moving to destination: {targetDestination.name}");

            // Set the destination for the agent
            agent.SetDestination(targetDestination.position);

            // Wait until the agent reaches the destination (or gets close)
            // Check remainingDistance and pathStatus
            while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
            {
                // Check if the path became invalid while moving
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    Debug.LogWarning($"Path to {targetDestination.name} became invalid. Choosing next destination.", this);
                    // Break inner loop to pick a new destination in the outer loop
                    yield return new WaitForSeconds(0.5f); // Small delay before retrying
                    break;
                }
                 // Check if the destination became null (e.g., destroyed)
                if (targetDestination == null)
                {
                    Debug.LogWarning("Current destination was destroyed. Finding new destinations.", this);
                    FindDestinations(); // Re-scan for destinations
                    if (destinations.Count == 0) yield break; // Exit if none left
                    // Break inner loop to pick a new one
                     yield return new WaitForSeconds(0.1f);
                     break;
                }

                yield return null; // Wait for the next frame
            }

             // Small check to ensure we didn't break out due to invalid path/target
            if (agent.remainingDistance <= agent.stoppingDistance && agent.pathStatus != NavMeshPathStatus.PathInvalid && targetDestination != null)
            {
                Debug.Log($"Arrived at destination: {targetDestination.name}. Waiting for {waitTime} seconds.");
                yield return new WaitForSeconds(waitTime); // Wait at the destination
            } else {
                 // If we broke out early (invalid path/target), just continue the outer loop to pick next target immediately
                 Debug.Log("Did not properly arrive or wait, picking next target immediately.");
            }
        }
    }

    // Optional: Call this if destinations might change during gameplay
    public void UpdateDestinations()
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }
        FindDestinations();
        if (destinations.Count > 0)
        {
            // Restart the patrol, potentially starting from a new random/first point
            currentDestinationIndex = -1; // Reset index
            movementCoroutine = StartCoroutine(PatrolRoutine());
        } else {
             Debug.LogWarning("No destinations found after update. Agent stopping.", this);
             agent.ResetPath(); // Stop current movement
        }
    }
}