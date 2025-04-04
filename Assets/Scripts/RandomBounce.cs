using UnityEngine;

public class RandomBounce : MonoBehaviour
{
    [Tooltip("Minimum upward force applied on collision.")]
    public float minBounceForce = 5f;
    [Tooltip("Maximum upward force applied on collision.")]
    public float maxBounceForce = 15f;
    [Tooltip("Only bounce if the collision's relative vertical speed exceeds this.")]
    public float minImpactSpeed = 0.1f;
    [Tooltip("Objects with these tags will trigger a bounce.  Leave empty to bounce on everything.")]
    public string[] bounceTags = new string[0];
    [Tooltip("Apply force relative to the object's rotation (true) or the world (false).")]
    public bool relativeForce = false;
    private Rigidbody _rb;
    private bool _canBounce = true;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (!_rb)
        {
            Debug.LogError("RandomBounce: Requires a Rigidbody component! Disabling to prevent errors.");
            enabled = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_canBounce)
        {
            _canBounce = true;
            return;
        }

        if (bounceTags.Length > 0 && !System.Array.Exists(bounceTags, tag => tag == collision.gameObject.tag))
            return;

        if (Vector3.Dot(collision.relativeVelocity, collision.contacts[0].normal) < minImpactSpeed)
            return;

        float bounceForce = Random.Range(minBounceForce, maxBounceForce);

        // Simplified force calculation: ONLY upward force.
        Vector3 force = relativeForce ? transform.up * bounceForce : Vector3.up * bounceForce;

        _rb.AddForce(force, ForceMode.Impulse);
    }

     public void SetBounciness(bool bounciness)
    {
        _canBounce = bounciness;
    }
}