using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class RC1ParityAudit
{
    public static void RunBpirCompatibility()
    {
        if (!Application.isBatchMode || !File.Exists(".bprc-rc1-audit"))
            throw new InvalidOperationException("Use Tools/run-bpir-compatibility-audit.ps1 in the isolated project.");
        Rows.Clear(); failures = 0;
        Directory.CreateDirectory("Builds/QA"); reportPath = "Builds/QA/bpir-compatibility.txt";
        Physics.autoSimulation = false;
        try
        {
            Boot(); Call(owner, "PostAwake"); GamePhysics.SelectEngine(PhysicsEngine.PhysX);
            INUserSettings.Instance.StarSeaSettings.EnableParamPanel = true;
            var gameData = ScriptableObject.CreateInstance<GameData>();
            gameData.m_alienPartParticles = Inactive("Audit alien particles");
            gameData.m_alienPartParticles.AddComponent<ParticleSystem>();
            gameData.m_jointConnectionStrengthWeak = 125f;
            gameData.m_jointConnectionStrengthNormal = 125f;
            gameData.m_jointConnectionStrengthHigh = 600f;
            gameData.m_jointConnectionStrengthExtreme = 900f;
            gameData.m_jointConnectionStrengthHighlyExtreme = 1200f;
            Static(typeof(WPFMonoBehaviour), "s_gameData", gameData);
            ReportCase("source formats", BpirFormats);
            ReportCase("imported rotation migration", BpirRotationMigration);
            ReportCase("physics parameters", BpirParameters);
            ReportCase("collision filtering", BpirCollisions);
            ReportCase("optional structural layers", BpirLayers);
            ReportCase("automatic imported layers", BpirAutoLayers);
            ReportCase("shared BPIR connection switch", BpirConnectionMode);
            ReportCase("user layered blueprint", BpirUserBlueprint);
        }
        catch (Exception exception) { Check(false, exception.ToString()); }
        File.WriteAllLines(reportPath, Rows);
        Debug.Log("BPIR compatibility: " + (Rows.Count - failures) + " passed, " + failures + " failed");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void BpirRotationMigration()
    {
        var construction = Inactive("Rotation migration construction").AddComponent<ConstructionUI>();
        Field(construction, "m_contraption", owner); Field(WPFMonoBehaviour.levelManager, "m_constructionUI", construction);
        var template = Inactive("Rotation migration template").AddComponent<BasePart>();
        BasePart build(string parameters) => WPFMonoBehaviour.levelManager.BuildPart(20, 10, (int)BasePart.GridRotation.Deg_90, false, template, parameters);
        var rotated = build("@bpir20220110:2;RotationOffset:45;@machineAngle:80");
        Check(Mathf.Abs(Mathf.DeltaAngle(rotated.transform.localEulerAngles.z, 125f)) < 0.001f && rotated.GetCustomParam(BpirSaveConverter.SourceKey) == 3f,
            "old marker-two import is normalized in its own machine frame");
        string saved = rotated.SerializeCustomParams();
        BpirSaveConverter.RestoreImportedRotation(rotated);
        owner.RefreshPartPose(rotated); PartRotationAnimator.Complete(rotated.transform);
        Check(saved == rotated.SerializeCustomParams() && Mathf.Abs(Mathf.DeltaAngle(rotated.transform.localEulerAngles.z, 125f)) < 0.001f,
            "repeat migration and build refresh do not subtract the native angle twice");
        var fine = build("@bpir20220110:2;RotationOffset:45;@fineAngle:123;@machineAngle:80");
        Check(Mathf.Abs(Mathf.DeltaAngle(fine.transform.localEulerAngles.z, 248f)) < 0.001f && fine.GetCustomParam(PartPhysicsParameters.RotationKey) == 45f,
            "explicit BPRC fine-angle edits retain their existing visual pose");
        var zero = build("@bpir20220110:2;RotationOffset:0");
        Check(Mathf.Abs(Mathf.DeltaAngle(zero.transform.localEulerAngles.z, 90f)) < 0.001f,
            "source zero rotation keeps its native facing");
        var native = build("RotationOffset:45");
        var old = build("@bpir20220110:1;RotationOffset:45");
        Check(Mathf.Abs(Mathf.DeltaAngle(native.transform.localEulerAngles.z, 135f)) < 0.001f &&
            Mathf.Abs(Mathf.DeltaAngle(old.transform.localEulerAngles.z, 135f)) < 0.001f && old.GetCustomParam(BpirSaveConverter.SourceKey) == 1f,
            "ordinary BPRC and historical shifted marker-one saves keep their existing rotation semantics");
        foreach (var part in owner.Parts.ToArray()) { owner.TryRemovePart(part); Object.DestroyImmediate(part.gameObject); }
        Object.DestroyImmediate(template.gameObject); Object.DestroyImmediate(construction.gameObject);
    }

    private static void BpirFormats()
    {
        INUserSettings.Instance.GeneralSettings.MultiLayerBuild = true;
        INContraptionDataManager.Create();
        INContraptionDataManager.Settings.Enabled = true;
        Field(owner, "m_running", false);
        var construction = Inactive("BPIR loader construction").AddComponent<ConstructionUI>();
        Field(construction, "m_contraption", owner);
        Field(WPFMonoBehaviour.levelManager, "m_constructionUI", construction);
        var template = Inactive("BPIR template").AddComponent<MotorWheel>();
        template.m_partType = BasePart.PartType.MotorWheel; template.m_force = 50f;
        template.gameObject.AddComponent<SphereCollider>().radius = 0.4f;
        string[] sources = Directory.GetFiles("Builds/QA/bpir-fixtures", "bpir-authentic.*");
        Check(sources.Length == 4, "four fixtures generated by the reference assembly serializers");
        foreach (string source in sources)
        {
            bool ok = BpirSaveConverter.TryConvert(File.ReadAllBytes(source), true, out var result, out string error);
            Check(ok, Path.GetExtension(source) + " converted: " + error);
            if (!ok) continue;
            string direct = Path.Combine(INContraptionDataManager.Instance.DataDirectory, "bpir-direct");
            File.WriteAllBytes(direct, File.ReadAllBytes(source));
            Check(INContraptionDataManager.Instance.Load(direct).Units[1].CustomParams == result.data.Units[1].CustomParams,
                "normal file loader automatically recognizes raw " + Path.GetExtension(source));
            if (Path.GetExtension(source) == ".csv")
                Check(INContraptionDataManager.LoadDataFromCsv(File.ReadAllText(source)).Units[1].CustomParams == result.data.Units[1].CustomParams,
                    "normal CSV loader automatically recognizes extended records");
            if (Path.GetExtension(source) == ".json")
                Check(INContraptionDataManager.LoadDataFromJson(File.ReadAllText(source)).Units[1].CustomParams == result.data.Units[1].CustomParams,
                    "normal JSON loader preserves structural layers and members");
            Check(result.data.Units.Count == 2 && result.warnings.Count == 0, "layers preserve both coincident source parts");
            foreach (var unit in result.data.Units)
            {
                var part = WPFMonoBehaviour.levelManager.BuildPart(result.data.ConvertTo().ContraptionDatasetList[result.data.Units.IndexOf(unit)], template);
                part.EnsureRigidbody();
                Check(Mathf.Abs(part.transform.localPosition.x - 3.25f) < 0.0001f &&
                    Mathf.Abs(part.transform.localPosition.y - 3.6f) < 0.0001f, "actual placement preserves world XY and offsets");
                Check(Mathf.Approximately(part.m_mass, 2.5f) && Mathf.Approximately(part.rigidbody.mass, 2.5f), "source mass reaches the body");
                Check(!part.rigidbody.useGravity && part.rigidbody.drag == 0f, "gravity and drag overrides reach the body");
                Check(Mathf.Approximately(((MotorWheel)part).m_force, 80f) && part.m_eightWay, "source typed members retain BPIR precedence over power");
                Check(Mathf.Approximately(part.PartScaleFactors.x, 1.95f) && Mathf.Approximately(part.PartScaleFactors.y, 0.975f), "size and XY stretch compose");
                Check(Mathf.Approximately(part.transform.localEulerAngles.z, 37f) &&
                    Mathf.Approximately(PartPhysicsParameters.Depth(part), 0.03f + (PartPhysicsParameters.Layer(part) - 1) * 0.02f), "rotation and depth layers mapped");
                string normalized = part.SerializeCustomParams();
                var reloaded = WPFMonoBehaviour.levelManager.BuildPart(unit.X, unit.Y, unit.Rotation, unit.Flipped, template, normalized);
                Check(reloaded.SerializeCustomParams() == normalized && Mathf.Abs(Mathf.DeltaAngle(reloaded.transform.localEulerAngles.z, 37f)) < 0.001f,
                    "normalized rotation reloads once without losing the reference absolute pose");
                owner.TryRemovePart(reloaded); Object.DestroyImmediate(reloaded.gameObject);
            }
            Check(owner.Parts.Count == 2, "actual construction keeps coincident parts on two layers");
            foreach (var part in owner.Parts.ToArray()) { owner.TryRemovePart(part); Object.DestroyImmediate(part.gameObject); }

            string csv = BpirSaveConverter.SerializeCsv(result.data);
            Check(INContraptionDataManager.TryLoadDataFromCsv(csv, out var csvData) &&
                csvData.Units[1].CustomParams == result.data.Units[1].CustomParams, "main CSV round trip");
            string json = Innovation.Json.Serialize(result.data);
            Check(INContraptionDataManager.LoadDataFromJson(json).Units[1].CustomParams == result.data.Units[1].CustomParams, "main JSON round trip");
            File.WriteAllText("Builds/QA/bpir-converted.csv", csv);
            INContraptionDataManager.Settings.SaveFormat = ContraptionDataSettings.SerializationFormat.JSON;
            Check(INContraptionSaveFiles.Install("Builds/QA/bpir-converted.csv", "bpir-export", out _) &&
                INContraptionDataManager.Instance.LoadContraptionData("bpir-export").ContraptionDatasetList.Count == 2, "exported main CSV imports into a JSON-configured loader");
            foreach (var format in new[] { ContraptionDataSettings.SerializationFormat.CSV, ContraptionDataSettings.SerializationFormat.JSON })
            {
                INContraptionDataManager.Settings.SaveFormat = format;
                Check(INContraptionSaveFiles.Install(source, "bpir-fixture", out var message), "Install source into " + format + ": " + message);
                Check(INContraptionDataManager.Instance.LoadContraptionData("bpir-fixture").ContraptionDatasetList[1].customParams ==
                    result.data.Units[1].CustomParams, "actual main loader reads converted " + format);
            }
            INContraptionDataManager.Settings.Enabled = false;
            Check(INContraptionSaveFiles.Install(source, "bpir-encrypted", out _), "Install into original main format");
            Check(WPFPrefs.LoadOriginalContraptionDataset(INContraptionSaveFiles.LoaderDirectory, "bpir-encrypted").ContraptionDatasetList[1].customParams ==
                result.data.Units[1].CustomParams, "main encrypted XML retains new fields");
            INContraptionDataManager.Settings.Enabled = true;
        }
        foreach (string bad in new[] { "10,0,0,0,0,0,NaN", "99,0,0,0,0,0,2", "10,0,0,0,99,0,2", "{\"Units\":[{}]}",
            "10,0,0,0,0,0,2,1,1,1,1,1,@offX=Infinity,1", "10,0,0,2147483647,0,0,2" })
            Check(!BpirSaveConverter.TryConvert(Encoding.UTF8.GetBytes(bad), false, out _, out _), "reject malformed source " + bad);
        string badFile = "Builds/QA/bpir-invalid.contraption";
        string target = Path.Combine(INContraptionSaveFiles.LoaderDirectory, "bpir-fixture");
        byte[] before = File.ReadAllBytes(target);
        File.WriteAllBytes(badFile, new byte[128]);
        Check(!INContraptionSaveFiles.Install(badFile, "bpir-fixture", out _) && File.ReadAllBytes(target).SequenceEqual(before), "bad encrypted file leaves existing save intact");
        File.WriteAllText(badFile, "10,0,0,0,0,0,NaN");
        Check(!INContraptionSaveFiles.Install(badFile, "bpir-fixture", out _) && File.ReadAllBytes(target).SequenceEqual(before), "bad extended text leaves existing save intact");
        File.WriteAllBytes(badFile, new CryptoUtility("3b91A049Ca7HvSjhxT35").Encrypt(Encoding.UTF8.GetBytes(
            "<ContraptionDataset><ContraptionDatasetList><ContraptionDatasetUnit x='0' y='0' partType='999' customPartIndex='0' rot='0' flipped='false' mass='2'/></ContraptionDatasetList></ContraptionDataset>")));
        Check(!INContraptionSaveFiles.Install(badFile, "bpir-fixture", out _) && File.ReadAllBytes(target).SequenceEqual(before), "bad encrypted BPIR data cannot bypass conversion validation");
        string row = "10,0,0,0,0,0,-1,-1,-1,-1,-1,-1,,1";
        Check(BpirSaveConverter.TryConvert(Encoding.UTF8.GetBytes(row + "\n" + row), true, out var coincident, out _) && coincident.data.Units.Count == 2, "same-layer duplicates convert without collapse");
        foreach (var unit in coincident.data.ConvertTo().ContraptionDatasetList) WPFMonoBehaviour.levelManager.BuildPart(unit, template);
        Check(owner.Parts.Count == 2, "same-layer coincident parts survive actual construction");
        foreach (var part in owner.Parts.ToArray()) { owner.TryRemovePart(part); Object.DestroyImmediate(part.gameObject); }
        Check(BpirSaveConverter.TryConvert(Encoding.UTF8.GetBytes(row + "\n" + row.Substring(0, row.Length - 1) + "2"), true,
            out var layeredCopy, out _), "layered clipboard fixture converts");
        ContraptionLayers.Select(3);
        foreach (var unit in layeredCopy.data.Units)
            WPFMonoBehaviour.levelManager.BuildPart(unit.X, unit.Y, unit.Rotation, unit.Flipped, template, unit.CustomParams);
        Check(owner.Parts.Count == 2 && owner.Parts.Select(PartPhysicsParameters.Layer).OrderBy(layer => layer).SequenceEqual(new[] { 1, 2 }),
            "clipboard/undo builder preserves source layers before map insertion even with a different active plane");
        foreach (var part in owner.Parts.ToArray()) { owner.TryRemovePart(part); Object.DestroyImmediate(part.gameObject); }
        ContraptionLayers.Select(1);
        Object.DestroyImmediate(template.gameObject); Object.DestroyImmediate(construction.gameObject);
        INUserSettings.Instance.GeneralSettings.MultiLayerBuild = false;
        Field(owner, "m_running", true);
    }

    private static BasePart BpirBox(Vector3 position, Vector3 size)
    {
        var go = Inactive("BPIR shape"); go.transform.position = position;
        go.AddComponent<BoxCollider>().size = size;
        var part = go.AddComponent<BasePart>(); part.contraption = owner;
        part.EnsureRigidbody(); owner.Parts.Add(part); go.SetActive(true);
        return part;
    }

    private static void BpirParameters()
    {
        var wheel = (RCR2Port.RoverPortPoweredPart)Build(1009, new Vector3(50, 0, 0));
        var forceField = typeof(RCR2Port.RoverPortPoweredPart).GetField("m_force", Private);
        float original = (float)forceField.GetValue(wheel);
        wheel.SetCustomParam(PartPhysicsParameters.PowerKey, 2f);
        Check(original > 0 && Mathf.Approximately((float)forceField.GetValue(wheel), original * 2f), "RC1 initialized force is the multiplier baseline");
        for (int i = 0; i < 6; i++) { wheel.ApplyCustomParams(); wheel.EnsureRigidbody(); }
        Check(Mathf.Approximately((float)forceField.GetValue(wheel), original * 2f), "RC1 EnsureRigidbody and repeated application do not accumulate");
        var clone = Object.Instantiate(wheel.gameObject).GetComponent<RCR2Port.RoverPortPoweredPart>();
        clone.ApplyCustomParams(); clone.EnsureRigidbody();
        Check(Mathf.Approximately((float)forceField.GetValue(clone), original * 2f), "clone retains authored multiplier baseline");
        Object.DestroyImmediate(clone.gameObject);
        wheel.RemoveCustomParam(PartPhysicsParameters.PowerKey);
        Check(Mathf.Approximately((float)forceField.GetValue(wheel), original), "removing multiplier restores force");
        Remove(wheel);

        var spring = (Spring)Native("Assets/GameObject/Part_Spring_01_SET.prefab", new Vector3(55, 0, 0));
        spring.DeserializeCustomParams("Spring.m_stiffness:400;Spring.m_damping:30;PowerMultiplier:2");
        var neighbour = BpirBox(new Vector3(55, 1, 0), Vector3.one);
        var springJoint = spring.CustomConnectToPart(neighbour);
        Check(springJoint is SpringJoint sj ? Mathf.Approximately(sj.spring, 800f) && Mathf.Approximately(sj.damper, 60f) :
            springJoint is ConfigurableJoint cj && Mathf.Approximately(cj.linearLimitSpring.spring, 800f) && Mathf.Approximately(cj.linearLimitSpring.damper, 60f),
            "source spring stiffness, damping and multiplier reach the actual joint");
        owner.TryRemovePart(neighbour); Object.DestroyImmediate(neighbour.gameObject); Object.DestroyImmediate(spring.gameObject);

        var part = BpirBox(new Vector3(60, 0, 0), Vector3.one);
        part.rigidbody.useGravity = true; part.rigidbody.drag = 0.2f;
        part.SetCustomParam(PartPhysicsParameters.GravityKey, 0f); part.SetCustomParam(PartPhysicsParameters.DragKey, 0.7f);
        Check(!part.rigidbody.useGravity && Mathf.Approximately(part.rigidbody.drag, 0.7f), "live gravity and drag controls apply");
        part.RemoveCustomParam(PartPhysicsParameters.GravityKey); part.RemoveCustomParam(PartPhysicsParameters.DragKey);
        Check(part.rigidbody.useGravity && Mathf.Approximately(part.rigidbody.drag, 0.2f), "removing overrides restores body defaults");
        part.SetCustomParam(PartPhysicsParameters.OffsetXKey, 0.2f);
        GridlessBuild.SetOffset(part, new Vector2(0.7f, -0.4f));
        Check(Mathf.Approximately(part.GetCustomParam(PartPhysicsParameters.OffsetXKey), 0.7f), "toolbar movement and parameter offset stay synchronized");
        part.RemoveCustomParam(PartPhysicsParameters.OffsetXKey);
        Check(GridlessBuild.GetOffset(part).x == 0f && Mathf.Approximately(GridlessBuild.GetOffset(part).y, -0.4f), "removing X override retains Y offset");
        var gravityBody = part.gameObject.AddComponent<RCR2Port.RoverPortGravityBody>();
        gravityBody.Configure(part.rigidbody, 0.5f); part.rigidbody.useGravity = false;
        Call(gravityBody, "FixedUpdate");
        Check(part.rigidbody.GetAccumulatedForce().sqrMagnitude == 0f, "ported gravity correction respects disabled gravity");
        var memberType = typeof(INPartParameterPanel).GetNestedType("Member", BindingFlags.NonPublic);
        var members = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(memberType));
        typeof(INPartParameterPanel).GetMethod("CollectMembers", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { part, members });
        object gravity = members.Cast<object>().Single(m => (string)memberType.GetField("Key").GetValue(m) == PartPhysicsParameters.GravityKey);
        Check((bool)memberType.GetField("IsBool").GetValue(gravity), "gravity menu row is boolean");
        Check((string)memberType.GetField("Key").GetValue(members[0]) == PartPhysicsParameters.GravityKey, "common controls appear first");
        owner.TryRemovePart(part); Object.DestroyImmediate(part.gameObject);
    }

    private static void BpirCollisions()
    {
        var settings = INUserSettings.Instance.GeneralSettings;
        settings.NoGridMode = true; settings.IgnoreGridlessOverlap = true;
        var a = BpirBox(new Vector3(100, 0, 0), Vector3.one);
        var b = BpirBox(new Vector3(100.4f, 0, 0), Vector3.one);
        var c = BpirBox(new Vector3(103, 0, 0), Vector3.one);
        var ground = new GameObject("BPIR test terrain").AddComponent<BoxCollider>();
        ground.transform.position = new Vector3(100, -0.4f, 0);
        Collider ac = a.GetComponent<Collider>(), bc = b.GetComponent<Collider>(), cc = c.GetComponent<Collider>();
        var support = new GameObject("Distant compound support").AddComponent<BoxCollider>();
        support.transform.SetParent(a.transform, false); support.transform.localPosition = Vector3.up * 3;
        GridlessOverlapCollision.Apply(owner);
        Check(GamePhysics.IsIgnoredPair(ac, bc) && Physics.GetIgnoreCollision(ac, bc), "initial penetration registered in backend and PhysX");
        Check(GamePhysics.IsIgnoredPair(support, bc), "overlap ignores the full compound part, including its support");
        Check(!GamePhysics.IsIgnoredPair(ac, cc) && !GamePhysics.IsIgnoredPair(ac, ground), "separate parts and terrain remain collidable");
        c.transform.position = b.transform.position; Physics.SyncTransforms();
        Check(!GamePhysics.IsIgnoredPair(ac, cc), "later contact is not disabled by a frame loop");
        foreach (var p in new[] { a, b, c }) { owner.TryRemovePart(p); Object.DestroyImmediate(p.gameObject); }
        Object.DestroyImmediate(ground.gameObject);

        settings.IgnoreGridlessOverlap = false;
        a = BpirBox(new Vector3(100, 0, 0), Vector3.one); b = BpirBox(new Vector3(100.2f, 0, 0), Vector3.one);
        GridlessOverlapCollision.Apply(owner);
        Check(!GamePhysics.IsIgnoredPair(a.GetComponent<Collider>(), b.GetComponent<Collider>()), "disabled switch keeps overlap collisions");
        settings.IgnoreGridlessOverlap = true; settings.NoGridMode = false;
        GridlessOverlapCollision.Apply(owner);
        Check(!GamePhysics.IsIgnoredPair(a.GetComponent<Collider>(), b.GetComponent<Collider>()), "grid mode keeps its existing collision behavior");
        b.SetCustomParam(PartPhysicsParameters.LayerKey, 2f);
        GridlessOverlapCollision.Apply(owner);
        Check(GamePhysics.IsIgnoredPair(a.GetComponent<Collider>(), b.GetComponent<Collider>()), "distinct structure layers remain independent");
        foreach (var p in new[] { a, b }) { owner.TryRemovePart(p); Object.DestroyImmediate(p.gameObject); }

        settings.NoGridMode = true;
        a = BpirBox(new Vector3(100, 0, 0), Vector3.one); b = BpirBox(new Vector3(101, 0, 0), Vector3.one);
        GridlessOverlapCollision.Apply(owner);
        Check(!GamePhysics.IsIgnoredPair(a.GetComponent<Collider>(), b.GetComponent<Collider>()), "touching faces without penetration keep collisions");
        b.transform.position = new Vector3(101.2f, 0, 0); a.transform.localScale = new Vector3(2, 1, 1);
        GridlessOverlapCollision.Apply(owner);
        Check(GamePhysics.IsIgnoredPair(a.GetComponent<Collider>(), b.GetComponent<Collider>()), "nonuniformly scaled actual shapes determine overlap");
        foreach (var p in new[] { a, b }) { owner.TryRemovePart(p); Object.DestroyImmediate(p.gameObject); }

        settings.NoGridMode = true;
        a = BpirBox(new Vector3(100, 0, 0), new Vector3(3, 0.1f, 1));
        b = BpirBox(new Vector3(100, 0.4f, 0), new Vector3(3, 0.1f, 1));
        a.transform.rotation = b.transform.rotation = Quaternion.Euler(0, 0, 45);
        GridlessOverlapCollision.Apply(owner);
        Check(a.GetComponent<Collider>().bounds.Intersects(b.GetComponent<Collider>().bounds) &&
            !GamePhysics.IsIgnoredPair(a.GetComponent<Collider>(), b.GetComponent<Collider>()), "rotated bounds intersection without shape penetration is not ignored");
        b.transform.position = a.transform.position;
        GridlessOverlapCollision.Apply(owner);
        Check(GamePhysics.IsIgnoredPair(a.GetComponent<Collider>(), b.GetComponent<Collider>()), "rotated shapes with penetration are ignored");
        foreach (var p in new[] { a, b }) { owner.TryRemovePart(p); Object.DestroyImmediate(p.gameObject); }
    }
}
