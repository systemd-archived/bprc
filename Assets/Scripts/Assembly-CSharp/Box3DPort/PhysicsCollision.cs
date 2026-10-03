using System;
using UnityEngine;

// Shared event payload: Unity invokes the original callbacks; Box3D invokes their adapters.
public sealed class PhysicsCollision
{
    public Collider collider;
    public Rigidbody rigidbody => collider ? collider.attachedRigidbody : null;
    public Transform transform => rigidbody ? rigidbody.transform : collider.transform;
    public GameObject gameObject => transform.gameObject;
    public Vector3 relativeVelocity;
    public Vector3 impulse;
    public PhysicsContactPoint[] contacts = Array.Empty<PhysicsContactPoint>();
    public int contactCount => contacts.Length;
    public PhysicsContactPoint GetContact(int index) => contacts[index];
    public System.Collections.Generic.IEnumerator<PhysicsContactPoint> GetEnumerator()
        => ((System.Collections.Generic.IEnumerable<PhysicsContactPoint>)contacts).GetEnumerator();

    public PhysicsCollision() { }

    public PhysicsCollision(Collision source)
    {
        collider = source.collider;
        relativeVelocity = source.relativeVelocity;
        impulse = source.impulse;
        var points = source.contacts;
        contacts = new PhysicsContactPoint[points.Length];
        for (int i = 0; i < points.Length; i++)
            contacts[i] = new PhysicsContactPoint { point = points[i].point, normal = points[i].normal,
                separation = points[i].separation, thisCollider = points[i].thisCollider, otherCollider = points[i].otherCollider };
    }
}

public struct PhysicsContactPoint
{
    public Vector3 point;
    public Vector3 normal;
    public float separation;
    public Collider thisCollider;
    public Collider otherCollider;
}
