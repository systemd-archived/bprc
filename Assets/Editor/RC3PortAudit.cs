using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using RCR2Port;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Audit for the ported part catalogs: RC3 (see Tools/RC3/ExtractRC3Catalog.py)
/// and RC1 (see Tools/RC1/BuildRC1Catalog.ps1), injected by
/// <see cref="RC3Port.RC3PortCatalog"/> and <see cref="RC3Port.RC1PortCatalog"/>.
///
/// It checks the invariants that make the parts usable at all - a fixed index
/// range that saves can resolve, a part type present in *both* enums (a missing
/// one makes the whole build menu disappear with a KeyNotFoundException), a
/// centred icon, and disjoint construction/physics renderer sets (when they
/// alias, ConfigureVisuals disables every renderer and the part is invisible).
///
/// Reports: Builds/QA/rc3-parts-audit.txt and Builds/QA/rc1-parts-audit.txt
/// Run with: Unity -batchmode -quit -executeMethod RC3PortAudit.Run
/// </summary>
public static class RC3PortAudit
{
    /// <summary>One ported family: an index block, an id prefix and a part count.</summary>
    private sealed class Family
    {
        public string Name;
        public string Prefix;
        public int ExpectedParts;
        /// <summary>The fixed base the index block starts at (RC1PortCatalog/RoverPortCatalog).</summary>
        public int FirstIndexHint;
        public Func<int, bool> IsIndex;
        public string ReportPath;
        public string CsvPath;
    }

    private static readonly Family Rc3 = new Family
    {
        Name = "RC3",
        Prefix = "part_rc3_",
        ExpectedParts = 148,
        FirstIndexHint = RC3Port.RC3PortCatalog.FirstPartIndex,
        IsIndex = RC3Port.RC3PortCatalog.IsRC3Index,
        ReportPath = "Builds/QA/rc3-parts-audit.txt",
        CsvPath = "Builds/QA/rc3-parts-audit.csv",
    };

    private static readonly Family Rc1 = new Family
    {
        Name = "RC1",
        Prefix = "part_rc1_",
        ExpectedParts = 32,
        FirstIndexHint = RC3Port.RC1PortCatalog.FirstPartIndex,
        IsIndex = RC3Port.RC1PortCatalog.IsRC1Index,
        ReportPath = "Builds/QA/rc1-parts-audit.txt",
        CsvPath = "Builds/QA/rc1-parts-audit.csv",
    };

    /// <summary>A connection point has to sit on a cell edge or a cell centre.</summary>
    private const float LatticeTolerance = 0.02f;

    public static void Run()
    {
        Directory.CreateDirectory("Builds/QA");

        // The build path reads INSettings through the part-type map and loads
        // materials through INUnity's resource table; neither is initialised in
        // batch mode. Same preamble as RoverPortRC3Audit.
        typeof(Innovation.Json).GetProperty("Service").SetValue(null,
            Activator.CreateInstance(typeof(BasePart).Assembly.GetType("JsonService"), true));
        var resourcesField = typeof(INUnity).GetField("s_resources", BindingFlags.Static | BindingFlags.NonPublic);
        var resources = resourcesField.GetValue(null) as Dictionary<(string, string), UnityEngine.Object>;
        if (resources == null)
        {
            resources = new Dictionary<(string, string), UnityEngine.Object>();
            resourcesField.SetValue(null, resources);
        }
        resources[("TextAsset", "INDeclarationSettingsExp")] = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/INDeclarationSettingsExp.json");
        resources[("TextAsset", "INAliasSettings")] = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TextAsset/INAliasSettings.json");
        resources[("Material", "UIPartButtonAtlas")] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/UIPartButtonAtlas.mat");
        resources[("Material", "IngameAtlas")] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/IngameAtlas.mat");
        typeof(INSettings).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { 0 });

        RoverPortExtraCatalog.Load();
        RC3Port.RC3PortCatalog.Load();
        RC3Port.RC1PortCatalog.Load();
        RoverPortCatalog catalog = RoverPortCatalog.Instance;

        var rc3Failures = new List<string>();
        var rc1Failures = new List<string>();
        PortAuditResult rc3 = AuditFamily(Rc3, catalog, rc3Failures);
        PortAuditResult rc1 = AuditFamily(Rc1, catalog, rc1Failures);

        // RC3-only: an RC3 battery has to drive an RC3 wheel through BPLE's engine
        // power pool (see CheckBatteryDrivesWheel).
        int powerChecks = CheckBatteryDrivesWheel(rc3Failures);

        WriteReport(Rc3, rc3, rc3Failures, powerChecks);
        WriteReport(Rc1, rc1, rc1Failures, 0);

