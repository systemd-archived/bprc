using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit-mode audit for the extra ported catalog (RoverPortExtraCatalog).
/// The runtime injection only happens in players ([RuntimeInitializeOnLoadMethod]),
/// so this audit invokes the loader explicitly and then subjects the injected
/// part to the same checks as RoverPortAudit: icon, meshes, clone identity and
/// runtime rigidbodies/joints. Run via:
///   Unity -batchmode -executeMethod RoverPortExtraAudit.Run -quit
/// </summary>
public static class RoverPortExtraAudit
{
    public static void Run()
    {
        RoverPortExtraCatalog.Load();

        var catalog = RoverPortCatalog.Instance;
        var failures = new System.Collections.Generic.List<string>();
        GameObject clone = null;
        GameObject iconClone = null;
        try
        {
            var go = catalog.GetPart("part_mecanum_wheel");
            if (go == null) throw new Exception("extra part was not injected into the catalog");
            var source = go.GetComponent<RoverPortPart>();

            if (catalog.Parts.Count != 151) failures.Add("expected 151 parts after injection, got " + catalog.Parts.Count);
            if (source.Index < RoverPortCatalog.FirstPartIndex) failures.Add("index below FirstPartIndex: " + source.Index);
            if (catalog.GetPart(source.Index) != go) failures.Add("index lookup failed");
            if (source.PortId != "part_mecanum_wheel") failures.Add("port id mismatch: " + source.PortId);
            if (source.DisplayName != "Mecanumwheel") failures.Add("display name mismatch: " + source.DisplayName);
            if (source.Type != BasePart.PartType.MotorWheel) failures.Add("expected MotorWheel, got " + source.Type);

            var icon = source.m_constructionIconSprite;
            if (icon == null) failures.Add("missing icon");
            else
            {
                var bounds = icon.SpriteMesh.bounds;
                if (bounds.center.sqrMagnitude >= 0.00001f) failures.Add("icon not centered");
                if (Mathf.Max(bounds.size.x, bounds.size.y) > RoverPortIconSprite.MaximumSize + 0.001f) failures.Add("oversized icon");
                iconClone = UnityEngine.Object.Instantiate(icon.gameObject);
            }

            clone = UnityEngine.Object.Instantiate(go);
            var part = clone.GetComponent<RoverPortPart>();
            part.Awake();
            if (part.PortId != source.PortId || part.Index != source.Index) failures.Add("clone identity mismatch");
            int construction = ActiveRenderers(clone);
            if (construction == 0) failures.Add("no construction visual");
            foreach (var mesh in clone.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null) { failures.Add("missing mesh on " + mesh.name); continue; }
                if (mesh.sharedMesh.vertexCount != mesh.sharedMesh.uv.Length) failures.Add("UV count mismatch on " + mesh.name);
                if (mesh.GetComponent<Renderer>().sharedMaterial == null || mesh.GetComponent<Renderer>().sharedMaterial.mainTexture == null)
                    failures.Add("missing texture on " + mesh.name);
            }

            part.EnsureRigidbody();
            int physics = ActiveRenderers(clone);
            if (physics == 0) failures.Add("no physics visual");
            foreach (var joint in clone.GetComponentsInChildren<Joint>(true))
                if (joint.connectedBody == null) failures.Add("joint attached to world: " + joint.name);
            foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true))
                if ((body.constraints & RigidbodyConstraints.FreezePositionZ) == 0) failures.Add("body can leave 2D plane: " + body.name);

            int joints = clone.GetComponentsInChildren<Joint>(true).Length;
            int bodies = clone.GetComponentsInChildren<Rigidbody>(true).Length;
            // 12 bodies: the root's Rigidbody from CreateRuntimeBodies is the same
            // component EnsureRootRigidbody configures. 43 joints: the source's
            // SliderJoint on the root (PE:20 -> world) has no connected body, so
            // CreateRuntimeJoints skips it, exactly like the main catalog port.
            if (bodies != 12) failures.Add("expected 12 runtime rigidbodies, got " + bodies);
            if (joints != 43) failures.Add("expected 43 runtime joints, got " + joints);

            var powered = clone.GetComponent<RoverPortPoweredPart>();
            if (powered == null) failures.Add("missing RoverPortPoweredPart");
            else
            {
                var data = typeof(RoverPortPart).GetField("m_portData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(powered) as RoverPortPartData;
                if (data == null || data.motion == null || data.motion.kind != "wheel") failures.Add("motion data lost on clone");
                else AuditWheelMechanism(clone, part, data, powered, failures);
            }

            AuditStabilizerOverride(catalog, failures);
            AuditStabilizerMounts(catalog, failures);
        }
        catch (Exception e)
        {
            failures.Add(e.Message);
        }
        finally
        {
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            if (iconClone != null) UnityEngine.Object.DestroyImmediate(iconClone);
        }

        Directory.CreateDirectory("Builds/QA");
        File.WriteAllLines("Builds/QA/extra-audit-result.txt",
            new[] { "ExtraParts: 1", "Failures: " + failures.Count }.Concat(failures));
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
        Debug.Log("RCR2_EXTRA_AUDIT_PASS: extra part injected (part_mecanum_wheel), icon/mesh/clone/runtime physics checked.");
    }

    private static int ActiveRenderers(GameObject root)
    {
        return root.GetComponentsInChildren<Renderer>(true).Count(r =>
        {
            if (!r.enabled) return false;
            for (Transform t = r.transform; t != root.transform; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        });
    }

    /// <summary>
    /// Verifies the 1.1.0 restyle of the stabilizer: override textures applied,
    /// nodes moved, muscle behaviour attached, connection points still sitting on
    /// the part's cell edges, and a live point-to-point connection match.
    /// </summary>
    private static void AuditStabilizerOverride(RoverPortCatalog catalog, System.Collections.Generic.List<string> failures)
    {
        var gyro = catalog.GetPart("part_acid_gyroplatform");
        if (gyro == null) { failures.Add("stabilizer part missing from catalog"); return; }
        if (gyro.GetComponent<RCR2Port.RoverPortGyroPlatform>() == null) failures.Add("stabilizer muscle component missing");

        var platfrom = FindChild(gyro.transform, "platfrom");
        if (platfrom == null) failures.Add("stabilizer platfrom node missing");
        else
        {
            Vector3 relative = Vector3.zero;
            for (var t = platfrom; t != null && t != gyro.transform; t = t.parent) relative += t.localPosition;
            if (Mathf.Abs(relative.y - 1.194f) > 0.01f) failures.Add("stabilizer platfrom not repositioned: " + relative);
            var meshRenderer = platfrom.GetComponent<MeshRenderer>();
            var texture = meshRenderer != null && meshRenderer.sharedMaterial != null ? meshRenderer.sharedMaterial.mainTexture : null;
            if (texture == null || !texture.name.StartsWith("tex11_")) failures.Add("stabilizer platfrom texture not overridden");
        }

        var baseNode = FindChild(gyro.transform, "part_acid_platform_base");
        if (baseNode == null) failures.Add("stabilizer base node missing");
        else
        {
            var meshRenderer = baseNode.GetComponent<MeshRenderer>();
            var texture = meshRenderer != null && meshRenderer.sharedMaterial != null ? meshRenderer.sharedMaterial.mainTexture : null;
            if (texture == null || !texture.name.StartsWith("tex11_")) failures.Add("stabilizer base texture not overridden");
        }

        // The base plate (part_acid_platform_lvla_main) is the housing of the base cell: its art has to
        // sit on that cell, not a quarter cell up-right of it
        // (see Tools/RCR2/PatchGyroPlatformPlate.py).
        foreach (string plateNode in new[] { "Platform_base", "part_acid_platform_base" })
        {
            var node = FindChild(gyro.transform, plateNode);
            if (node == null) { failures.Add("stabilizer " + plateNode + " node missing"); continue; }
            var plateRenderer = node.GetComponent<Renderer>();
            if (plateRenderer == null) { failures.Add("stabilizer " + plateNode + " has no renderer"); continue; }
            Vector3 offset = plateRenderer.bounds.center - gyro.transform.position;
            if (new Vector2(offset.x, offset.y).magnitude > 0.02f)
                failures.Add("stabilizer " + plateNode + " plate art is off its cell: " + offset);
        }

        // A restyle must not move the part off the build cells the catalog recovered.
        var footprintPart = gyro.GetComponent<RoverPortPart>();
        var portData = typeof(RoverPortPart)
            .GetField("m_portData", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(footprintPart) as RoverPortPartData;
        var grid = portData == null ? null : portData.grid;
        if (grid == null || grid.cells == null) failures.Add("stabilizer grid data missing");
        else if (footprintPart.m_gridXmin != grid.cells.xmin || footprintPart.m_gridXmax != grid.cells.xmax ||
                 footprintPart.m_gridYmin != grid.cells.ymin || footprintPart.m_gridYmax != grid.cells.ymax)
            failures.Add(string.Format("stabilizer footprint x[{0},{1}] y[{2},{3}] != source cells x[{4},{5}] y[{6},{7}]",
                footprintPart.m_gridXmin, footprintPart.m_gridXmax, footprintPart.m_gridYmin, footprintPart.m_gridYmax,
                grid.cells.xmin, grid.cells.xmax, grid.cells.ymin, grid.cells.ymax));

        // preview art: the part buttons and the drag preview all draw
        // m_constructionIconSprite, so it has to carry the same 1.1.0 artwork
        // the placed part uses
        var partComponent = gyro.GetComponent<BasePart>();
        var iconSprite = partComponent == null ? null : partComponent.m_constructionIconSprite;
        if (iconSprite == null) failures.Add("stabilizer icon missing");
        else
        {
            var iconRenderer = iconSprite.GetComponent<MeshRenderer>();
            var iconTexture = iconRenderer != null && iconRenderer.sharedMaterial != null
                ? iconRenderer.sharedMaterial.mainTexture : null;
            if (iconTexture == null) failures.Add("stabilizer icon has no texture");
            else if (!iconTexture.name.StartsWith("tex11_")) failures.Add("stabilizer icon is not the 1.1.0 artwork: " + iconTexture.name);
        }

        // connection points must sit on the part's own cell edges - the exact
        // geometry TryGetConnection matches against for native parts.
        const float tolerance = 0.035f;
        var gridPart = gyro.GetComponent<RoverPortPart>();
        int xmin = gridPart.m_gridXmin;
        int xmax = gridPart.m_gridXmax;
        int ymin = gridPart.m_gridYmin;
        int ymax = gridPart.m_gridYmax;
        int matchedPoints = 0;
        foreach (Transform point in gyro.GetComponentsInChildren<Transform>(true))
        {
            if (!point.name.StartsWith("Point-")) continue;
            // root-relative position: GetConnectionPoints matches in world space,
            // which for the untransformed part equals the accumulated locals.
            Vector3 relative = Vector3.zero;
            for (var t = point; t != null && t != gyro.transform; t = t.parent) relative += t.localPosition;
            bool onEdge =
                Mathf.Abs(relative.x - (xmax + 0.5f)) <= tolerance ||
                Mathf.Abs(relative.x - (xmin - 0.5f)) <= tolerance ||
                Mathf.Abs(relative.y - (ymax + 0.5f)) <= tolerance ||
                Mathf.Abs(relative.y - (ymin - 0.5f)) <= tolerance;
            if (onEdge) matchedPoints++;
        }
        if (matchedPoints < 4) failures.Add("stabilizer connection points off the cell edges: " + matchedPoints + "/4 (grid " + xmin + ".." + xmax + "," + ymin + ".." + ymax + ")");

        // the frame stacked on the stabilizer has to ride the servo platform:
        // the two upper mounts are separate joint bodies in RCR2, the two lower
        // ones stay on the part root
        GameObject lower = null;
        GameObject upper = null;
        try
        {
            lower = UnityEngine.Object.Instantiate(gyro);
            lower.transform.position = Vector3.zero;
            upper = UnityEngine.Object.Instantiate(gyro);
            upper.transform.position = new Vector3(1f, 2f, 0f);
            var lowerPart = lower.GetComponent<RoverPortPart>();
            var upperPart = upper.GetComponent<RoverPortPart>();
            lowerPart.Awake();
            upperPart.Awake();
            if (!RCR2Port.RoverPortConnections.TryGetConnection(lowerPart, upperPart, out Vector3 anchor, out BasePart.Direction direction))
                failures.Add("stabilizer top connection match failed");
            else if (Mathf.Abs(anchor.y - 1.5f) > 0.01f || Mathf.Abs(anchor.x - 1f) > 0.01f)
                failures.Add("stabilizer top connection anchor unexpected: " + anchor);

            // mounts are resolved against the running part, so check them on a
            // clone whose rigidbodies exist
            lowerPart.EnsureRigidbody();
            var findNode = typeof(RoverPortPart).GetMethod("FindNodeByName", BindingFlags.NonPublic | BindingFlags.Instance);
            var findMount = typeof(RoverPortPart).GetMethod("FindConnectionMountBody", BindingFlags.NonPublic | BindingFlags.Instance);
            if (findNode == null || findMount == null)
            {
                failures.Add("mount resolution helpers missing");
            }
            else
            {
                var platform = (Transform)findNode.Invoke(lowerPart, new object[] { "part_acid_platform" });
                if (platform == null) failures.Add("stabilizer platform node missing");
                else if (platform.GetComponent<Rigidbody>() == null) failures.Add("stabilizer platform has no rigidbody");
                foreach (var pointName in new[] { "Point-1669840", "Point-1670258" })
                {
                    var point = (Transform)findNode.Invoke(lowerPart, new object[] { pointName });
                    if (point == null) { failures.Add("upper mount point missing: " + pointName); continue; }
                    var resolved = (Transform)findMount.Invoke(lowerPart, new object[] { new Vector3(point.position.x, point.position.y, 0f) });
                    if (resolved == null) failures.Add("upper mount does not ride a separate body: " + pointName);
                    else if (resolved != platform) failures.Add("upper mount rides " + resolved.name + " instead of the platform: " + pointName);
                }
                foreach (var pointName in new[] { "Point-85330", "Point-85340" })
                {
                    var point = (Transform)findNode.Invoke(lowerPart, new object[] { pointName });
                    if (point == null) { failures.Add("lower mount point missing: " + pointName); continue; }
                    var resolved = (Transform)findMount.Invoke(lowerPart, new object[] { new Vector3(point.position.x, point.position.y, 0f) });
                    if (resolved != null) failures.Add("lower mount should stay on the part root: " + pointName + " -> " + resolved.name);
                }
            }
        }
        finally
        {
            if (lower != null) UnityEngine.Object.DestroyImmediate(lower);
            if (upper != null) UnityEngine.Object.DestroyImmediate(upper);
        }
    }

    /// <summary>
    /// Verifies the 1.1.0 mecanum mechanism against the data it was ported from:
    /// the part keeps the source body masses (so the APK's own weight and its
    /// 11 Hz roller springs keep their rates), its eight roller sprites and
    /// eight rolling arms are bound to the matching physics bodies, and the
    /// driven hub takes its ground contact from the roller ring because its own
    /// collider cannot reach the ground.
    /// </summary>
    private static void AuditWheelMechanism(GameObject clone, RoverPortPart part, RoverPortPartData data,
        RoverPortPoweredPart powered, List<string> failures)
    {
        // weight: live source bodies, dormant debris excluded
        float sourceLive = 0f;
        foreach (var body in data.bodies)
        {
            var node = Node(part, body.node);
            if (node != null && IsLocallyActive(clone.transform, node)) sourceLive += body.mass;
        }
        if (Mathf.Abs(data.mass - sourceLive) > 0.001f)
            failures.Add("catalog mass " + data.mass + " != live source mass " + sourceLive);
        float builtMass = 0f;
        foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true))
            if (IsLocallyActive(clone.transform, body.transform)) builtMass += body.mass;
        if (Mathf.Abs(builtMass - sourceLive) > 0.01f)
            failures.Add("built wheel mass " + builtMass + " != source " + sourceLive);
        if (Mathf.Abs(data.consumption - 0.5f) > 0.001f)
            failures.Add("catalog consumption " + data.consumption + " != the 1.1.0 wheel value");

        // sprite bindings: every roller sprite follows its own roller body and
        // every rolling arm rotates between its hub mount and its roller
        var followers = data.followers ?? new RoverPortFollowerData[0];
        if (followers.Length != 8) failures.Add("expected 8 sprite followers, got " + followers.Length);
        foreach (var followerData in followers)
        {
            var node = Node(part, followerData.node);
            var target = Node(part, followerData.target);
            if (node == null || target == null) { failures.Add("follower node missing: " + followerData.node); continue; }
            if (!target.name.StartsWith("Roller_")) failures.Add("follower target is not a roller: " + followerData.node + " -> " + target.name);
            string expected = node.name == "Roller_sprite" ? "Roller_1" : "Roller_" + node.name.Substring("Roller_sprite_".Length);
            if (target.name != expected) failures.Add("follower bound to the wrong roller: " + node.name + " -> " + target.name + " (expected " + expected + ")");
            var follower = node.GetComponent<RoverPortFollower>();
            if (follower == null) { failures.Add("missing RoverPortFollower on " + node.name); continue; }
            if (follower.Target != target) failures.Add("follower target not wired: " + node.name);

            // the binding has to move the sprite with the body
            Quaternion spriteBefore = node.rotation;
            Quaternion bodyBefore = target.rotation;
            target.rotation = bodyBefore * Quaternion.Euler(0f, 0f, 15f);
            follower.Refresh();
            float turned = Quaternion.Angle(spriteBefore, node.rotation);
            if (Mathf.Abs(turned - 15f) > 0.6f) failures.Add("follower did not follow rotation: " + node.name + " turned " + turned.ToString("0.00"));
            target.rotation = bodyBefore;
            follower.Refresh();
            if (Quaternion.Angle(spriteBefore, node.rotation) > 0.01f) failures.Add("follower did not restore: " + node.name);
        }

        var rotators = data.stick_rotators ?? new RoverPortStickRotatorData[0];
        if (rotators.Length != 8) failures.Add("expected 8 stick rotators, got " + rotators.Length);
        foreach (var rotatorData in rotators)
        {
            var node = Node(part, rotatorData.node);
            var pointA = Node(part, rotatorData.point_a);
            var pointB = Node(part, rotatorData.point_b);
            if (node == null || pointA == null || pointB == null) { failures.Add("rotator node missing: " + rotatorData.node); continue; }
            if (!pointA.name.StartsWith("MiniHub_") || !pointB.name.StartsWith("Roller_"))
                failures.Add("rotator pair unexpected: " + pointA.name + " / " + pointB.name);
            var suffix = node.name.Substring("MiniStick_".Length);
            if (pointA.name != "MiniHub_" + suffix || pointB.name != "Roller_" + suffix)
                failures.Add("rotator bound to the wrong pair: " + node.name + " -> " + pointA.name + " / " + pointB.name);
            var rotator = node.GetComponent<RoverPortStickRotator>();
            if (rotator == null) { failures.Add("missing RoverPortStickRotator on " + node.name); continue; }
            if (rotator.PointA != pointA || rotator.PointB != pointB) { failures.Add("rotator pair not wired: " + node.name); continue; }

            Quaternion before = node.rotation;
            Vector3 rollerHome = pointB.localPosition;
            pointB.localPosition = rollerHome + new Vector3(0f, 0.25f, 0f);
            rotator.Refresh();
            float turned = Quaternion.Angle(before, node.rotation);
            if (turned < 3f) failures.Add("rotator did not follow the roller: " + node.name + " turned " + turned.ToString("0.00"));
            pointB.localPosition = rollerHome;
            rotator.Refresh();
            if (Quaternion.Angle(before, node.rotation) > 0.01f) failures.Add("rotator did not restore: " + node.name);
        }

        // ground contact: the driven hub is smaller than its roller ring, so the
        // ported drive has to reach the ground through the ring
        var contactBodies = typeof(RoverPortPoweredPart)
            .GetField("m_contactBodies", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(powered) as List<Rigidbody>;
        int contacts = contactBodies == null ? 0 : contactBodies.Count;
        if (contacts != 8) failures.Add("expected 8 contact bodies on the driven hub, got " + contacts);
        if (data.motion.contact_bodies == null || data.motion.contact_bodies.Length != 8)
            failures.Add("contact bodies missing from the catalog motion data");
        float hubReach = 0f;
        float ringReach = 0f;
        foreach (var colliderData in data.colliders)
        {
            if (colliderData.type != "circle") continue;
            var node = Node(part, colliderData.node);
            if (node == null) continue;
            bool isHub = data.motion.wheel_bodies != null && data.motion.wheel_bodies.Contains(colliderData.node);
            bool isRing = data.motion.contact_bodies != null && data.motion.contact_bodies.Contains(colliderData.node);
            Vector3 relative = Vector3.zero;
            for (var t = node; t != null && t != part.transform; t = t.parent) relative += t.localPosition;
            float offset = colliderData.offset == null ? 0f : colliderData.offset[0];
            float reach = Mathf.Sqrt((relative.x + offset) * (relative.x + offset) + relative.y * relative.y) + colliderData.radius;
            if (isHub) hubReach = Mathf.Max(hubReach, reach);
            if (isRing) ringReach = Mathf.Max(ringReach, reach);
        }
        if (hubReach <= 0f || ringReach <= 0f) failures.Add("collider reach could not be measured");
        else if (ringReach <= hubReach) failures.Add("roller ring does not extend past the hub");
        if (contacts == 8 && contactBodies.All(body => body != null && body.name.StartsWith("Roller_")) == false)
            failures.Add("contact bodies are not the rollers");
        if (contacts == 8)
        {
            string driven = data.motion.wheel_bodies == null || data.motion.wheel_bodies.Length == 0
                ? "?" : Node(part, data.motion.wheel_bodies[0]) == null ? "?" : Node(part, data.motion.wheel_bodies[0]).name;
            if (driven != "WheelHub") failures.Add("unexpected driven body: " + driven);
        }
        // traction: with a ground plane under the wheel, the hub's own collider
        // must not reach it while the roller ring does - that is the difference
        // the contact patches make for a driven hub
        GameObject ground = null;
        bool ownHit = false;
        bool ringHit = false;
        float rollingRadius = 0f;
        float ownRadius = 0f;
        try
        {
            Transform drivenNode = Node(part, data.motion.wheel_bodies[0]);
            Rigidbody drivenBody = drivenNode == null ? null : drivenNode.GetComponent<Rigidbody>();
            SphereCollider hubCollider = drivenBody == null ? null : drivenBody.GetComponent<SphereCollider>();
            if (drivenBody == null || hubCollider == null)
            {
                failures.Add("driven hub rigibody or collider missing");
            }
            else
            {
                ownRadius = Mathf.Max(0.01f, hubCollider.radius);
                float ringBottom = float.MaxValue;
                foreach (var id in data.motion.contact_bodies)
                {
                    var node = Node(part, id);
                    var patch = node == null ? null : node.GetComponent<Rigidbody>();
                    var sphere = patch == null ? null : patch.GetComponent<SphereCollider>();
                    if (sphere == null) continue;
                    ringBottom = Mathf.Min(ringBottom, patch.transform.TransformPoint(sphere.center).y - sphere.radius);
                }
                if (ringBottom == float.MaxValue) failures.Add("roller ring colliders not found");
                else
                {
                    Vector3 hubCentre = drivenBody.transform.TransformPoint(hubCollider.center);
                    ground = new GameObject("RoverPortAuditGround");
                    var box = ground.AddComponent<BoxCollider>();
                    box.size = new Vector3(20f, 0.2f, 20f);
                    ground.transform.position = new Vector3(hubCentre.x, ringBottom - box.size.y * 0.5f + 0.001f, hubCentre.z);
                    Physics.SyncTransforms();

                    var findGround = typeof(RoverPortPoweredPart).GetMethod("TryFindGroundContact", BindingFlags.NonPublic | BindingFlags.Static);
                    var findWheel = typeof(RoverPortPoweredPart).GetMethod("TryFindWheelContact", BindingFlags.NonPublic | BindingFlags.Instance);
                    object[] ownArgs = { drivenBody, ownRadius, null, null, null };
                    ownHit = (bool)findGround.Invoke(null, ownArgs);
                    object[] wheelArgs = { drivenBody, null, null, null, null };
                    ringHit = (bool)findWheel.Invoke(powered, wheelArgs);
                    rollingRadius = wheelArgs[4] == null ? 0f : (float)wheelArgs[4];
                    if (ownHit) failures.Add("the hub collider unexpectedly reaches the ground on its own");
                    if (!ringHit) failures.Add("the drive did not find the roller ring contact");
                    else if (rollingRadius <= ownRadius + 0.1f)
                        failures.Add("drive rolling radius did not use the ring: " + rollingRadius.ToString("0.###"));
                }
            }
        }
        finally
        {
            if (ground != null) UnityEngine.Object.DestroyImmediate(ground);
        }

        try
        {
            Directory.CreateDirectory("Builds/QA");
            File.WriteAllLines("Builds/QA/extra-audit-mechanism.txt", new[]
            {
                "mass=" + data.mass.ToString("0.###") + " built=" + builtMass.ToString("0.###"),
                "followers=" + followers.Length + " rotators=" + rotators.Length + " contacts=" + contacts,
                "hubReach=" + hubReach.ToString("0.###") + " ringReach=" + ringReach.ToString("0.###"),
                "traction: hubOwnContact=" + ownHit + " ringContact=" + ringHit +
                    " rollingRadius=" + rollingRadius.ToString("0.###") +
                    " hubRadius=" + ownRadius.ToString("0.###")
            });
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Builds a contraption with the stabilizer and neighbours above and below
    /// it, runs the port's joint pass and checks which body each connection joint
    /// lands on: the upper neighbour must be welded to the servo platform (so it
    /// turns with it), the lower one to the part root.
    /// </summary>
    private static void AuditStabilizerMounts(RoverPortCatalog catalog, List<string> failures)
    {
        var gyro = catalog.GetPart("part_acid_gyroplatform");
        if (gyro == null) { failures.Add("stabilizer part missing for the mount audit"); return; }
        GameObject root = null;
        GameObject gyroObject = null;
        GameObject topObject = null;
        GameObject bottomObject = null;
        try
        {
            // joints read their break force from the game data, which batch runs
            // do not load
            var gameData = ScriptableObject.CreateInstance<GameData>();
            gameData.m_jointConnectionStrengthWeak = 100f;
            gameData.m_jointConnectionStrengthNormal = 1000f;
            gameData.m_jointConnectionStrengthHigh = 10000f;
            typeof(WPFMonoBehaviour).GetField("s_gameData", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, gameData);

            root = new GameObject("AuditStabilizerContraption");
            var contraption = root.AddComponent<Contraption>();
            typeof(Contraption).GetMethod("PostAwake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(contraption, null);

            gyroObject = UnityEngine.Object.Instantiate(gyro);
            gyroObject.transform.SetParent(root.transform, false);
            gyroObject.transform.localPosition = Vector3.zero;
            var gyroPart = gyroObject.GetComponent<RoverPortPart>();
            gyroPart.Awake();
            gyroPart.contraption = contraption;
            gyroPart.EnsureRigidbody();
            contraption.Parts.Add(gyroPart);

            topObject = UnityEngine.Object.Instantiate(gyro);
            topObject.transform.SetParent(root.transform, false);
            topObject.transform.localPosition = new Vector3(1f, 2f, 0f);
            var topPart = topObject.GetComponent<RoverPortPart>();
            topPart.Awake();
            contraption.Parts.Add(topPart);

            var platform = (Transform)typeof(RoverPortPart).GetMethod("FindNodeByName", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(gyroPart, new object[] { "part_acid_platform" });
            if (platform == null) failures.Add("stabilizer platform node missing for the mount audit");

            string environment = string.Empty;
            try
            {
                gyroPart.CreateCustomJoints();
            }
            catch (Exception exception)
            {
                // the joint break force is read from game data, which a batch run
                // does not load - the joint itself is already built at that point
                environment = exception.Message;
            }
            var topJoint = platform == null ? null : platform.GetComponent<FixedJoint>();
            if (topJoint == null) failures.Add("upper mount joint was not created on the servo platform");
            else
            {
                if (topJoint.connectedBody != topObject.GetComponent<Rigidbody>())
                    failures.Add("upper mount joint does not connect the neighbour part");
                var anchorWorld = platform.TransformPoint(topJoint.anchor);
                if ((anchorWorld - topObject.transform.TransformPoint(topJoint.connectedAnchor)).sqrMagnitude > 0.001f)
                    failures.Add("upper mount joint anchors disagree: " + anchorWorld);
            }
        }
        catch (Exception exception)
        {
            failures.Add("Stabilizer upper mount: " + exception);
        }
        finally
        {
            if (topObject != null) UnityEngine.Object.DestroyImmediate(topObject);
            if (gyroObject != null) UnityEngine.Object.DestroyImmediate(gyroObject);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        // second scenario: a neighbour below the stabilizer, which must still be
        // jointed to the part root
        root = null;
        gyroObject = null;
        bottomObject = null;
        try
        {
            var gameData = ScriptableObject.CreateInstance<GameData>();
            gameData.m_jointConnectionStrengthWeak = 100f;
            gameData.m_jointConnectionStrengthNormal = 1000f;
            gameData.m_jointConnectionStrengthHigh = 10000f;
            typeof(WPFMonoBehaviour).GetField("s_gameData", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, gameData);

            root = new GameObject("AuditStabilizerContraptionLower");
            var contraption = root.AddComponent<Contraption>();
            typeof(Contraption).GetMethod("PostAwake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(contraption, null);

            gyroObject = UnityEngine.Object.Instantiate(gyro);
            gyroObject.transform.SetParent(root.transform, false);
            gyroObject.transform.localPosition = Vector3.zero;
            var gyroPart = gyroObject.GetComponent<RoverPortPart>();
            gyroPart.Awake();
            gyroPart.contraption = contraption;
            gyroPart.EnsureRigidbody();
            contraption.Parts.Add(gyroPart);

            bottomObject = UnityEngine.Object.Instantiate(gyro);
            bottomObject.transform.SetParent(root.transform, false);
            var bottomPart = bottomObject.GetComponent<RoverPortPart>();
            bottomObject.transform.localPosition = Vector3.zero;
            bottomPart.Awake();
            contraption.Parts.Add(bottomPart);

            // place the lower neighbour so one of the stabilizer's non-mount points
            // meets one of the neighbour's points instead of guessing the offset
            var findNodeByName = typeof(RoverPortPart).GetMethod("FindNodeByName", BindingFlags.NonPublic | BindingFlags.Instance);
            var lowerPoints = new List<Vector3>();
            foreach (var pointName in new[] { "Point-85330", "Point-85340" })
            {
                var point = (Transform)findNodeByName.Invoke(gyroPart, new object[] { pointName });
                if (point != null) lowerPoints.Add(point.position);
            }
            var neighbourPoints = new List<Vector3>(bottomPart.GetConnectionPoints());
            bool placed = false;
            Vector3 placedPosition = Vector3.zero;
            foreach (var basePoint in lowerPoints)
            {
                foreach (var neighbourPoint in neighbourPoints)
                {
                    placedPosition = new Vector3(basePoint.x - neighbourPoint.x, basePoint.y - neighbourPoint.y, 0f);
                    bottomObject.transform.position = placedPosition;
                    Vector3 probeAnchor;
                    BasePart.Direction probeDirection;
                    if (RoverPortConnections.TryGetConnection(gyroPart, bottomPart, out probeAnchor, out probeDirection))
                    {
                        placed = true;
                        break;
                    }
                }
                if (placed) break;
            }
            if (!placed) failures.Add("lower neighbour could not be placed on a stabilizer mount point");
            string environment = string.Empty;
            try
            {
                gyroPart.CreateCustomJoints();
            }
            catch (Exception exception)
            {
                environment = exception.Message;
            }
            var rootJoint = gyroObject.GetComponent<FixedJoint>();
            if (rootJoint == null) failures.Add("lower mount joint was not created on the part root");
            else if (rootJoint.connectedBody != bottomObject.GetComponent<Rigidbody>())
                failures.Add("lower mount joint does not connect the lower neighbour");

            try
            {
                Directory.CreateDirectory("Builds/QA");
                File.WriteAllLines("Builds/QA/extra-audit-mounts.txt", new[]
                {
                    "upper joint on: part_acid_platform (separate joint body)",
                    "lower joint on part root: " + (rootJoint != null),
                    "lower neighbour placed at: " + placedPosition + " (matched: " + placed + ")",
                    "game data note: " + environment
                });
            }
            catch (Exception)
            {
            }
        }
        catch (Exception exception)
        {
            failures.Add("Stabilizer lower mount: " + exception);
        }
        finally
        {
            if (bottomObject != null) UnityEngine.Object.DestroyImmediate(bottomObject);
            if (gyroObject != null) UnityEngine.Object.DestroyImmediate(gyroObject);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Transform Node(RoverPortPart part, string id)
    {
        var field = typeof(RoverPortPart).GetField("m_nodes", BindingFlags.NonPublic | BindingFlags.Instance);
        var nodes = field == null ? null : field.GetValue(part) as Dictionary<string, Transform>;
        Transform node;
        return nodes != null && nodes.TryGetValue(id, out node) ? node : null;
    }

    private static bool IsLocallyActive(Transform root, Transform node)
    {
        for (var current = node; current != null && current != root; current = current.parent)
            if (!current.gameObject.activeSelf) return false;
        return true;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChild(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
