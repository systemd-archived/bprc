using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BuildInterfaceAudit
{
    private static bool MoveExtractedPart(Contraption c, BasePart part, int x, int y, Vector2 offset)
    {
        return (bool)typeof(Contraption).GetMethod("TryMoveBuildPart", Private).Invoke(c,
            new object[] { part, x, y, offset, false, null, null });
    }
    private static bool JointBetween(Contraption c, BasePart a, BasePart b)
    {
        foreach (var link in c.JointMap)
            if ((link.partA == a && link.partB == b) || (link.partA == b && link.partB == a)) return true;
        return false;
    }

    private static void SelectionIsolation()
    {
        bool oldGrid = settings.GeneralSettings.NoGridMode, oldMerge = settings.GeneralSettings.MergeGridlessGroups;
        FieldInfo runtimeField = typeof(INSettings).GetField("s_runtimeSettings", Static);
        FieldInfo dataField = typeof(WPFMonoBehaviour).GetField("s_gameData", Static);
        object oldRuntime = runtimeField.GetValue(null), oldData = dataField.GetValue(null);
        var data = ScriptableObject.CreateInstance<GameData>(); data.m_jointConnectionStrengthWeak = 1000f;
        Type itemType = typeof(INSettings).GetNestedType("SettingData", BindingFlags.NonPublic);
        Type scopeType = typeof(INSettings).GetNestedType("SettingScope", BindingFlags.NonPublic);
        Type containerType = typeof(INSettings).GetNestedType("SettingDataContainer", BindingFlags.NonPublic);
        object container = Activator.CreateInstance(containerType, new object[] { Enum.GetNames(typeof(INFeature)).Length });
        foreach (INFeature feature in Enum.GetValues(typeof(INFeature)))
            containerType.GetMethod("SetData").Invoke(container, new[] { (object)feature,
                Activator.CreateInstance(itemType, new object[] { Enum.ToObject(scopeType, 3),
                    feature == INFeature.ConnectionStrength ? (Variant)new Variant<float>(1f) : new Variant<bool>(false) }) });
        runtimeField.SetValue(null, container); dataField.SetValue(null, data);
        try
        {
            settings.GeneralSettings.NoGridMode = true; settings.GeneralSettings.MergeGridlessGroups = true;
            var c = MergeMachine("ExtractionRemerge");
            BasePart left = MergePart(c, 0, 7, Vector2.zero), selected = MergePart(c, 1, 7, Vector2.right),
                right = MergePart(c, 2, 7, Vector2.right * 2f);
            Check(GridlessSelectionExtract.TryExtract(c, new[] { selected }, out int group, out _),
                "a selected middle part splits from its original independent frame");
            Check(GridlessBuild.GetLatticeId(selected) != GridlessBuild.GetLatticeId(left),
                "extracted parts receive a fresh topology identity before any movement");
            BuildMachineTransform.Apply(c, new BuildMachineTransform { translation = Vector2.right * 2f }, group);
            Check(GridlessGroupMerge.TryMerge(c, group) == 1 && c.FindPartAt(0, 0, selected) == null &&
                c.FindPartAt(2, 0, selected) == null,
                "moving and remerging a split subset never restores its old logical neighbors");
            var running = MergeMachine("ExtractionRemergeRuntime");
            foreach (BasePart original in c.Parts)
            {
                BasePart copy = Object.Instantiate(original); objects.Add(copy.gameObject);
                copy.transform.SetParent(running.transform, false); copy.contraption = running; running.Parts.Add(copy);
                Call(running, "RestoreSimulationPartPose", copy); copy.EnsureRigidbody();
                running.SetPartMap(copy.m_coordX, copy.m_coordY, copy.GenerationIndex, copy);
            }
            var pairs = new HashSet<(BasePart, BasePart)>();
            MethodInfo connect = typeof(Contraption).GetMethod("ConnectNativeGridPart", Private);
            foreach (BasePart part in running.Parts) connect.Invoke(running, new object[] { part, pairs });
            GridlessGroupMerge.ConnectRuntime(running);
            var links = (List<Contraption.JointConnection>)typeof(Contraption).GetField("m_jointMap", Private).GetValue(running);
            Check(links.Count == 1 && JointBetween(running, running.Parts[1], running.Parts[2]) &&
                !JointBetween(running, running.Parts[0], running.Parts[1]),
                "remerged simulation connects the actual new contact and leaves the old neighbor disconnected");

            var overlapping = MergeMachine("ExtractedEnclosureDrag");
            BasePart outer = MergePart(overlapping, 0, 5, Vector2.zero),
                inner = MergePart(overlapping, 0, 5, Vector2.zero);
            MergePart(overlapping, 1, 5, Vector2.right);
            outer.enclosedPart = inner; Call(overlapping, "RebuildLayerMap");
            Check(GridlessSelectionExtract.TryExtract(overlapping, new[] { inner }, out _, out _),
                "enclosed part can be extracted without moving either same-cell part");
            Check(MoveExtractedPart(overlapping, outer, 0, 0, new Vector2(0.1f, 0f)) &&
                MoveExtractedPart(overlapping, inner, 0, 0, new Vector2(-0.1f, 0f)),
                "both independent same-cell parts accept fractional drags");
            Check(MoveExtractedPart(overlapping, inner, 3, 0, Vector2.zero) &&
                overlapping.FindPartAt(0, 0, outer) == outer && overlapping.FindPartAt(0, 0) == outer,
                "moving the mapped same-cell part retains the surviving original part in the cell map");

            var ropes = MergeMachine("ExtractedRopeQuery");
            BasePart rope = MergePart(ropes, 0, 6, Vector2.zero), neighbor = MergePart(ropes, 1, 6, Vector2.right);
            rope.m_partType = neighbor.m_partType = BasePart.PartType.Rope;
            GridlessSelectionExtract.TryExtract(ropes, new[] { rope }, out _, out _);
            Check(!ropes.IsPartTypeAt(1, 0, BasePart.PartType.Rope, neighbor.m_gridRotation, rope) &&
                ropes.IsPartTypeAt(1, 0, BasePart.PartType.Rope, neighbor.m_gridRotation, neighbor),
                "rope auto-alignment type queries stay within the requested independent group");
        }
        finally
        {
            settings.GeneralSettings.NoGridMode = oldGrid; settings.GeneralSettings.MergeGridlessGroups = oldMerge;
            runtimeField.SetValue(null, oldRuntime); dataField.SetValue(null, oldData); Object.DestroyImmediate(data);
        }
    }
}
