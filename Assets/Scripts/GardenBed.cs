using UnityEngine;

public class GardenBed : MonoBehaviour, PlayerController.IInteractable
{
    public void Interact()
    {
        Debug.Log("Interaction happened with garden bed: " + gameObject.name);
    }
} 