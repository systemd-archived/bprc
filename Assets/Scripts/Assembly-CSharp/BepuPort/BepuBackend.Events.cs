using System;
using System.Collections.Generic;
using BepuPhysics.Collidables;
using UnityEngine;

public sealed partial class BepuBackend
{
    private struct Message
    {
        public GameObject target;
        public string name;
        public object payload;
    }

    private readonly HashSet<ulong> previousContacts = new HashSet<ulong>();
    private readonly HashSet<ulong> previousTriggers = new HashSet<ulong>();
    private readonly List<Message> messages = new List<Message>(64);
    private readonly List<ulong> pairScratch = new List<ulong>(64);
    private readonly List<PhysicsContactPoint> pointScratch = new List<PhysicsContactPoint>(16);

    /// <summary>
    /// Turn this step's raw narrow-phase results into the same
    /// <c>OnPhysicsCollisionEnter/Stay/Exit</c> and <c>OnTriggerEnter/Exit</c> messages the PhysX and
    /// Box3D paths deliver.
    /// </summary>
    /// <remarks>
    /// Bepu reports contacts as a per-step set with no begin/end signalling, so enter/stay/exit are
    /// reconstructed by diffing against the previous step. That is the same shape of solution
    /// <c>Box3DBackend.Events</c> uses, and the reason <see cref="BepuCallbacks.contactPairs"/> exists.
    /// Unlike that implementation this one reuses its buffers, so a quiet step allocates nothing.
    /// </remarks>
    private void DispatchEvents()
    {
        messages.Clear();

        // ---- solid contacts -------------------------------------------------------------------
        if (callbacks.contactPairs.Count > 0 || previousContacts.Count > 0)
        {
            pairScratch.Clear();
            foreach (ulong key in previousContacts) if (!callbacks.contactPairs.Contains(key)) pairScratch.Add(key);
            foreach (ulong key in pairScratch) { previousContacts.Remove(key); QueueCollisionPair(key, "OnPhysicsCollisionExit", false); }

            pairScratch.Clear();
            foreach (ulong key in callbacks.contactPairs) if (previousContacts.Add(key)) pairScratch.Add(key);
            foreach (ulong key in pairScratch) QueueCollisionPair(key, "OnPhysicsCollisionEnter", true);

            foreach (ulong key in callbacks.contactPairs) QueueCollisionPair(key, "OnPhysicsCollisionStay", true);
        }

        // ---- triggers ------------------------------------------------------------------------
        pairScratch.Clear();
        foreach (ulong key in previousTriggers) if (!callbacks.triggerPairs.Contains(key)) pairScratch.Add(key);
        foreach (ulong key in pairScratch) { previousTriggers.Remove(key); QueueTriggerPair(key, "OnTriggerExit"); }

        bool anyEnter = false;
        pairScratch.Clear();
        foreach (ulong key in callbacks.triggerPairs) if (previousTriggers.Add(key)) { pairScratch.Add(key); anyEnter = true; }
        foreach (ulong key in pairScratch) QueueTriggerPair(key, "OnTriggerEnter");

        // A trigger that fired Enter this step must not also fire Stay.
        if (anyEnter && callbacks.triggerPairs.Count > pairScratch.Count)
        {
            var entered = new HashSet<ulong>(pairScratch);
            foreach (ulong key in callbacks.triggerPairs) if (!entered.Contains(key)) QueueTriggerPair(key, "OnTriggerStay");
        }
        else if (!anyEnter)
        {
            foreach (ulong key in callbacks.triggerPairs) QueueTriggerPair(key, "OnTriggerStay");
        }

        foreach (var message in messages)
            if (message.target && message.target.activeInHierarchy)
                message.target.SendMessage(message.name, message.payload, SendMessageOptions.DontRequireReceiver);
        messages.Clear();
    }

    private bool TryResolvePair(ulong key, out CollidableReference a, out CollidableReference b)
    {
        if (callbacks.pairRefs.TryGetValue(key, out var pair))
        {
            a = pair.a; b = pair.b;
            return true;
        }
        a = default; b = default;
        return false;
    }

    private void QueueTriggerPair(ulong key, string name)
    {
        if (!TryResolvePair(key, out var a, out var b)) return;
        var colliderA = RepresentativeCollider(a);
        var colliderB = RepresentativeCollider(b);
        if (!colliderA || !colliderB) return;
        Queue(a, name, colliderB);
        Queue(b, name, colliderA);
    }

    private void QueueCollisionPair(ulong key, string name, bool withData)
    {
        if (!TryResolvePair(key, out var a, out var b)) return;
        var colliderA = RepresentativeCollider(a);
        var colliderB = RepresentativeCollider(b);
        if (!colliderA || !colliderB) return;

        Vector3 relative = Vector3.zero;
        if (TryGetEntry(a, out var entryA) && TryGetEntry(b, out var entryB))
            relative = entryA.incomingVelocity - entryB.incomingVelocity;

        var payloadA = new PhysicsCollision { collider = colliderB, relativeVelocity = relative };
        var payloadB = new PhysicsCollision { collider = colliderA, relativeVelocity = -relative };
        if (withData) FillContacts(key, colliderA, colliderB, payloadA, payloadB);

        Queue(a, name, payloadA);
        Queue(b, name, payloadB);
    }

    private void FillContacts(ulong key, Collider colliderA, Collider colliderB,
        PhysicsCollision payloadA, PhysicsCollision payloadB)
    {
        pointScratch.Clear();
        var samples = callbacks.samples;
        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            if (sample.pairKey != key || sample.trigger) continue;
            pointScratch.Add(new PhysicsContactPoint
            {
                point = sample.offset,
                normal = sample.normal,
                separation = sample.depth,
                thisCollider = colliderA,
                otherCollider = colliderB
            });
        }
        if (pointScratch.Count == 0) return;
        payloadA.contacts = pointScratch.ToArray();
        var mirrored = new PhysicsContactPoint[pointScratch.Count];
        for (int i = 0; i < pointScratch.Count; i++)
        {
            var point = pointScratch[i];
            mirrored[i] = new PhysicsContactPoint
            {
                point = point.point,
                normal = -point.normal,
                separation = point.separation,
                thisCollider = colliderB,
                otherCollider = colliderA
            };
        }
        payloadB.contacts = mirrored;
    }

    private Collider RepresentativeCollider(in CollidableReference reference) =>
        TryGetEntry(reference, out var entry) && entry.colliderIds.Length > 0 &&
        shapes.TryGetValue(entry.colliderIds[0], out var shape) ? shape.source : null;

    private void Queue(in CollidableReference reference, string name, object payload)
    {
        if (!TryGetEntry(reference, out var entry)) return;
        GameObject target = entry.source ? entry.source.gameObject : entry.transform.gameObject;
        messages.Add(new Message { target = target, name = name, payload = payload });
        Collider collider = RepresentativeCollider(reference);
        if (collider && collider.gameObject != target)
            messages.Add(new Message { target = collider.gameObject, name = name, payload = payload });
    }
}
