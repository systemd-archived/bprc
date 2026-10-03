#if UNITY_EDITOR
using UnityEngine;

// Must be in the runtime assembly so Play Mode delivers real Unity collision callbacks.
public sealed class Box3DWheelAuditProbe : MonoBehaviour
{
    public Vector3 direction;
    public int breaks;
    public void OnJointBreak(float force) { breaks++; }
    public void OnPhysicsCollisionStay(PhysicsCollision collision)
    {
        if (collision.contacts.Length > 0)
            direction = (collision.contacts[0].point - transform.TransformPoint(new Vector3(0, -0.075f, 0))).normalized;
    }
    public void OnCollisionStay(Collision collision) { OnPhysicsCollisionStay(new PhysicsCollision(collision)); }
}
#endif
