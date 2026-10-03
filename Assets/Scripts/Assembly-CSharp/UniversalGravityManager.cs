using System;
using UnityEngine;

// Cross-component 2D gravity: G*m1*m2 / (r²)^attenuation, minimum r=1, cutoff 0.01N.
// Kinematic pumpkin vehicles remain sources. A batch samples each body only once per step.
public class UniversalGravityManager : PartManager
{
    private readonly GpuPhysicsForces forces = new GpuPhysicsForces(GpuPhysicsForces.ForceKind.Gravity);
    private Rigidbody[] bodies = Array.Empty<Rigidbody>();
    public override StatusCode Status => StatusCode.Running;

    public override void Start()
    {
        GpuPhysicsForces.Configure(INUserSettings.Instance?.StarSeaSettings?.GpuPhysicsCompute ?? false);
        int count = Contraption.Instance?.Parts?.Count ?? 0;
        Reserve(count);
        forces.Prepare(count);
    }

    private void Reserve(int count)
    {
        forces.Reserve(count);
        if (bodies.Length < forces.Inputs.Length) bodies = new Rigidbody[forces.Inputs.Length];
    }

    public override void FixedUpdate()
    {
        var settings = INUserSettings.Instance?.StarSeaSettings;
        if (settings == null || !settings.UniversalGravity || settings.UniversalGravityConstant == 0f) return;
        var parts = Contraption.Instance?.Parts;
        if (parts == null || parts.Count < 2) return;
        Reserve(parts.Count);
        int count = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var body = part != null ? part.rigidbody : null;
            if (body == null) continue;
            Vector3 position = part.transform.position;
            bodies[count] = body;
            forces.Inputs[count++] = new GpuPhysicsForces.Body {
                state = new Vector4(position.x, position.y, body.mass, 0f), component = part.ConnectedComponent,
                flags = body.isKinematic ? GpuPhysicsForces.Kinematic : 0
            };
        }
        var values = forces.Evaluate(count, settings.UniversalGravityConstant, settings.UniversalGravityAttenuation);
        for (int i = 0; i < count; i++)
            if ((forces.Inputs[i].flags & GpuPhysicsForces.Kinematic) == 0 && (values[i].x != 0f || values[i].y != 0f))
                bodies[i].EngineAddForce(new Vector3(values[i].x, values[i].y, 0f));
    }

    public override void OnDestroy()
    {
        forces.Dispose(); bodies = Array.Empty<Rigidbody>();
    }
}
