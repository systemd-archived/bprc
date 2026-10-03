using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class RC1ParityAudit
{
    private static void LayerConnect(Contraption c) => Call(c, "ConnectLayeredParts");
    private static bool LayerJoined(Contraption c, BasePart a, BasePart b) => c.JointMap.Any(j =>
        j.partA == a && j.partB == b || j.partA == b && j.partB == a);
    private static void BpirAutoLayers()
    {
        var settings = INUserSettings.Instance.GeneralSettings;
        settings.MultiLayerBuild = false;
        var c = Inactive("Automatic layer detection").AddComponent<Contraption>(); Call(c, "PostAwake");
        BasePart imported(int layer)
        {
            var go = Inactive("Imported part"); var p = go.AddComponent<BasePart>();
            p.DeserializeCustomParams(BpirSaveConverter.SourceKey + ":2;CollisionLayer:" + layer);
            c.SetPartAt(0, 0, p, false, true); return p;
        }
        var a = imported(1);
        Check(!ContraptionLayers.Enabled(c), "single-layer imports leave legacy rules unchanged");
        var b = imported(2);
        Check(ContraptionLayers.Enabled(c) && !settings.MultiLayerBuild && c.FindPartAt(0, 0, a) == a && c.FindPartAt(0, 0, b) == b,
            "imported multilayer records activate isolated cell maps without enabling the global legacy-save switch");
        settings.Update(Innovation.Json.Deserialize<GeneralSettings>("{\"AutoDetectMultiLayer\":false}"));
        Call(c, "RebuildLayerMap");
        Check(ContraptionLayers.Enabled(c) && c.FindPartAt(0, 0, a) == a && c.FindPartAt(0, 0, b) == b,
            "old saved auto-detection false is ignored and original layer ownership remains intact");
        Check(typeof(GeneralSettings).GetProperty("AutoDetectMultiLayer").IsDefined(typeof(ObsoleteAttribute), true) &&
            JObject.FromObject(settings)["AutoDetectMultiLayer"] == null,
            "automatic recognition has no visible settings row or serialized toggle");
        Check(!PartConnections.Enabled(b), "automatically detected multilayer imports also disable connection points");
        var clone = c.Clone();
        Check(ContraptionLayers.Enabled(clone), "runtime clone retains automatic save-specific layer rules");
        Object.DestroyImmediate(clone.gameObject); Object.DestroyImmediate(c.gameObject);
    }
    private static void BpirConnectionMode()
    {
        var settings = INUserSettings.Instance.GeneralSettings;
        bool oldMode = settings.MultiLayerBuild, oldGrid = settings.NoGridMode, oldMerge = settings.MergeGridlessGroups;
        bool oldAll = INSettings.GetBool(INFeature.AllDirectionsConnection);
        var c = Inactive("BPIR shared connection mode").AddComponent<Contraption>(); Call(c, "PostAwake");
        var snapshot = Inactive("BPIR run snapshot").AddComponent<Contraption>(); Call(snapshot, "PostAwake");
        BasePart place(Type type, int x, int layer = 1)
        {
            var go = Inactive("Connection mode part"); go.transform.SetParent(c.transform, false);
            var p = (BasePart)go.AddComponent(type);
            p.m_jointConnectionType = BasePart.JointConnectionType.Source;
            p.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any;
            p.SetStoredParam(PartPhysicsParameters.LayerKey, layer);
            c.SetPartAt(x, 0, p, false, true); p.EnsureRigidbody(); return p;
        }
        void point(BasePart p, Vector3 position)
        {
            var root = new GameObject("ConnectionPoints"); root.transform.SetParent(p.transform, false);
            var node = new GameObject("Point-Audit"); node.transform.SetParent(root.transform, false); node.transform.position = position;
            if (p is RCR2Port.RoverPortPart)
                ((Dictionary<string, Transform>)typeof(RCR2Port.RoverPortPart).GetField("m_nodes", Private).GetValue(p))["audit-point"] = node.transform;
        }
        void connect(BasePart p) => typeof(Contraption).GetMethod("ConnectNativeGridPart", Private)
            .Invoke(c, new object[] { p, new HashSet<(BasePart, BasePart)>() });
        try
        {
            settings.MultiLayerBuild = false; settings.NoGridMode = true; settings.MergeGridlessGroups = true;
            INSettings.SetSettingsValue("AllDirectionsConnection", false);
            var a = place(typeof(BasePart), 0); var b = place(typeof(BasePart), 1);
            point(a, new Vector3(0.5f, 0f)); point(b, new Vector3(0.5f, 0f));
            var rover = (RCR2Port.RoverPortPart)place(typeof(RCR2Port.RoverPortPart), 4);
            var neighbor = place(typeof(BasePart), 5);
            point(rover, new Vector3(4f, 10f));
            var points = new List<PartConnections.Point>(); var circles = new List<GridlessGroupMerge.Circle>();
            Check(PartConnections.HasPoints(a) && PartConnections.TryGetConnection(a, b, out var anchor, out _) &&
                Vector3.Distance(anchor, new Vector3(0.5f, 0f)) < 0.0001f,
                "native mode retains authored point matching and midpoint anchors");
            Check(!c.CanConnectTo(rover, neighbor, BasePart.Direction.Right),
                "native mode rejects an imported mount whose points do not reach its grid neighbor");
            c.AddFixedJoint(a, b);
            Check(Vector3.Distance(a.transform.TransformPoint(c.JointMap.Last().joint.anchor), new Vector3(0.5f, 0f)) < 0.0001f,
                "native fixed joints still discover authored anchors");
            var cockpit = place(typeof(RCR2Port.RoverPortCockpitPart), 7);
            neighbor.m_partType = BasePart.PartType.MetalFrame;
            float sum = c.GetJointConnectionStrength(cockpit.GetJointConnectionStrength()) + c.GetJointConnectionStrength(neighbor.GetJointConnectionStrength());
            Check(Mathf.Approximately(RCR2Port.RoverPortConnections.GetJointBreakForce(c, cockpit, neighbor), sum * 0.7f),
                "native mode retains the cockpit frame strength modifier");

            settings.MultiLayerBuild = true; Call(c, "RebuildLayerMap");
            PartConnections.GetPoints(a, points); GridlessGroupMerge.GetCircles(neighbor, circles);
            Check(!PartConnections.HasPoints(a) && points.Count == 0 && circles.Count == 0 &&
                !PartConnections.TryGetConnection(a, b, out _, out _) && !rover.GetConnectionPoints().Any(),
                "shared multilayer switch disables authored matching, imported points and native circles");
            Check(!GridlessGroupMerge.Enabled && GridlessGroupMerge.TryMerge(c, 0) == 0,
                "BPIR mode disables circle merging even when the saved merge setting is on");
            rover.CreateCustomJoints();
            Check(!LayerJoined(c, rover, neighbor), "BPIR mode suppresses imported point joint creation");
            Check(c.CanConnectTo(rover, neighbor, BasePart.Direction.Right), "BPIR mode connects imported parts by grid direction");
            connect(rover); connect(neighbor);
            Check(c.JointMap.Count(link => link.partA == rover && link.partB == neighbor || link.partA == neighbor && link.partB == rover) == 1,
                "BPIR grid walk creates one imported neighbor joint without duplicate reverse connections");
            c.AddFixedJoint(a, b, new Vector3(100f, 100f));
            Check(c.JointMap.Last().joint.anchor == Vector3.zero && c.JointMap.Last().joint.connectedBody == b.rigidbody,
                "BPIR fixed joints use the root anchor even when a caller supplies a connection point");
            Check(Mathf.Approximately(RCR2Port.RoverPortConnections.GetJointBreakForce(c, cockpit, neighbor), sum),
                "BPIR mode uses the full sum of cockpit and frame connection strengths");

            var shifted = place(typeof(BasePart), 10, 2); var offsetTarget = place(typeof(BasePart), 14, 2);
            var otherLayer = place(typeof(BasePart), 14, 3);
            shifted.SetCustomParam(PartPhysicsParameters.OffsetXKey, 3f);
            LayerConnect(c);
            Check(LayerJoined(c, shifted, offsetTarget) && !LayerJoined(c, shifted, otherLayer),
                "ordinary unmarked parts gain BPIR rounded-offset neighbors only in their own layer");
            var custom = place(typeof(BpirConnectionAuditSource), 20);
            custom.m_customJointConnectionDirection = BasePart.JointConnectionDirection.Any;
            var spring = place(typeof(Spring), 21);
            spring.m_customJointConnectionDirection = BasePart.JointConnectionDirection.Any;
            connect(custom);
            Check(c.JointMap.Last().partA == custom && c.JointMap.Last().joint is FixedJoint,
                "BPIR mode gives the current part's custom joint priority over an unmarked target spring");
            var high = place(typeof(BasePart), 30, 2); var left = place(typeof(BasePart), 29);
            connect(high);
            Check(!LayerJoined(c, high, left), "BPIR public-layer neighbor walk respects the disabled left/up mask");
            INSettings.SetSettingsValue("AllDirectionsConnection", true); connect(high);
            Check(LayerJoined(c, high, left), "BPIR public-layer neighbor walk honors all-directions mode");

            var latticeA = place(typeof(BasePart), 40); var latticeB = place(typeof(BasePart), 48);
            latticeA.SetStoredParam(GridlessBuild.LatticeKey, 1); latticeB.SetStoredParam(GridlessBuild.LatticeKey, 2);
            latticeB.transform.localPosition = latticeA.transform.localPosition + Vector3.right;
            string savedLattice = latticeB.SerializeCustomParams();
            GridlessGroupMerge.ConnectRuntime(c);
            Check(!LayerJoined(c, latticeA, latticeB) && latticeB.SerializeCustomParams() == savedLattice,
                "BPIR mode suppresses saved lattice bridges without deleting saved membership");

            Call(c, "CaptureLayerRules"); settings.MultiLayerBuild = false;
            Check(!c.IsRunning && ContraptionLayers.Enabled(c) && !PartConnections.Enabled(a),
                "BPIR snapshot takes effect during startup before the running flag is set");
            Field(c, "m_running", true);
            Check(!PartConnections.HasPoints(a), "changing the shared switch cannot reenable points on a live BPIR vehicle");
            Call(snapshot, "CaptureLayerRules"); settings.MultiLayerBuild = true;
            Check(!ContraptionLayers.Enabled(snapshot), "a native run snapshot does not change when the shared switch turns on");
            snapshot.StopContraption();
            Check(ContraptionLayers.Enabled(snapshot), "stopping clears the run snapshot so the next run uses current settings");

            Field(c, "m_running", false); c.Parts.Clear(); c.StopContraption();
            settings.MultiLayerBuild = false;
            PartConnections.GetPoints(a, points); GridlessGroupMerge.GetCircles(neighbor, circles);
            Check(PartConnections.HasPoints(a) && points.Count == 1 && circles.Count == 4 &&
                PartConnections.TryGetConnection(a, b, out _, out _) && rover.GetConnectionPoints().Any(),
                "switching BPIR off restores cached connection nodes and native circles");
            c.Parts.Add(latticeA); c.Parts.Add(latticeB); GridlessGroupMerge.ConnectRuntime(c);
            Check(LayerJoined(c, latticeA, latticeB), "native mode restores saved lattice bridge joints");
            c.Parts.Add(custom); c.Parts.Add(spring);
            foreach (var link in c.JointMap.Where(link => link.partA == custom || link.partB == custom).ToList())
            { Object.DestroyImmediate(link.joint); c.JointMap.Remove(link); }
            connect(custom);
            Check(c.JointMap.Last().partA == spring && !(c.JointMap.Last().joint is FixedJoint),
                "switching BPIR off restores native target-spring custom mount priority");
        }
        finally
        {
            Object.DestroyImmediate(c.gameObject); Object.DestroyImmediate(snapshot.gameObject);
            INSettings.SetSettingsValue("AllDirectionsConnection", oldAll);
            settings.MultiLayerBuild = oldMode; settings.NoGridMode = oldGrid; settings.MergeGridlessGroups = oldMerge;
        }
    }
    private static void BpirLayers()
    {
        var settings = INUserSettings.Instance.GeneralSettings;
        Check(!new GeneralSettings().MultiLayerBuild, "BPIR multi-layer behavior defaults off for existing saves");
        settings.MultiLayerBuild = false;
        var c = Inactive("Layer connection regression").AddComponent<Contraption>(); Call(c, "PostAwake");
        BasePart box(int x, int y, int layer)
        {
            var go = new GameObject("Layer part"); go.transform.SetParent(c.transform, false);
            var part = go.AddComponent<BasePart>(); part.m_jointConnectionType = BasePart.JointConnectionType.Source;
            part.m_jointConnectionDirection = BasePart.JointConnectionDirection.Any; part.contraption = c;
            part.SetStoredParam(PartPhysicsParameters.LayerKey, layer); c.SetPartAt(x, y, part, false, true);
            part.EnsureRigidbody(); go.AddComponent<BoxCollider>(); return part;
        }
        var shared = box(0, 0, 1); var second = box(0, 0, 2); var third = box(0, 0, 3);
        string originalParams = third.SerializeCustomParams();
        Check(c.FindPartAt(0, 0) == third, "disabled mode retains the original single-map lookup behavior");
        LayerConnect(c);
        Check(c.JointMap.Count == 0, "disabled switch adds no shared-layer joints to existing vehicles");
        settings.MultiLayerBuild = true;
        Call(c, "RebuildLayerMap");
        Check(third.SerializeCustomParams() == originalParams, "enabling layer behavior never rewrites stored part parameters");
        Check(c.FindPartAt(0, 0, shared) == shared && c.FindPartAt(0, 0, second) == second && c.FindPartAt(0, 0, third) == third,
            "three coincident layers have independent cell ownership without artificial coordinate shifts");
        LayerConnect(c);
        Check(LayerJoined(c, shared, second) && LayerJoined(c, shared, third) && !LayerJoined(c, second, third),
            "layer-one same-cell chassis bridges layers two and three without directly joining those planes");
        int count = c.JointMap.Count; LayerConnect(c);
        Check(c.JointMap.Count == count, "layer connection pass is idempotent");
        var adjacent = box(2, 0, 2); var baseAdjacent = box(3, 0, 1);
        LayerConnect(c);
        Check(LayerJoined(c, adjacent, baseAdjacent), "higher layer without a base cell connects to an adjacent shared-layer chassis");
        var a = box(5, 0, 2); var baseCell = box(5, 0, 1); var unwanted = box(6, 0, 1);
        LayerConnect(c);
        Check(LayerJoined(c, a, baseCell) && !LayerJoined(c, a, unwanted), "same-cell shared chassis takes precedence over adjacent base-layer targets");
        var map = new Contraption.PartMap(); map.Set(-1, 1, 0, shared); map.Set(65535, 0, 0, second);
        Check(map.Get(-1, 1, 0) == shared && map.Get(65535, 0, 0) == second, "signed and large coordinates cannot alias another cell");
        var material = new Material(Shader.Find("Sprites/Default"));
        var renderer = third.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        var properties = new MaterialPropertyBlock(); properties.SetColor("_Color", new Color(0.6f, 0.7f, 0.8f, 0.9f));
        renderer.SetPropertyBlock(properties);
        ContraptionLayers.Select(2); ContraptionLayerView.Refresh(c);
        renderer.GetPropertyBlock(properties);
        Check(ContraptionLayers.Editable(second) && !ContraptionLayers.Editable(shared) &&
            Mathf.Approximately(properties.GetColor("_Color").a, 0.27f), "focus isolates editable parts and dims other layers to thirty percent of original alpha");
        ContraptionLayers.Select(0); ContraptionLayerView.Refresh(c);
        renderer.GetPropertyBlock(properties);
        Check(!ContraptionLayers.Editable(second) && Mathf.Approximately(properties.GetColor("_Color").a, 0.9f),
            "all-layer view restores original rendering and disallows editing");
        ContraptionLayers.Select(1);
        ContraptionLayerView.Refresh(c);
        var clone = c.Clone();
        var cloneParts = clone.GetComponentsInChildren<BasePart>(true);
        var cloneThird = cloneParts.Single(p => PartPhysicsParameters.Layer(p) == 3);
        cloneThird.GetComponent<Renderer>().GetPropertyBlock(properties);
        Check(cloneParts.Length == c.Parts.Count && cloneThird.SerializeCustomParams() == originalParams,
            "simulation cloning retains every structural layer and its stored parameters");
        float cloneAlpha = properties.HasColor("_Color") ? properties.GetColor("_Color").a : cloneThird.GetComponent<Renderer>().sharedMaterial.color.a;
        Check(cloneAlpha >= 0.9f, "simulation clone cannot inherit construction dimming; alpha=" + cloneAlpha);
        Object.DestroyImmediate(clone.gameObject);
        var editable = box(9, 3, 2); int revision = ContraptionLayers.Revision;
        editable.SetCustomParam(PartPhysicsParameters.LayerKey, 3);
        Check(c.FindPartAtInLayer(9, 3, 0, 2) == null && c.FindPartAtInLayer(9, 3, 0, 3) == editable &&
            ContraptionLayers.Revision != revision, "parameter layer changes rebuild cell ownership and invalidate focus without changing part count");
        Check(c.TryRemovePart(editable) && c.FindPartAtInLayer(9, 3, 0, 3) == null && c.FindPartAt(0, 0, second) == second,
            "deleting one layer preserves the coincident occupants of other layers");
        Object.DestroyImmediate(editable.gameObject);
        var aligned = box(20, 0, 2); aligned.m_autoAlign = BasePart.AutoAlignType.Rotate;
        aligned.m_jointConnectionDirection = BasePart.JointConnectionDirection.UpAndDown;
        box(19, 0, 1); box(20, 1, 2); aligned.SetRotation(BasePart.GridRotation.Deg_90);
        c.AutoAlign(aligned);
        Check(aligned.m_gridRotation == BasePart.GridRotation.Deg_0,
            "automatic rotation selects the part's own-plane neighbor instead of the active public-plane neighbor");
        var reset = box(9, 3, 2); reset.ClearCustomParams();
        Check(c.FindPartAtInLayer(9, 3, 0, 2) == null && c.FindPartAtInLayer(9, 3, 0, 1) == reset,
            "resetting part parameters restores default-layer map ownership");
        Object.DestroyImmediate(material); ContraptionLayerView.Clear();
        Field(shared, "m_connectedComponent", 0); Field(second, "m_connectedComponent", 0); Field(third, "m_connectedComponent", 0);
        c.gameObject.SetActive(true);
        Call(c, "CaptureLayerRules");
        Field(c, "m_running", true); ContraptionLayerCollisions.Install(c);
        settings.MultiLayerBuild = false;
        Check(ContraptionLayers.Enabled(c), "changing the feature switch during simulation takes effect on the next run instead of partially changing the live vehicle");
        settings.MultiLayerBuild = true;
        Check(GamePhysics.IsIgnoredPair(shared.collider, second.collider) && GamePhysics.IsIgnoredPair(second.collider, third.collider),
            "shared-layer joints do not re-enable cross-plane collisions");
        var control = c.GetComponent<ContraptionLayerCollisions>();
        var separateA = box(12, 0, 1); var separateB = box(15, 0, 1);
        Field(separateA, "m_connectedComponent", 4); Field(separateB, "m_connectedComponent", 4);
        settings.LayerSelfCollision = false; control.Refresh();
        Check(GamePhysics.IsIgnoredPair(separateA.collider, separateB.collider), "connected component keeps collision disabled even before shapes overlap");
        settings.LayerSelfCollision = true; control.Refresh();
        Check(!GamePhysics.IsIgnoredPair(separateA.collider, separateB.collider), "self-collision switch restores unmodified nonadjacent same-plane shapes");
        separateA.SetCustomParam(PartPhysicsParameters.OffsetXKey, 0.2f); control.Refresh();
        Check(GamePhysics.IsIgnoredPair(separateA.collider, separateB.collider), "shape overrides still suppress connected self-collision like BPIR");
        Field(separateB, "m_connectedComponent", 5); control.Refresh();
        Check(!GamePhysics.IsIgnoredPair(separateA.collider, separateB.collider), "detached components restore collisions owned by the layer filter");
        Object.DestroyImmediate(c.gameObject);
        settings.LayerSelfCollision = false; settings.MultiLayerBuild = false;
        var restored = Innovation.Json.Deserialize<GeneralSettings>("{}");
        Check(!restored.MultiLayerBuild && !restored.LayerSelfCollision, "older settings without the new fields keep the feature disabled");
        Check(!settings.MultiLayerBuild, "layer tests do not automatically enable the feature for future loads");
    }

    private static void BpirUserBlueprint()
    {
        byte[] bytes = File.ReadAllBytes("Builds/QA/bpir-fixtures/Level_Sandbox_06_2.csv");
        Check(BpirSaveConverter.TryConvert(bytes, true, out var result, out string error), "user blueprint converts: " + error);
        if (result == null) return;
        var raw = System.Text.Encoding.UTF8.GetString(bytes).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(row => row.Split(',')).ToArray();
        Check(result.data.Units.Count == raw.Length && result.warnings.Count == 0, "every source record and numeric override is retained");
        Check(result.data.Units.Select((u, i) => u.Type == int.Parse(raw[i][0]) && u.Index == int.Parse(raw[i][1]) &&
            u.X == int.Parse(raw[i][2]) && u.Y == int.Parse(raw[i][3])).All(ok => ok), "conversion preserves original order, coordinates and variant indices");
        File.WriteAllText("Builds/QA/Level_Sandbox_06_2.bprc.csv", BpirSaveConverter.SerializeCsv(result.data));
        var previousOwner = owner;
        var c = Inactive("User BPIR layered contraption").AddComponent<Contraption>(); Call(c, "PostAwake"); owner = c;
        var settings = INUserSettings.Instance.GeneralSettings; settings.MultiLayerBuild = false; settings.NoGridMode = false;
        var construction = Inactive("User blueprint construction UI").AddComponent<ConstructionUI>();
        Field(construction, "m_contraption", c); Field(WPFMonoBehaviour.levelManager, "m_constructionUI", construction);
        var templates = new Dictionary<(int, int), BasePart>();
        var staging = Inactive("User blueprint templates");
        var spriteDatabase = Inactive("BPIR legacy sprite database").AddComponent<RuntimeSpriteDatabase>();
        try
        {
            SpriteManager();
            Static(typeof(Singleton<RuntimeSpriteDatabase>), "instance", spriteDatabase);
            foreach (var record in JArray.Parse(File.ReadAllText("Builds/QA/bpir-fixtures/layer-prefabs.json")))
            {
                string path = record.Value<string>("path");
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) throw new InvalidOperationException("Missing prefab " + path);
                var go = Object.Instantiate(asset, staging.transform); go.SetActive(false);
                var part = go.GetComponent<BasePart>(); part.Index = record.Value<int>("index");
                part.Awake(); foreach (var sprite in go.GetComponentsInChildren<INSerializedSprite>(true)) sprite.CreateMesh();
                foreach (var sprite in go.GetComponentsInChildren<Sprite>(true))
                    if (!string.IsNullOrEmpty(sprite.m_id)) sprite.SelectSprite(spriteDatabase.Find(sprite.m_id));
                go.AddComponent(typeof(BasePart).Assembly.GetType("BpirAuditPartAwake", true));
                templates[(record.Value<int>("type"), part.Index)] = part;
            }
            var built = new List<BasePart>();
            foreach (var unit in result.data.ConvertTo().ContraptionDatasetList)
            {
                var template = templates[((int)((BasePart.PartType)unit.partType).ToSortedPartType(), unit.customPartIndex)];
                var part = WPFMonoBehaviour.levelManager.BuildPart(unit, template);
                var go = part.gameObject; go.SetActive(false);
                foreach (var sprite in go.GetComponentsInChildren<INSerializedSprite>(true)) sprite.CreateMesh();
                part.SetFlipped(unit.flipped); part.SetRotation((BasePart.GridRotation)unit.rot);
                part.ApplyCustomParams(); PartRotationAnimator.Complete(part.transform);
                go.SetActive(true); built.Add(part);
            }
            Check(c.Parts.Count == raw.Length, "actual construction preserves all " + raw.Length + " user parts across three layers");
            Check(ContraptionLayers.Enabled(c) && !settings.MultiLayerBuild, "full user blueprint loader automatically enables only its own layer rules");
            c.RefreshConnections(); c.RefreshPartPositions();
            foreach (var part in built) PartRotationAnimator.Complete(part.transform);
            Check(built.OfType<Spring>().All(p => p.m_gridRotation == (BasePart.GridRotation)int.Parse(raw[built.IndexOf(p)][4])),
                "reentering build UI preserves spring grid directions used for connectivity");
            var diagonalSpring = built.OfType<Spring>().Single(p => p.m_coordX == 695 && p.m_coordY == 1447);
            var uprightSpring = built.OfType<Spring>().Single(p => p.m_coordX == 690 && p.m_coordY == 1447);
            Check(Mathf.Abs(Mathf.DeltaAngle(diagonalSpring.transform.localEulerAngles.z, 45f)) < 0.001f &&
                Mathf.Abs(Mathf.DeltaAngle(uprightSpring.transform.localEulerAngles.z, 180f)) < 0.001f,
                "BPIR initial Bind gives the diagonal spring absolute 45 degrees while the spring without rotZ retains native 180 degrees");
            var bar = built.Single(p => p.Type == BasePart.PartType.JetEngine && p.Index == 2 && p.m_coordX == 695 && p.m_coordY == 1447 && PartPhysicsParameters.Layer(p) == 1);
            Check(Mathf.Abs(Vector3.Dot(diagonalSpring.transform.up.normalized, bar.transform.right.normalized)) > 0.999f,
                "reported spring coil axis follows the diagonal bar instead of crossing it at ninety degrees");
            Check(diagonalSpring.GetJointConnectionDirection() == BasePart.JointConnectionDirection.LeftAndRight &&
                diagonalSpring.GetCustomJointConnectionDirection() == BasePart.JointConnectionDirection.Right,
                "source spring retains horizontal grid connections and a right-side custom mount independently of its diagonal artwork");
            var rightFrame = built[5];
            Check(Vector2.Angle(rightFrame.transform.position - diagonalSpring.transform.position, bar.transform.right) > 20f,
                "the right neighbor's center direction is not inferred from the diagonal rod artwork");
            var springSprite = diagonalSpring.transform.Find("SpringVisualization").GetComponent<Sprite>();
            Check(springSprite.SpriteMesh != null && springSprite.SpriteMesh.bounds.size.y > springSprite.SpriteMesh.bounds.size.x &&
                Mathf.Abs(Vector3.Dot(springSprite.transform.up.normalized, bar.transform.right.normalized)) > 0.999f,
                "actual atlas-backed spring mesh is longitudinal and rendered along the reported diagonal bar");
            var normalized = new INContraptionData(built.Count);
            foreach (var part in built)
                normalized.Units.Add(new INContraptionData.Unit((int)part.Type.ToSortedPartType(), part.Index, part.m_coordX, part.m_coordY,
                    (int)part.m_gridRotation, part.m_flipped) { CustomParams = part.SerializeCustomParams() });
            File.WriteAllText("Builds/QA/Level_Sandbox_06_2.bprc.csv", BpirSaveConverter.SerializeCsv(normalized));
            var poseClone = c.Clone();
            var clonedSpring = poseClone.GetComponentsInChildren<Spring>(true).Single(p => p.m_coordX == 695 && p.m_coordY == 1447);
            Check(Mathf.Abs(Mathf.DeltaAngle(clonedSpring.transform.localEulerAngles.z, 45f)) < 0.001f &&
                clonedSpring.SerializeCustomParams() == diagonalSpring.SerializeCustomParams(), "runtime clone retains the corrected spring pose and normalized parameters");
            Object.DestroyImmediate(poseClone.gameObject);
            foreach (var wheel in built.OfType<OffRoadWheel>())
            {
                var art = wheel.transform.Find("WheelPivot/WheelSprite"); var tyre = wheel.GetComponent<SphereCollider>();
                float visualRadius = art.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x * Mathf.Abs(art.lossyScale.x);
                Check(Mathf.Approximately(art.localScale.x, 1.5f) && Mathf.Abs(visualRadius - 1.07421875f) < 0.002f,
                    "imported large wheel retains BPIR authored tyre size at source scale 1.1");
                Check(Mathf.Abs(visualRadius - tyre.radius * Mathf.Abs(wheel.transform.lossyScale.x)) < 0.002f && art.GetComponent<Renderer>().sortingOrder == 0,
                    "imported tyre collider matches artwork and renderer respects source layer depth");
            }
            Check(built.Select((p, i) => PartPhysicsParameters.Layer(p) == int.Parse(raw[i][13])).All(ok => ok), "construction places every part on its original structural layer");
            Check(c.Parts.All(p => p.enclosedInto == null || PartPhysicsParameters.SameLayer(p, p.enclosedInto)), "frame enclosure cannot capture a part on another layer");
            var positions = built.Select(p => p.transform.localPosition).ToArray();
            Check(built.Select((p, i) => {
                float ox = p.GetCustomParam(PartPhysicsParameters.OffsetXKey), oy = p.GetCustomParam(PartPhysicsParameters.OffsetYKey);
                return Mathf.Abs(p.transform.localPosition.x - int.Parse(raw[i][2]) - ox) < 0.0001f &&
                    Mathf.Abs(p.transform.localPosition.y - int.Parse(raw[i][3]) - oy) < 0.0001f;
            }).All(ok => ok), "all transformed positions match BPIR coordinates and offsets");
            var pairs = new HashSet<(BasePart, BasePart)>();
            c.gameObject.SetActive(true);
            ContraptionLayers.Select(0); ContraptionLayerView.Refresh(c);
            // Focus on the reported pair; light/propulsion artwork in this minimal scene can obscure it.
            var renderers = c.GetComponentsInChildren<Renderer>(true);
            var visibility = renderers.Select(r => r.enabled).ToArray();
            foreach (var renderer in renderers)
                renderer.enabled = renderer.transform.IsChildOf(diagonalSpring.transform) || renderer.transform.IsChildOf(bar.transform);
            CaptureObject(c.gameObject, "Builds/QA/bpir-spring-corrected.png", 1.25f, new Vector3(695f, 1446.85f, -100f));
            var correctedRotation = diagonalSpring.transform.localRotation;
            diagonalSpring.transform.localRotation = Quaternion.Euler(0f, 0f, 135f);
            CaptureObject(c.gameObject, "Builds/QA/bpir-spring-before.png", 1.25f, new Vector3(695f, 1446.85f, -100f));
            diagonalSpring.transform.localRotation = correctedRotation;
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visibility[i];
            ContraptionLayers.Select(1); ContraptionLayerView.Refresh(c);
            foreach (var part in c.Parts) { part.EnsureRigidbody(); part.rigidbody.useGravity = false; }
            foreach (var part in c.Parts)
            {
                typeof(Contraption).GetMethod("ConnectNativeGridPart", Private).Invoke(c, new object[] { part, pairs });
            }
            LayerConnect(c);
            foreach (var frame in c.Parts.OfType<Frame>()) frame.Initialize();
            var expected = BpirExpectedEdges(c, out var springMounts);
            var actual = new HashSet<(int, int)>(c.JointMap.Select(link => EdgeId(c, link.partA, link.partB)));
            Check(expected.SetEquals(actual), "user joint graph matches BPIR own-plane, shared-chassis, offset-neighbor and enclosure rules; expected=" + expected.Count);
            var actualSpringMounts = c.JointMap.Where(link => link.partA is Spring &&
                (link.joint is SpringJoint || link.joint is ConfigurableJoint)).Select(link => ((Spring)link.partA, link.partB)).ToList();
            Check(actualSpringMounts.SequenceEqual(springMounts),
                "spring custom joints are created in BPIR's source order, including shared-layer mounts before later enclosed-part mounts");
            var expectedVisualTarget = springMounts.Last(link => link.spring == diagonalSpring).target;
            var actualVisualTarget = (Rigidbody)typeof(Spring).GetField("m_connectedBody", Private).GetValue(diagonalSpring);
            Check(actualVisualTarget == expectedVisualTarget.rigidbody,
                "reported spring follows the reference's final connected body instead of choosing the visually aligned rod; expected record=" + built.IndexOf(expectedVisualTarget));
            var reportedMounts = c.JointMap.Where(link => link.partA == diagonalSpring &&
                (link.joint is SpringJoint || link.joint is ConfigurableJoint)).ToList();
            Check(reportedMounts.Select(link => built.IndexOf(link.partB)).SequenceEqual(new[] { 5, 115, 4 }),
                "reported spring has the source's three elastic mounts: right frame, public-layer tube, then right enclosed component");
            Check(reportedMounts.All(link => link.joint.connectedBody == link.partB.rigidbody &&
                (link.joint is SpringJoint ? Vector3.Distance(link.joint.anchor, new Vector3(0f, -0.5f, 0f)) < 0.0001f :
                    link.joint is ConfigurableJoint joint && joint.xMotion == ConfigurableJointMotion.Locked &&
                    joint.yMotion == ConfigurableJointMotion.Limited && joint.zMotion == ConfigurableJointMotion.Locked)),
                "actual spring joint bodies and local constraint definitions match the source rather than a rod-derived axis");
            Vector3 referenceRemote = expectedVisualTarget.transform.InverseTransformPoint(diagonalSpring.transform.position - 0.5f * diagonalSpring.transform.up);
            var actualRemote = (Vector3)typeof(Spring).GetField("m_remoteConnectionPoint", Private).GetValue(diagonalSpring);
            Check(Vector3.Distance(referenceRemote, actualRemote) < 0.0001f,
                "spring visualization endpoint is stored in the actual final connected body's coordinates");
            var visual = diagonalSpring.transform.Find("SpringVisualization");
            var savedVisualPosition = visual.localPosition; var savedVisualRotation = visual.localRotation; var savedVisualScale = visual.localScale;
            var savedTargetPosition = expectedVisualTarget.transform.position; var savedBarPosition = bar.transform.position;
            try
            {
                expectedVisualTarget.transform.position += new Vector3(0.15f, 0.1f, 0f);
                typeof(Spring).GetMethod("Update", Private).Invoke(diagonalSpring, null);
                Vector3 start = diagonalSpring.transform.TransformPoint(new Vector3(0f, 0.5f, 0f));
                Vector3 end = expectedVisualTarget.transform.TransformPoint(referenceRemote);
                Check(Vector3.Distance(visual.position, 0.5f * (start + end)) < 0.001f &&
                    Mathf.Abs(Vector3.Dot(visual.up.normalized, (end - start).normalized)) > 0.999f &&
                    Mathf.Abs(Vector3.Dot(visual.up.normalized, bar.transform.right.normalized)) < 0.999f,
                    "runtime coil follows its moved endpoint and can diverge from the rod without changing grid connectivity");
                var endpointPose = visual.position; var endpointOrientation = visual.rotation;
                bar.transform.position += new Vector3(-0.15f, 0.2f, 0f);
                typeof(Spring).GetMethod("Update", Private).Invoke(diagonalSpring, null);
                Check(Vector3.Distance(visual.position, endpointPose) < 0.0001f && Quaternion.Angle(visual.rotation, endpointOrientation) < 0.001f,
                    "moving only the rod does not select it as the spring's visualization endpoint");
            }
            finally
            {
                expectedVisualTarget.transform.position = savedTargetPosition; bar.transform.position = savedBarPosition;
                visual.localPosition = savedVisualPosition; visual.localRotation = savedVisualRotation; visual.localScale = savedVisualScale;
            }
            File.WriteAllLines("Builds/QA/bpir-spring-connections.txt", new[] {
                "grid rotation=" + diagonalSpring.m_gridRotation + "; root angle=" + diagonalSpring.transform.localEulerAngles.z,
                "grid connections=" + diagonalSpring.GetJointConnectionDirection() + "; custom mount=" + diagonalSpring.GetCustomJointConnectionDirection(),
                "elastic target records=" + string.Join(",", reportedMounts.Select(link => built.IndexOf(link.partB))),
                "tracked body record=" + built.IndexOf(expectedVisualTarget) + "; visual local endpoint=" + actualRemote,
                "right frame center delta=" + (rightFrame.transform.position - diagonalSpring.transform.position),
                "rod right axis=" + bar.transform.right
            }.Concat(reportedMounts.Select(link => link.joint.GetType().Name + " target=" + built.IndexOf(link.partB) +
                " local anchor=" + link.joint.anchor + " connected anchor=" + link.joint.connectedAnchor)));
            Call(c, "FindConnectedComponents");
            Field(c, "m_running", true); ContraptionLayerCollisions.Install(c);
            Check(c.JointMap.Any(link => !PartPhysicsParameters.SameLayer(link.partA, link.partB)) &&
                c.JointMap.Where(link => !PartPhysicsParameters.SameLayer(link.partA, link.partB)).All(link =>
                    PartPhysicsParameters.Layer(link.partA) == 1 || PartPhysicsParameters.Layer(link.partB) == 1), "actual blueprint creates public-layer bridges without joining layers two and three directly");
            Check(c.JointMap.All(link => link.joint != null && link.joint.connectedBody != null), "every recorded mount has a real connected Rigidbody");
            int jointCount = c.JointMap.Count;
            File.WriteAllLines("Builds/QA/bpir-layer-joints.txt", c.JointMap.Select(link =>
                built.IndexOf(link.partA) + "/" + built.IndexOf(link.partB) + " " + link.joint.GetType().Name +
                " anchor=" + Vector3.Distance(link.joint.transform.TransformPoint(link.joint.anchor),
                    link.joint.connectedBody.transform.TransformPoint(link.joint.connectedAnchor)) + " force=" + link.joint.breakForce));
            Physics.SyncTransforms();
            for (int i = 0; i < 180; i++) Physics.Simulate(1f / 60f);
            float displacement = built.Select((p, i) => Vector2.Distance(p.transform.localPosition, positions[i])).Max();
            float anchorError = c.JointMap.Where(link => link.joint is FixedJoint && link.joint.connectedBody != null).Select(link =>
                Vector3.Distance(link.joint.transform.TransformPoint(link.joint.anchor),
                    link.joint.connectedBody.transform.TransformPoint(link.joint.connectedAnchor))).DefaultIfEmpty(0f).Max();
            File.WriteAllLines("Builds/QA/bpir-layer-motion.txt", built.Select((p, i) => i + " " + p.GetType().Name +
                " mass=" + p.rigidbody.mass + " movement=" + Vector2.Distance(p.transform.localPosition, positions[i])));
            Rows.Add("INFO remaining joints=" + c.JointMap.Count(link => link.joint != null) + "; broken=" +
                string.Join(",", c.JointMap.Where(link => link.joint == null).Select(link => built.IndexOf(link.partA) + "/" + built.IndexOf(link.partB))));
            Check(c.JointMap.Count(link => link.joint != null) == jointCount && anchorError < 0.1f,
                "three seconds of real PhysX simulation retain all " + jointCount + " joints; max fixed mount separation=" + anchorError);
            Check(c.Parts.All(p => BuildMachineTransform.Finite(p.rigidbody.position.x) && BuildMachineTransform.Finite(p.rigidbody.position.y)), "low-mass, stretched layered blueprint produces no nonfinite physics positions");
            Rows.Add("INFO user blueprint: " + raw.Length + " parts, " + jointCount + " joints, " + c.ConnectedComponentCount + " components; world movement=" + displacement);
        }
        finally
        {
            owner = previousOwner; Object.DestroyImmediate(staging); Object.DestroyImmediate(c.gameObject); Object.DestroyImmediate(construction.gameObject);
            Object.DestroyImmediate(spriteDatabase.gameObject);
            settings.MultiLayerBuild = false;
        }
    }

    private static (int, int) EdgeId(Contraption c, BasePart a, BasePart b)
    {
        int i = c.Parts.IndexOf(a), j = c.Parts.IndexOf(b); return i < j ? (i, j) : (j, i);
    }
    // Adjacency oracle transcribed from BPIR ConnectPart/TryConnectSameCell/ConnectOffsetPartsVisually.
    // It uses an independent occupancy table, and never calls BPRC's connection or map routines.
    private static HashSet<(int, int)> BpirExpectedEdges(Contraption c, out List<(Spring spring, BasePart target)> springMounts)
    {
        var cells = new Dictionary<(int, int, int, int), BasePart>();
        foreach (var p in c.Parts)
            if (p.enclosedInto == null) cells[(p.m_coordX, p.m_coordY, p.GenerationIndex, PartPhysicsParameters.Layer(p))] = p;
        BasePart at(BasePart p, int x, int y, int layer) => cells.TryGetValue((x, y, p.GenerationIndex, layer), out var other) ? other : null;
        var edges = new HashSet<(int, int)>();
        var mounts = new List<(Spring spring, BasePart target)>();
        springMounts = mounts;
        bool connect(BasePart p, BasePart other, BasePart.Direction d)
        {
            if (other == null || other == p || !p.CanConnectTo(d) || !other.CanConnectTo(BasePart.InverseDirection(d))) return false;
            if (p.m_jointConnectionType == BasePart.JointConnectionType.None || other.m_jointConnectionType == BasePart.JointConnectionType.None ||
                p.m_jointConnectionType != BasePart.JointConnectionType.Source && other.m_jointConnectionType != BasePart.JointConnectionType.Source) return false;
            if (edges.Add(EdgeId(c, p, other)))
            {
                if (p.CanCustomConnectTo(d))
                { if (p is Spring spring) mounts.Add((spring, other)); }
                else if (other.CanCustomConnectTo(BasePart.InverseDirection(d)) && other is Spring spring)
                    mounts.Add((spring, p));
            }
            return true;
        }
        foreach (var p in c.Parts)
        {
            int layer = PartPhysicsParameters.Layer(p);
            var shared = layer == 1 ? null : at(p, p.m_coordX, p.m_coordY, 1);
            for (int d = 0; d < 4; d++)
            {
                var bit = (BitDirection)(1 << d);
                if (!INSettings.GetBool(INFeature.AllDirectionsConnection) && (bit == BitDirection.Left || bit == BitDirection.Up)) continue;
                var delta = bit.ToVector();
                connect(p, at(p, p.m_coordX + delta.Item1, p.m_coordY + delta.Item2, layer), (BasePart.Direction)d);
                if (layer > 1 && shared == null) connect(p, at(p, p.m_coordX + delta.Item1, p.m_coordY + delta.Item2, 1), (BasePart.Direction)d);
            }
            if (shared != null) for (int d = 0; d < 4; d++) if (connect(p, shared, (BasePart.Direction)d)) break;
        }
        // BPIR runs offset connections after all normal per-part own/shared-layer connections.
        foreach (var p in c.Parts)
        {
            int layer = PartPhysicsParameters.Layer(p);
            var offset = GridlessBuild.GetOffset(p);
            int x = Mathf.RoundToInt(p.m_coordX + offset.x), y = Mathf.RoundToInt(p.m_coordY + offset.y);
            if (x != p.m_coordX || y != p.m_coordY)
                for (int d = 0; d < 4; d++)
                {
                    var delta = ((BitDirection)(1 << d)).ToVector(); connect(p, at(p, x + delta.Item1, y + delta.Item2, layer), (BasePart.Direction)d);
                }
            if (p is Frame && p.enclosedPart != null && !(p.enclosedPart is Rope) && !(p.enclosedPart is HingePlate))
                edges.Add(EdgeId(c, p, p.enclosedPart));
        }
        return edges;
    }
}

public sealed class BpirConnectionAuditSource : BasePart
{
    public override Joint CustomConnectToPart(BasePart part)
    {
        var joint = gameObject.AddComponent<FixedJoint>(); joint.connectedBody = part.rigidbody; return joint;
    }
}
