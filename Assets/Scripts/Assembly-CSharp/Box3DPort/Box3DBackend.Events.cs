using System.Collections.Generic;
using Box3D;
using Unity.Mathematics;
using UnityEngine;

public sealed partial class Box3DBackend
{
    private sealed class Touch
    {
        public ShapeEntry a, b;
        public bool fresh;
    }
    private readonly Dictionary<ContactId, Touch> contacts = new Dictionary<ContactId, Touch>();
    private readonly Dictionary<(ShapeId, ShapeId), Touch> sensors = new Dictionary<(ShapeId, ShapeId), Touch>();
    private struct Message
    {
        public GameObject target;
        public string name;
        public object payload;
    }
    private readonly List<Message> messages = new List<Message>();
    private readonly List<ContactId> deadContacts = new List<ContactId>();
    private readonly List<(ShapeId, ShapeId)> deadSensorPairs = new List<(ShapeId, ShapeId)>();

    private void Queue(ShapeEntry self, string name, object payload)
    {
        if (!self.source) return;
        GameObject target = self.owner.source ? self.owner.source.gameObject : self.source.gameObject;
        messages.Add(new Message { target = target, name = name, payload = payload });
        if (target != self.source.gameObject)
            messages.Add(new Message { target = self.source.gameObject, name = name, payload = payload });
    }

    private void QueueCollision(Touch touch, string phase, ContactData? data)
    {
        if (!touch.a.source || !touch.b.source) return;
        var a = new PhysicsCollision { collider = touch.b.source,
            relativeVelocity = touch.a.owner.incomingVelocity - touch.b.owner.incomingVelocity };
        var b = new PhysicsCollision { collider = touch.a.source, relativeVelocity = -a.relativeVelocity };
        if (data.HasValue)
        {
            var manifolds = data.Value.Manifolds;
            int count = 0;
            foreach (var m in manifolds) count += m.PointCount;
            a.contacts = new PhysicsContactPoint[count]; b.contacts = new PhysicsContactPoint[count];
            Vector3 center = (float3)touch.a.owner.native.GetWorldCenterOfMass();
            int index = 0;
            foreach (var m in manifolds)
            {
                Vector3 normal = m.Normal;
                for (int i = 0; i < m.PointCount; i++)
                {
                    var p = m[i];
                    Vector3 point = center + (Vector3)p.AnchorA;
                    a.contacts[index] = new PhysicsContactPoint { point = point, normal = -normal,
                        separation = p.Separation, thisCollider = touch.a.source, otherCollider = touch.b.source };
                    b.contacts[index] = new PhysicsContactPoint { point = point, normal = normal,
                        separation = p.Separation, thisCollider = touch.b.source, otherCollider = touch.a.source };
                    a.impulse -= normal * p.TotalNormalImpulse;
                    index++;
                }
                a.impulse -= (Vector3)m.FrictionImpulse;
            }
            b.impulse = -a.impulse;
        }
        Queue(touch.a, "OnPhysicsCollision" + phase, a);
        Queue(touch.b, "OnPhysicsCollision" + phase, b);
    }

    private void QueueSensor(Touch touch, string phase)
    {
        if (!touch.a.source || !touch.b.source) return;
        Queue(touch.a, "OnTrigger" + phase, touch.b.source);
        Queue(touch.b, "OnTrigger" + phase, touch.a.source);
    }

    private void DispatchEvents()
    {
        messages.Clear();
        // Snapshot all native events before invoking game scripts: scripts can destroy objects.
        var events = world.GetContactEvents();
        foreach (var e in events.End)
            if (contacts.TryGetValue(e.ContactId, out var old))
            { QueueCollision(old, "Exit", null); contacts.Remove(e.ContactId); }
        foreach (var e in events.Begin)
            if (shapeIds.TryGetValue(e.ShapeIdA, out var a) && shapeIds.TryGetValue(e.ShapeIdB, out var b))
                contacts[e.ContactId] = new Touch { a = a, b = b, fresh = true };
        var dead = deadContacts;
        dead.Clear();
        foreach (var pair in contacts)
        {
            var contact = new Contact { Id = pair.Key };
            if (!contact.IsValid || !pair.Value.a.source || !pair.Value.b.source)
            { dead.Add(pair.Key); continue; }
            var data = contact.GetData();
            // The data's A/B ordering is authoritative for manifold anchors and normals.
            if (shapeIds.TryGetValue(data.ShapeA.Id, out var a) && shapeIds.TryGetValue(data.ShapeB.Id, out var b))
            { pair.Value.a = a; pair.Value.b = b; }
            QueueCollision(pair.Value, pair.Value.fresh ? "Enter" : "Stay", data);
            pair.Value.fresh = false;
        }
        foreach (var id in dead) contacts.Remove(id);

        var triggerEvents = world.GetSensorEvents();
        foreach (var e in triggerEvents.End)
        {
            var key = (e.SensorShapeId, e.VisitorShapeId);
            if (sensors.TryGetValue(key, out var old)) { QueueSensor(old, "Exit"); sensors.Remove(key); }
        }
        foreach (var e in triggerEvents.Begin)
            if (shapeIds.TryGetValue(e.SensorShapeId, out var a) && shapeIds.TryGetValue(e.VisitorShapeId, out var b))
                sensors[(e.SensorShapeId, e.VisitorShapeId)] = new Touch { a = a, b = b, fresh = true };
        var deadSensors = deadSensorPairs;
        deadSensors.Clear();
        foreach (var pair in sensors)
        {
            if (!pair.Value.a.source || !pair.Value.b.source || !pair.Value.a.native.IsValid || !pair.Value.b.native.IsValid)
            { deadSensors.Add(pair.Key); continue; }
            QueueSensor(pair.Value, pair.Value.fresh ? "Enter" : "Stay");
            pair.Value.fresh = false;
        }
        foreach (var key in deadSensors) sensors.Remove(key);
        foreach (var message in messages)
            if (message.target && message.target.activeInHierarchy)
                message.target.SendMessage(message.name, message.payload, SendMessageOptions.DontRequireReceiver);
        messages.Clear();
    }
}