        if (rc1Failures.Count > 0 || rc3Failures.Count > 0)
        {
            throw new Exception("RC1: " + rc1Failures.Count + " failure(s)\n" + string.Join("\n", rc1Failures)
                + "\nRC3: " + rc3Failures.Count + " failure(s)\n" + string.Join("\n", rc3Failures));
        }
        Debug.Log("RC1_PARTS_AUDIT_PASS: " + rc1.Parts + " RC1 parts at indices " + rc1.Lowest + ".." + rc1.Highest
            + "; " + rc1.Anchors + " connection anchors joinable; " + rc1.PortJoins
            + " port-to-port joins created; collider frames, collider materials + depths,"
            + " run-time motion values, icons,"
            + " type mapping, renderer sets and clones checked.");
        Debug.Log("RC3_PARTS_AUDIT_PASS: " + rc3.Parts + " RC3 parts at indices " + rc3.Lowest + ".." + rc3.Highest
            + "; " + rc3.Anchors + " connection anchors joinable; " + rc3.PortJoins
            + " port-to-port joins created; " + powerChecks
            + " battery-to-wheel power checks; icons, type mapping, renderer sets,"
            + " colliders and clones checked.");
    }

    /// <summary>What one family's audit measured, for the report and the log line.</summary>
    private struct PortAuditResult
    {
        public int Parts;
        public int Anchors;
        public int PortJoins;
        public int Lowest;
        public int Highest;
    }

    private static void WriteReport(Family family, PortAuditResult result, List<string> failures, int powerChecks)
    {
        File.WriteAllLines(family.ReportPath,
            new[]
            {
                family.Name + " parts: " + result.Parts,
                "Connection anchors joined: " + result.Anchors,
                "Port-to-port joins created: " + result.PortJoins,
                "Battery-to-wheel checks: " + powerChecks,
                "Failures: " + failures.Count
            }.Concat(failures));
        if (failures.Count > 0)
        {
            Debug.LogError(family.Name + "_PARTS_AUDIT_FAIL: " + failures.Count + " failure(s), see " + family.ReportPath);
        }
    }

    /// <summary>
    /// The per-part checks, run for every family. Writes the family's CSV and
    /// returns what it measured; failures are appended to <paramref name="failures"/>.
    /// </summary>
    private static PortAuditResult AuditFamily(Family family, RoverPortCatalog catalog, List<string> failures)
    {
        var result = new PortAuditResult { Lowest = int.MaxValue, Highest = int.MinValue };
        var rows = new List<string> { "id,index,type,iconWidth,iconHeight,constructionRenderers,physicsRenderers,nodes" };

        var ids = new HashSet<string>();
        var indices = new HashSet<int>();
        int count = 0;
        int anchorsChecked = 0;
        int portJoins = 0;
        int lowest = int.MaxValue;
        int highest = int.MinValue;

        foreach (GameObject go in catalog.Parts)
        {
            var source = go.GetComponent<RoverPortPart>();
            if (source == null)
            {
                failures.Add(go.name + " has no RoverPortPart");
                continue;
            }
            if (!family.IsIndex(source.Index))
            {
                continue;
            }
            count++;
            lowest = Math.Min(lowest, source.Index);
            highest = Math.Max(highest, source.Index);
            string label = source.PortId;

            if (!ids.Add(source.PortId)) failures.Add("duplicate ID " + label);
            if (!indices.Add(source.Index)) failures.Add("duplicate index " + source.Index);
            if (!label.StartsWith(family.Prefix, StringComparison.Ordinal)) failures.Add(label + ": unexpected id prefix");
            if (catalog.GetPart(source.Index) != go) failures.Add(label + ": index lookup failed");
            if (catalog.GetPart(source.PortId) != go) failures.Add(label + ": id lookup failed");
            if (source.Type.ToSortedPartType().ToPartType() != source.Type)
            {
                failures.Add(label + ": " + source.Type + " has no same-named SortedPartType");
            }
            if (string.IsNullOrEmpty(source.DisplayName)) failures.Add(label + ": empty display name");

            int iconWidth = 0;
            int iconHeight = 0;
            var icon = source.m_constructionIconSprite;
            if (icon == null)
            {
                failures.Add(label + ": missing icon");
            }
            else
            {
                Bounds bounds = icon.SpriteMesh.bounds;
                if (bounds.center.sqrMagnitude >= 0.0001f) failures.Add(label + ": icon not centered");
                if (Mathf.Max(bounds.size.x, bounds.size.y) > RoverPortIconSprite.MaximumSize + 0.001f)
                {
                    failures.Add(label + ": oversized icon " + bounds.size);
                }
                if (Mathf.Min(bounds.size.x, bounds.size.y) <= 0f) failures.Add(label + ": empty icon");
                iconWidth = Mathf.RoundToInt(bounds.size.x);
                iconHeight = Mathf.RoundToInt(bounds.size.y);
            }

            List<Renderer> construction = GetPrivateRenderers(source, "m_constructionRenderers");
            List<Renderer> physics = GetPrivateRenderers(source, "m_physicsRenderers");
            if (construction.Count == 0) failures.Add(label + ": no construction renderers");
            if (physics.Count == 0) failures.Add(label + ": no physics renderers");
            if (construction.Intersect(physics).Any())
            {
                failures.Add(label + ": construction and physics renderers alias the same objects");
            }

            var data = (RoverPortPartData)typeof(RoverPortPart)
                .GetField("m_portData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
            if (data == null) failures.Add(label + ": no port data");
            else
            {
                if (data.motion == null) failures.Add(label + ": no motion block (build path dereferences it)");
                if (string.IsNullOrEmpty(data.model_root) || string.IsNullOrEmpty(data.physics_root))
                {
                    failures.Add(label + ": missing model_root/physics_root");
                }
                if (data.renderers == null || data.renderers.Length == 0) failures.Add(label + ": no physics renderer data");
                if (data.construction_renderers == null || data.construction_renderers.Length == 0)
                {
                    failures.Add(label + ": no construction renderer data");
                }
                if (data.colliders == null || data.colliders.Length == 0) failures.Add(label + ": no colliders");
                CheckBuildGrid(label, source, data, failures);
                if (data.grid != null && data.grid.authoritative)
                {
                    anchorsChecked += CheckAnchorsConnect(label, source, failures);
                    portJoins += CheckPortToPortConnect(label, source, failures);
                }
                CheckColliderFrames(label, source, data, failures);
                CheckColliderMaterials(label, source, data, failures);
                CheckSourceMotions(label, data, failures);
                CheckPowerOutput(label, source, data, failures);
            }

            // A part is placed by cloning the prototype, so the clone has to come
            // back with the same identity and a live node table.
            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(go);
                var copy = clone.GetComponent<RoverPortPart>();
                copy.Awake();
                if (copy.PortId != source.PortId || copy.Index != source.Index)
                {
                    failures.Add(label + ": clone identity mismatch");
                }
                if (copy.GetConnectionPoints() == null) failures.Add(label + ": clone has no connection point table");
            }
            catch (Exception exception)
            {
                failures.Add(label + ": clone threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            }

            rows.Add(string.Join(",", new[]
            {
                label,
                source.Index.ToString(CultureInfo.InvariantCulture),
                source.Type.ToString(),
                iconWidth.ToString(CultureInfo.InvariantCulture),
                iconHeight.ToString(CultureInfo.InvariantCulture),
                construction.Count.ToString(CultureInfo.InvariantCulture),
                physics.Count.ToString(CultureInfo.InvariantCulture),
                (data != null && data.nodes != null ? data.nodes.Length : 0).ToString(CultureInfo.InvariantCulture),
            }));
        }

        if (count != family.ExpectedParts) failures.Add("expected " + family.ExpectedParts + " " + family.Name + " parts, got " + count);
        if (count > 0 && lowest < family.FirstIndexHint)
        {
            failures.Add("lowest " + family.Name + " index " + lowest + " is below the fixed base");
        }
        if (count > 0 && highest - lowest + 1 != count)
        {
            failures.Add(family.Name + " indices are not a contiguous block: " + lowest + ".." + highest);
        }

        File.WriteAllLines(family.CsvPath, rows);
        result.Parts = count;
        result.Anchors = anchorsChecked;
        result.PortJoins = portJoins;
        result.Lowest = count > 0 ? lowest : 0;
        result.Highest = count > 0 ? highest : 0;
        return result;
    }

    /// <summary>
    /// End-to-end power check: an RC3 battery has to *drive* an RC3 wheel through
    /// BPLE's engine power pool, with no fuel container anywhere in the
    /// contraption (see <see cref="RoverPortPowerCellPart"/>).
    ///
    /// <see cref="CheckPowerOutput"/> only proves a battery publishes
    /// <c>m_enginePower</c>. This proves the published power reaches a consumer,
    /// which is the part the player actually sees: the contraption's component
    /// sums <c>m_enginePower</c>, <c>RoverPortPoweredPart.CalculateEnergyScale</c>
    /// reads <c>Contraption.GetEnginePowerFactor</c>, and that scale is what lets
    /// the wheel turn. A vehicle with an RC3 battery and no BPLE engine has to run.
    ///
    /// The wheel is measured twice in the same contraption - alone, then joined to
    /// the battery - so the check also fails if the wheel spins up from something
    /// other than the battery.
    /// </summary>
    private static int CheckBatteryDrivesWheel(List<string> failures)
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        int checks = 0;
        GameObject root = null;
        GameObject wheelObject = null;
        GameObject batteryObject = null;
        try
        {
            // Joining two parts reaches Contraption.GetJointConnectionStrength, which
            // reads WPFMonoBehaviour.gameData - absent in batch mode. Same stub the
            // interaction audit installs (RoverPortInteractionAudit.Run).
            EnsureGameDataStub();

            root = new GameObject("RC3AuditPowerContraption");
            var contraption = root.AddComponent<Contraption>();
            typeof(Contraption).GetMethod("PostAwake", PrivateInstance).Invoke(contraption, null);

            // part_rc3_40001 is an active wheel (RoverPortPoweredPart, draw 100 like a native
            // MotorWheel); part_rc3_30004 is the 90-capacity battery.
            wheelObject = UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_rc3_40001"));
            wheelObject.transform.SetParent(root.transform, false);
            var wheel = wheelObject.GetComponent<RoverPortPoweredPart>();
            if (wheel == null)
            {
                failures.Add("power: part_rc3_40001 is not a driven wheel");
                return checks;
            }
            wheel.Awake();
            wheel.contraption = contraption;
            wheel.EnsureRigidbody();
            contraption.Parts.Add(wheel);
            RebuildComponents(contraption);
            typeof(Contraption).GetProperty("Instance").GetSetMethod(true)
                .Invoke(null, new object[] { contraption });
            wheel.InitializeEngine();
            wheel.SetEnabled(true);

            checks++;
            if (contraption.GetEnginePowerFactor(wheel) != 0f)
            {
                failures.Add("power: an RC3 wheel with no power source has engine power");
            }
            checks++;
            if (EnergyScale(wheel) > 0f)
            {
                failures.Add("power: an RC3 wheel with no power source draws energy");
            }

            batteryObject = UnityEngine.Object.Instantiate(RoverPortCatalog.Instance.GetPart("part_rc3_30004"));
            batteryObject.transform.SetParent(root.transform, false);
            var battery = batteryObject.GetComponent<RoverPortPowerCellPart>();
            if (battery == null)
            {
                failures.Add("power: part_rc3_30004 is not a RoverPortPowerCellPart");
                return checks;
            }
            battery.Awake();
            battery.contraption = contraption;
            battery.EnsureRigidbody();
            contraption.Parts.Add(battery);
            contraption.AddFixedJoint(wheel, battery);
            RebuildComponents(contraption);
            wheel.InitializeEngine();
            wheel.SetEnabled(true);

            checks++;
            if (battery.m_enginePower <= 0f)
            {
                failures.Add("power: RC3 battery publishes no engine power");
            }
            checks++;
            if (contraption.GetEnginePowerFactor(wheel) <= 0f)
            {
                failures.Add("power: RC3 battery does not reach the engine power factor ("
                    + contraption.GetEnginePowerFactor(wheel) + ")");
            }
            checks++;
            if (EnergyScale(wheel) <= 0f)
            {
                failures.Add("power: RC3 wheel draws no energy from the battery");
            }
            checks++;
            if (wheel.RequiredFuelAmount != 0f)
            {
                failures.Add("power: RC3 wheel still demands fuel while engine power is available");
            }
        }
        catch (Exception exception)
        {
            // Full ToString: a bare NullReferenceException here is otherwise
            // undiagnosable, and this harness is the only place that runs it.
            failures.Add("power: " + exception);
        }
        finally
        {
            try
            {
                typeof(Contraption).GetProperty("Instance").GetSetMethod(true)
                    .Invoke(null, new object[] { null });
            }
            catch
            {
                // Leaving the static set is safer than tearing it down twice.
            }
            if (batteryObject != null) UnityEngine.Object.DestroyImmediate(batteryObject);
            if (wheelObject != null) UnityEngine.Object.DestroyImmediate(wheelObject);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
        return checks;
    }

    private static void RebuildComponents(Contraption contraption)
    {
        typeof(Contraption)
            .GetMethod("FindConnectedComponents", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(contraption, null);
    }

    private static float EnergyScale(RoverPortPoweredPart part)
    {
        return (float)typeof(RoverPortPoweredPart)
            .GetMethod("CalculateEnergyScale", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(part, null);
    }

    private static List<Renderer> GetPrivateRenderers(RoverPortPart part, string fieldName)
    {
        var field = typeof(RoverPortPart).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? field.GetValue(part) as List<Renderer> : null;
    }

    /// <summary>
    /// Build-grid invariants. These are the checks that would have caught the
    /// four defects this audit was extended for:
    ///
    /// * a ported part whose footprint came from its artwork instead of the
    ///   source layout, so it overhung its cells;
    /// * a part with no connection points at all, which cannot be joined to
    ///   anything (`RoverPortConnections.TryGetConnection` needs an anchor on
    ///   both sides);
    /// * a connection point that is not on the build lattice, which would put
    ///   the joint off the cell edge it belongs to;
    /// * the two node trees aliasing, which makes ConfigureVisuals hide the part.
    /// </summary>
    private static void CheckBuildGrid(string label, RoverPortPart source, RoverPortPartData data,
        List<string> failures)
    {
        var points = source.GetConnectionPoints().ToList();
        var grid = data.grid;
        // `grid == null` is not a usable test: Unity's JsonUtility turns a JSON
        // null object field into a default instance, so the eighteen drivers -
        // which the source gives no layout at all - would otherwise read as a
        // zeroed 1x1 footprint. The extractor marks a real layout instead.
        if (grid == null || !grid.authoritative)
        {
            // Parts with no source layout (the eighteen 6xxxx drivers) measure
            // their own artwork and legitimately declare no connection edges.
            if (points.Count != 0)
            {
                failures.Add(label + ": has connection points but no build layout");
            }
            return;
        }

        if (grid.cells == null)
        {
            failures.Add(label + ": build layout without cells");
            return;
        }

        // The runtime footprint must be the source layout, not the artwork's.
        int xmin = ReadGrid(source, "m_gridXmin");
        int xmax = ReadGrid(source, "m_gridXmax");
        int ymin = ReadGrid(source, "m_gridYmin");
        int ymax = ReadGrid(source, "m_gridYmax");
        if (xmin != grid.cells.xmin || xmax != grid.cells.xmax ||
            ymin != grid.cells.ymin || ymax != grid.cells.ymax)
        {
            failures.Add(string.Format(
                "{0}: footprint x[{1},{2}] y[{3},{4}] does not match the source cells x[{5},{6}] y[{7},{8}]",
                label, xmin, xmax, ymin, ymax,
                grid.cells.xmin, grid.cells.xmax, grid.cells.ymin, grid.cells.ymax));
        }

        if (points.Count == 0)
        {
            failures.Add(label + ": no connection points (cannot be joined to anything)");
            return;
        }

        // Count *distinct anchors*, not nodes: the extractor emits the points in
        // both the ME and the PE tree, so one cell edge is two nodes at the same
        // place. What the source declares (`mount_points`) is a count of edges.
        var distinct = new HashSet<(int, int)>();
        foreach (Vector3 point in points)
        {
            distinct.Add((Mathf.RoundToInt(point.x * 1000f), Mathf.RoundToInt(point.y * 1000f)));
        }
        if (grid.mount_points > 0 && distinct.Count != grid.mount_points)
        {
            failures.Add(string.Format("{0}: {1} distinct connection point(s) but the source declares {2}",
                label, distinct.Count, grid.mount_points));
        }
        // The declared mount table and the emitted node tree are two views of the
        // same source edges; they must not drift apart.
        if (grid.mounts != null && grid.mounts.Length != distinct.Count)
        {
            failures.Add(string.Format("{0}: grid.mounts has {1} entr(ies) but {2} distinct anchor(s) exist",
                label, grid.mounts.Length, distinct.Count));
        }

        // Each point must sit on the lattice and inside the part's own cells.
        foreach (Vector3 point in points)
        {
            if (OffLattice(point.x) || OffLattice(point.y))
            {
                failures.Add(string.Format("{0}: connection point ({1}, {2}) is off the build lattice",
                    label, point.x, point.y));
            }
            // The prototype sits at the origin, but measure in the part's own
            // frame so this holds wherever the audit places it.
            Vector3 local = source.transform.InverseTransformPoint(point);
            float lowX = grid.cells.xmin - 0.5f, highX = grid.cells.xmax + 0.5f;
            float lowY = grid.cells.ymin - 0.5f, highY = grid.cells.ymax + 0.5f;
            if (local.x < lowX - LatticeTolerance || local.x > highX + LatticeTolerance ||
                local.y < lowY - LatticeTolerance || local.y > highY + LatticeTolerance)
            {
                failures.Add(string.Format(
                    "{0}: connection point ({1}, {2}) is outside the part's cells x[{3},{4}] y[{5},{6}]",
                    label, local.x, local.y, grid.cells.xmin, grid.cells.xmax,
                    grid.cells.ymin, grid.cells.ymax));
            }
        }

        // The footprint must follow the part through all four grid rotations,
        // exactly like RoverPortGridAudit asserts for the RCR2 catalog.
        for (int rotation = 1; rotation <= 3; rotation++)
        {
            source.SetRotation(rotation);
            int[] expected = RotateCells(grid.cells.xmin, grid.cells.xmax,
                grid.cells.ymin, grid.cells.ymax, rotation);
            if (ReadGrid(source, "m_gridXmin") != expected[0] ||
                ReadGrid(source, "m_gridXmax") != expected[1] ||
                ReadGrid(source, "m_gridYmin") != expected[2] ||
                ReadGrid(source, "m_gridYmax") != expected[3])
            {
                failures.Add(string.Format("{0}: rotation {1} footprint does not match the rotated cells",
                    label, rotation * 90));
            }
        }
        source.SetRotation(0);
        source.SetFlipped(false);
    }

    /// <summary>
    /// The invariant <see cref="CheckAnchorsConnect"/> cannot see: two *ported* parts
    /// on neighbouring cells must join each other.
    ///
    /// <c>CheckAnchorsConnect</c> pairs a ported part with a synthetic native
    /// <c>BasePart</c>, which takes the `port`/`second` branch of
    /// <c>RoverPortConnections.TryGetConnection</c> (the native grid edges). Two ported
    /// parts take the other branch, which compares the two parts' own connection
    /// points, and that is the join a player actually gets when they build a rover out
    /// of one ported family. It is also the join that was broken: an RC1 part had no
    /// `Point-*` nodes at all, so this branch matched nothing and
    /// <c>RoverPortPart.CreateCustomJoints</c> created no joint.
    ///
    /// This runs the real path - two clones in a real <see cref="Contraption"/>,
    /// <c>CreateCustomJoints</c>, then a look for the joint it is supposed to have
    /// added - instead of calling TryGetConnection directly.
    /// </summary>
    private static int CheckPortToPortConnect(string label, RoverPortPart part, List<string> failures)
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        int created = 0;
        GameObject root = null;
        GameObject hostObject = null;
        GameObject neighbourObject = null;
        try
        {
            EnsureGameDataStub();
            var contraption = new GameObject("PortAuditContraption").AddComponent<Contraption>();
            root = contraption.gameObject;
            typeof(Contraption).GetMethod("PostAwake", PrivateInstance).Invoke(contraption, null);
            typeof(Contraption).GetProperty("Instance").GetSetMethod(true)
                .Invoke(null, new object[] { contraption });

            // The prototype itself stays untouched (the catalog shares it): both ends of
            // the join are throwaway clones, the way the interaction audit does it.
            hostObject = UnityEngine.Object.Instantiate(part.gameObject);
            hostObject.name = "PortAuditHost";
            hostObject.SetActive(true);
            var host = hostObject.GetComponent<RoverPortPart>();
            host.Awake();
            host.contraption = contraption;
            host.EnsureRigidbody();
            contraption.Parts.Add(host);

            neighbourObject = UnityEngine.Object.Instantiate(part.gameObject);
            neighbourObject.name = "PortAuditNeighbour";
            neighbourObject.SetActive(true);
            var neighbour = neighbourObject.GetComponent<RoverPortPart>();
            neighbour.Awake();
            neighbour.contraption = contraption;
            neighbour.EnsureRigidbody();
            contraption.Parts.Add(neighbour);

            // One neighbour position per face the part declares an edge on: the anchor
            // is on the cell edge, so the cell beyond it is half a cell further out.
            // Which of the four offsets is "outward" is not recorded anywhere, so all
            // four are tried and any one that joins counts. The host's own cell is
            // excluded explicitly: a copy dropped on top of the host has every anchor
            // coincident with an anchor, so it would join for the wrong reason.
            var hostCell = (Mathf.RoundToInt(hostObject.transform.position.x),
                Mathf.RoundToInt(hostObject.transform.position.y));
            var tried = new HashSet<(int, int)>();
            foreach (Vector3 point in host.GetConnectionPoints().ToList())
            {
                bool anchorJoined = false;
                foreach (Vector3 offset in new[] { Vector3.right, Vector3.up, Vector3.left, Vector3.down })
                {
                    Vector3 center = point + offset * 0.5f;
                    if (Mathf.Abs(center.x - Mathf.Round(center.x)) > LatticeTolerance ||
                        Mathf.Abs(center.y - Mathf.Round(center.y)) > LatticeTolerance)
                    {
                        continue;
                    }
                    var cell = (Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y));
                    if (cell == hostCell)
                    {
                        continue;
                    }
                    if (!tried.Add(cell))
                    {
                        continue;
                    }
                    neighbourObject.transform.position =
                        new Vector3(cell.Item1, cell.Item2, hostObject.transform.position.z);

                    // A fresh start per cell, so "already connected" cannot hide a
                    // failed pairing: clear both the joints this harness made and the
                    // contraption's own joint map (which CreateCustomJoints consults).
                    foreach (Joint stale in hostObject.GetComponents<Joint>())
                    {
                        UnityEngine.Object.DestroyImmediate(stale);
                    }
                    contraption.DestroyAllJoints();

                    host.CreateCustomJoints();

                    if (hostObject.GetComponents<Joint>().Any(joint =>
                        joint != null && joint.connectedBody == neighbour.rigidbody))
                    {
                        created++;
                        anchorJoined = true;
                        break;
                    }
                }
                if (!anchorJoined)
                {
                    failures.Add(string.Format(
                        "{0}: two copies cannot join at any cell next to anchor ({1}, {2})",
                        label, point.x, point.y));
                }
            }
            if (created == 0)
            {
                failures.Add(label + ": no pair of copies joins at any neighbouring cell");
            }
        }
        catch (Exception exception)
        {
            failures.Add(label + ": port-to-port join threw " + exception);
        }
        finally
        {
            try
            {
                typeof(Contraption).GetProperty("Instance").GetSetMethod(true)
                    .Invoke(null, new object[] { null });
            }
            catch
            {
                // Leaving the static set is safer than tearing it down twice.
            }
            if (neighbourObject != null) UnityEngine.Object.DestroyImmediate(neighbourObject);
            if (hostObject != null) UnityEngine.Object.DestroyImmediate(hostObject);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
        return created;
    }

    /// <summary>
    /// No collider may sit outside its part's own cells (one cell of margin). This is
    /// the check for the second half of the RC1 "parts fly around" report: MAIN anchors
    /// every Android prefab root at y = -26..-40, and while the port re-bases the part
    /// on its AndroidSource node, a collider re-anchored on the part root inherited that
    /// offset and spawned 26..40 cells below its part - inside the ground, where the
    /// solver depenetrates it explosively. 37 of RC1's 46 colliders did; RC3 (0 of 498)
    /// and RCR2 (0 of 610) have none.
    /// </summary>
    private static void CheckColliderFrames(string label, RoverPortPart source, RoverPortPartData data,
        List<string> failures)
    {
        if (data.colliders == null || data.grid == null || data.grid.cells == null)
        {
            return;
        }
        float lowX = data.grid.cells.xmin - 1f, highX = data.grid.cells.xmax + 1f;
        float lowY = data.grid.cells.ymin - 1f, highY = data.grid.cells.ymax + 1f;
        foreach (RoverPortColliderData collider in data.colliders)
        {
            if (collider == null || collider.offset == null || collider.offset.Length < 2)
            {
                continue;
            }
            float x = collider.offset[0];
            float y = collider.offset[1];
            if (x < lowX - LatticeTolerance || x > highX + LatticeTolerance ||
                y < lowY - LatticeTolerance || y > highY + LatticeTolerance)
            {
                failures.Add(string.Format(
                    "{0}: collider '{1}' on {2} is offset ({3}, {4}), outside the part's cells x[{5},{6}] y[{7},{8}] (+1 cell of margin) - it would spawn interpenetrating",
                    label, collider.type, collider.node, x, y,
                    data.grid.cells.xmin, data.grid.cells.xmax,
                    data.grid.cells.ymin, data.grid.cells.ymax));
            }
        }
    }

    /// <summary>
    /// The collider material/depth contract, checked on the built part instead of on the catalog.
    ///
    /// The catalog is generated, so a check that reads it back only proves the generator is
    /// self-consistent. This walks the colliders <see cref="RoverPortCatalog.BuildColliders"/>
    /// actually created and reads their <c>sharedMaterial</c>, which is what the solver uses.
    ///
    /// Why it is worth a check at all: friction is the one part of an imported Android part
    /// that MAIN stores on the *collider* and not on the BasePart, so it is the one part of
    /// MAIN's physics the port cannot get from a part field. RC1 shipped a whole generation
    /// with all 46 colliders on one invented PhysicMaterial fed by a value the generator
    /// always wrote as 0 - dynamicFriction = staticFriction = 0 on all 32 parts, including
    /// the wheels, which are floored at 0.7 by the driven-part path - so every RC1 part slid
    /// over the ground. RC1's frames, engines, thrusters and a wheel's SupportCollider must
    /// come out on Contraption_PhysMat (0.7 / 0.5, Average), its tyres on
    /// Contraption_WheelFriction_PhysMat (0.025 / 0, Multiply), its ski on the two
    /// AndroidSki materials, and its bigfoot wheels and track on Unity's Default Material
    /// (0.6 / 0.6) - the same materials this project's own Part_*.prefab files use.
    /// </summary>
    private static void CheckColliderMaterials(string label, RoverPortPart part, RoverPortPartData data,
        List<string> failures)
    {
        if (data.colliders == null)
        {
            return;
        }
        int built = part.GetComponentsInChildren<Collider>(true).Length;
        if (built != data.colliders.Length)
        {
            failures.Add(string.Format("{0}: catalog declares {1} collider(s) but the built part has {2}",
                label, data.colliders.Length, built));
        }
        foreach (RoverPortColliderData record in data.colliders)
        {
            if (record == null)
            {
                continue;
            }
            Transform node = part.ResolvePortNode(record.node);
            if (node == null)
            {
                failures.Add(string.Format("{0}: collider '{1}' names node '{2}', which the part does not have",
                    label, record.type, record.node));
                continue;
            }
            Collider collider = FindCollider(node, record.type);
            if (collider == null)
            {
                failures.Add(string.Format("{0}: no {1} on node '{2}' - BuildColliders skipped the catalog entry",
                    label, record.type, record.node));
                continue;
            }
            if (collider.sharedMaterial == null)
            {
                failures.Add(string.Format(
                    "{0}: {1} on '{2}' has no PhysicMaterial; it falls back to Unity's Default Material (0.6/0.6/Average)",
                    label, record.type, record.node));
                continue;
            }
            BoxCollider box = collider as BoxCollider;
            if (box != null && record.depth > 0f && Mathf.Abs(box.size.z - record.depth) > 0.0005f)
            {
                failures.Add(string.Format(
                    "{0}: box on '{1}' is {2:0.####} deep, the catalog says {3:0.####}",
                    label, record.node, box.size.z, record.depth));
            }
            // Catalogs that store friction as one number on the part (RCR2 / RC3) carry no
            // per-collider material; the part-level material is their contract and is checked
            // by RoverPortPart's own tests.
            if (record.material == null)
            {
                continue;
            }
            CompareMaterial(label, record, collider, failures);
        }
    }

    /// <summary>
    /// The run-time motion contract of an imported part, checked on the *deserialized*
    /// catalog record.
    ///
    /// This is the one check the generator's own assertions cannot make. The generator
    /// proves the JSON it writes is right; Unity's <c>JsonUtility</c> then matches every key
    /// to a C# field *by name* and silently leaves an unmatched field at its default - no
    /// warning, no exception. A renamed key would therefore ship as a zeroed value, and for
    /// this half of the port the values are all that matters: a bigfoot wheel with
    /// <c>travel = 0</c> welds its axle back to its mount, an exhaust with
    /// <c>size_multiplier = 0</c> smokes at 1/1.8 of MAIN's, a spoiler with four zeros
    /// pushes nothing, and a track with <c>spring_stiffness = 0</c> is rigid again. Every
    /// one of them is a valid-looking catalog, so the values have to be read back and
    /// compared here.
    /// </summary>
    private static void CheckSourceMotions(string label, RoverPortPartData data, List<string> failures)
    {
        if (data.motion != null && data.motion.wheel_pivots != null)
        {
            foreach (RoverPortWheelPivotData pivot in data.motion.wheel_pivots)
            {
                if (pivot == null)
                {
                    continue;
                }
                bool sprung = pivot.mode == "velocity_x";
                if (sprung && (pivot.travel <= 0f || pivot.spring_frequency <= 0f || pivot.damping <= 0f))
                {
                    failures.Add(string.Format(
                        "{0}: sprung pivot '{1}' reads travel {2:0.####} / frequency {3:0.####} / damping {4:0.####} - JsonUtility dropped one of them and the axle would not travel",
                        label, pivot.node, pivot.travel, pivot.spring_frequency, pivot.damping));
                }
                if (!sprung && pivot.travel > 0f)
                {
                    failures.Add(string.Format(
                        "{0}: pivot '{1}' (mode {2}) claims travel {3:0.####}; only AndroidBigfootMotion moves its node",
                        label, pivot.node, pivot.mode, pivot.travel));
                }
            }
        }
        if (data.motion != null && data.motion.spring_stiffness > 0f &&
            data.base_component != "OffRoadWheel")
        {
            failures.Add(string.Format(
                "{0}: spring_stiffness {1:0.####} with base component '{2}' - only OffRoadWheel has a CustomConnectToPart to carry it",
                label, data.motion.spring_stiffness, data.base_component));
        }
        if (data.base_component == "OffRoadWheel" &&
            (data.motion == null || data.motion.spring_stiffness <= 0f))
        {
            failures.Add(label + ": OffRoadWheel part with no connection spring - the wheel would weld rigid");
        }
        if (data.spoiler != null)
        {
            RoverPortSpoilerData spoiler = data.spoiler;
            if (spoiler.lift_coefficient <= 0f || spoiler.downforce_constant <= 0f ||
                spoiler.base_force <= 0f || spoiler.max_force <= 0f)
            {
                failures.Add(string.Format(
                    "{0}: spoiler reads lift {1:0.####} / slope {2:0.####} / base {3:0.####} / max {4:0.####} - it would push nothing",
                    label, spoiler.lift_coefficient, spoiler.downforce_constant, spoiler.base_force, spoiler.max_force));
            }
            if (spoiler.max_force < spoiler.base_force)
            {
                failures.Add(label + ": spoiler max_force is below base_force; the clamp would hold it under its resting value");
            }
        }
        if (data.particles != null)
        {
            foreach (RoverPortParticleData particle in data.particles)
            {
                if (particle == null || particle.driver != "FxSmoke")
                {
                    continue;
                }
                if (particle.size_multiplier <= 1f)
                {
                    failures.Add(string.Format(
                        "{0}: FxSmoke reads size_multiplier {1:0.####}; MAIN's Exhaust scales the plume up (1.8)",
                        label, particle.size_multiplier));
                }
            }
        }
        if (data.android_class == RoverPortSkiMotion.AndroidSkiClass)
        {
            foreach (string needed in new[]
                     {
                         RoverPortSkiMotion.SupportNodeName, RoverPortSkiMotion.BoardNodeName,
                         RoverPortSkiMotion.PivotNodeName
                     })
            {
                bool found = false;
                foreach (RoverPortNodeData node in data.nodes ?? new RoverPortNodeData[0])
                {
                    if (node != null && node.name == needed && node.id != null &&
                        node.id.StartsWith("PE:", StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    failures.Add(string.Format(
                        "{0}: {1} looks up '{2}' by name but the catalog has no PE node with that name - the ski could not be split",
                        label, RoverPortSkiMotion.AndroidSkiClass, needed));
                }
            }
        }
    }

    private static void CompareMaterial(string label, RoverPortColliderData record, Collider collider,
        List<string> failures)    {
        PhysicMaterial material = collider.sharedMaterial;
        RoverPortPhysicsMaterialData expected = record.material;
        string where = string.Format("{0}: {1} on '{2}'", label, record.type, record.node);
        if (Mathf.Abs(material.dynamicFriction - expected.dynamic_friction) > 0.0005f)
        {
            failures.Add(string.Format("{0}: dynamicFriction {1:0.####}, source '{2}' has {3:0.####}",
                where, material.dynamicFriction, expected.name, expected.dynamic_friction));
        }
        if (Mathf.Abs(material.staticFriction - expected.static_friction) > 0.0005f)
        {
            failures.Add(string.Format("{0}: staticFriction {1:0.####}, source '{2}' has {3:0.####}",
                where, material.staticFriction, expected.name, expected.static_friction));
        }
        if (Mathf.Abs(material.bounciness - expected.bounciness) > 0.0005f)
        {
            failures.Add(string.Format("{0}: bounciness {1:0.####}, source '{2}' has {3:0.####}",
                where, material.bounciness, expected.name, expected.bounciness));
        }
        if ((int)material.frictionCombine != expected.friction_combine)
        {
            failures.Add(string.Format("{0}: frictionCombine {1}, source '{2}' has {3}",
                where, material.frictionCombine, expected.name, (PhysicMaterialCombine)expected.friction_combine));
        }
        if ((int)material.bounceCombine != expected.bounce_combine)
        {
            failures.Add(string.Format("{0}: bounceCombine {1}, source '{2}' has {3}",
                where, material.bounceCombine, expected.name, (PhysicMaterialCombine)expected.bounce_combine));
        }
    }

    private static Collider FindCollider(Transform node, string type)
    {
        foreach (Collider collider in node.GetComponents<Collider>())
        {
            switch (type)
            {
                case "box":
                    if (collider is BoxCollider) return collider;
                    break;
                case "circle":
                    if (collider is SphereCollider) return collider;
                    break;
                case "capsule":
                    if (collider is CapsuleCollider) return collider;
                    break;
                default:
                    // "polygon" / "edge": a MeshCollider when the outline was usable, and a
                    // BoxCollider over the outline's bounds when it was not (RCR2 ships no
                    // polygon outlines at all, so every one of its 471 takes the fallback).
                    if (collider is MeshCollider || collider is BoxCollider) return collider;
                    break;
            }
        }
        return null;
    }

    /// <summary>
    /// <see cref="CheckBatteryDrivesWheel"/> reaches Contraption.GetJointConnectionStrength,
    /// which reads WPFMonoBehaviour.gameData - absent in batch mode, and the scene
    /// GameManager that would supply it does not exist here. Same stub the interaction
    /// audit installs (RoverPortInteractionAudit.Run); it is only created when nothing
    /// has provided one, so running the audit from a live editor session keeps the real
    /// asset.
    /// </summary>
    private static void EnsureGameDataStub()
    {
        var gameDataField = typeof(WPFMonoBehaviour).GetField("s_gameData", BindingFlags.Static | BindingFlags.NonPublic);
        if (gameDataField.GetValue(null) != null)
        {
            return;
        }
        var gameData = ScriptableObject.CreateInstance<GameData>();
        gameData.m_jointConnectionStrengthWeak = 100f;
        gameData.m_jointConnectionStrengthNormal = 1000f;
        gameData.m_jointConnectionStrengthHigh = 10000f;
        gameDataField.SetValue(null, gameData);
    }

    /// <summary>
    /// Every anchor a part declares must actually join it to a part in the
    /// neighbouring cell. This is the end-to-end proof of the connection fix:
    /// before the extractor emitted <c>ConnectionPoints</c>, 147 of the 148 parts
    /// yielded no anchors at all, <c>RoverPortConnections.TryGetConnection</c>
    /// could never match, and <c>CreateCustomJoints</c> created no joint - so no
    /// RC3 part could be attached to anything.
    ///
    /// The probe mirrors <c>RoverPortInteractionAudit.AuditWheelMounts</c>: a
    /// native frame is walked around each anchor, and the pairing must be found
    /// for every offset that puts the frame on a lattice cell centre beside it.
    /// A native frame is used rather than a second copy of the part because a
    /// part need not declare opposing edges (a wheel only has a TOP link), so
    /// only the native frame can offer a face on every side.
    /// </summary>
    private static int CheckAnchorsConnect(string label, RoverPortPart part, List<string> failures)
    {
        var probe = new GameObject("RC3AnchorProbe");
        try
        {
            var frame = probe.AddComponent<BasePart>();
            frame.m_jointConnectionType = BasePart.JointConnectionType.Source;
            probe.AddComponent<Rigidbody>();

            int checkedAnchors = 0;
            Vector3 firstAnchor = Vector3.zero;
            bool haveFirst = false;
            foreach (Vector3 point in part.GetConnectionPoints())
            {
                if (!haveFirst)
                {
                    firstAnchor = point;
                    haveFirst = true;
                }
                bool matched = false;
                foreach (Vector3 offset in new[] { Vector3.right, Vector3.up, Vector3.left, Vector3.down })
                {
                    Vector3 center = point + offset * 0.5f;
                    if (Mathf.Abs(center.x - Mathf.Round(center.x)) > LatticeTolerance ||
                        Mathf.Abs(center.y - Mathf.Round(center.y)) > LatticeTolerance)
                    {
                        continue;
                    }
                    probe.transform.position = new Vector3(Mathf.Round(center.x), Mathf.Round(center.y), 0f);
                    // The frame faces back towards the part it is joining.
                    frame.m_jointConnectionDirection = offset.x > 0f ? BasePart.JointConnectionDirection.Left :
                        offset.x < 0f ? BasePart.JointConnectionDirection.Right :
                        offset.y > 0f ? BasePart.JointConnectionDirection.Down :
                        BasePart.JointConnectionDirection.Up;
                    Vector3 anchor;
                    BasePart.Direction direction;
                    if (RoverPortConnections.TryGetConnection(part, frame, out anchor, out direction))
                    {
                        matched = true;
                    }
                }
                if (!matched)
                {
                    failures.Add(string.Format("{0}: anchor ({1}, {2}) joins nothing next to it",
                        label, point.x, point.y));
                }
                checkedAnchors++;
            }

            // ... and a part parked far away must not connect to any of them.
            if (haveFirst)
            {
                probe.transform.position = firstAnchor + new Vector3(40f, 40f, 0f);
                Vector3 unusedAnchor;
                BasePart.Direction unusedDirection;
                if (RoverPortConnections.TryGetConnection(part, frame, out unusedAnchor, out unusedDirection))
                {
                    failures.Add(label + ": a distant frame still connects");
                }
            }
            return checkedAnchors;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
        }
    }

    /// <summary>
    /// RC3 batteries drive BPLE's engine power pool instead of the source's charge
    /// model (see <see cref="RoverPortPowerCellPart"/>).
    ///
    /// This guards both halves of that contract: a battery must publish engine
    /// power and must not be a fuel container, and a part that is *not* a battery
    /// must not quietly become an engine - a ported consumer reporting engine
    /// power would drive the vehicle for free.
    /// </summary>
    private static void CheckPowerOutput(string label, RoverPortPart source, RoverPortPartData data,
        List<string> failures)
    {
        bool isBattery = data.motion != null &&
            string.Equals(data.motion.kind, "battery", StringComparison.Ordinal);
        if (!isBattery)
        {
            if (source.m_enginePower > 0f)
            {
                failures.Add(label + ": not a battery but supplies engine power");
            }
            if (source is RoverPortPowerCellPart)
            {
                failures.Add(label + ": not a battery but uses the power-cell component");
            }
            return;
        }

        if (!(source is RoverPortPowerCellPart))
        {
            failures.Add(label + ": battery does not supply engine power (BPLE cannot drive from it)");
            return;
        }
        if (source.m_enginePower <= 0f)
        {
            failures.Add(label + ": battery supplies no engine power");
        }
        if (source is IFuelContainer)
        {
            failures.Add(label + ": battery is still a fuel container (source charge model)");
        }
        if (source.m_powerConsumption != 0f)
        {
            failures.Add(label + ": engine power source must not draw power");
        }
    }

    private static int ReadGrid(RoverPortPart part, string fieldName)
    {
        return (int)typeof(BasePart).GetField(fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(part);
    }

    /// <summary>Cell block after a grid rotation, matching RoverPortGridAudit.</summary>
    private static int[] RotateCells(int xmin, int xmax, int ymin, int ymax, int rotation)
    {
        switch (rotation & 3)
        {
            case 1: return new[] { -ymax, -ymin, xmin, xmax };
            case 2: return new[] { -xmax, -xmin, -ymax, -ymin };
            case 3: return new[] { ymin, ymax, -xmax, -xmin };
            default: return new[] { xmin, xmax, ymin, ymax };
        }
    }

    private static bool OffLattice(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) > LatticeTolerance &&
               Mathf.Abs(Mathf.Abs(value - Mathf.Round(value)) - 0.5f) > LatticeTolerance;
    }
}
