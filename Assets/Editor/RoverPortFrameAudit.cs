using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Verifies the frame alignment of a ported part against the source catalog: every node must end
/// up at the pose the source data describes once the two frame offsets are applied - the ME tree
/// takes the model offset (rotation centre minus grid shift) and the PE tree takes that offset
/// minus the ME's CenterOfElement, because RCR2 authors the physics prefab around the centre.
/// A regression here shows up as the run-time artwork jumping away from the build view the
/// moment the contraption starts (the flag, chainsaw, ski wheel, cockpits and deer wheel were
/// off by half a cell before the physics-tree offset was introduced).
///
/// The two root entries themselves are skipped: the port maps them onto the part root transform,
/// whose position the game sets from the grid - the prefab root positions are authoring
/// leftovers that the source overrides at instantiation (proved by the grid audit passing with
/// them dropped: honouring them would shove the build view off the cells).
///
/// Run with: Unity -batchmode -quit -executeMethod RoverPortFrameAudit.Run
/// Report: Builds/QA/frame-alignment.txt
/// </summary>
public static class RoverPortFrameAudit
{
    private const float PositionTolerance = 0.0005f;

    public static void Run()
    {
        string reportPath = Path.Combine(Application.dataPath, "..", "Builds", "QA", "frame-alignment.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        var lines = new List<string>();
        int checkedParts = 0, checkedNodes = 0, failures = 0;

        var host = new GameObject("FrameAuditHost");
        try
        {
            foreach (GameObject proto in RCR2Port.RoverPortCatalog.Instance.Parts)
            {
                RCR2Port.RoverPortPart part = proto.GetComponent<RCR2Port.RoverPortPart>();
                if (part == null)
                {
                    continue;
                }
                GameObject clone = Object.Instantiate(proto);
                clone.transform.SetParent(host.transform, false);
                clone.transform.position = Vector3.zero;
                clone.transform.rotation = Quaternion.identity;
                clone.SetActive(true);

                var dataField = typeof(RCR2Port.RoverPortPart).GetField("m_portData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var portData = (RCR2Port.RoverPortPartData)dataField.GetValue(part);
                if (portData == null || portData.nodes == null)
                {
                    Object.DestroyImmediate(clone);
                    continue;
                }

                var nodeIds = (Dictionary<string, Transform>)typeof(RCR2Port.RoverPortPart)
                    .GetField("m_nodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(part);
                var nodeData = new Dictionary<string, RCR2Port.RoverPortNodeData>();
                foreach (RCR2Port.RoverPortNodeData node in portData.nodes)
                {
                    if (node != null && !string.IsNullOrEmpty(node.id))
                    {
                        nodeData[node.id] = node;
                    }
                }

                Vector3 modelOffset = ToVector3(portData.rotation_center) - ToVector3(portData.grid != null ? portData.grid.shift : null);
                Vector3 physicsOffset = modelOffset - ToVector3(portData.center);

                int matched = 0;
                float worst = 0f;
                string worstNode = "";
                foreach (KeyValuePair<string, RCR2Port.RoverPortNodeData> pair in nodeData)
                {
                    // The two roots map onto the part root transform by design; their stored
                    // positions are prefab-editor leftovers the source never uses.
                    if (pair.Key == portData.model_root || pair.Key == portData.physics_root)
                    {
                        continue;
                    }
                    Transform built;
                    if (!nodeIds.TryGetValue(pair.Key, out built) || built == null) continue;
                    RCR2Port.RoverPortNodeData source = pair.Value;

                    // Which root the chain belongs to decides the frame offset.
                    string rootId = ChainRoot(nodeData, source);
                    Vector3 offset = rootId == portData.physics_root ? physicsOffset : modelOffset;

                    // The port builds each node under its parent at the source local position; the
                    // frame offset enters only at the root children, so the expected local position
                    // of a root child is source.position - offset, everything deeper keeps its own.
                    string parentId = source.parent;
                    bool isRootChild = parentId == portData.model_root || parentId == portData.physics_root;
                    Vector3 expectedLocal = ToVector3(source.position);
                    if (isRootChild)
                    {
                        expectedLocal -= offset;
                    }
                    float error = Vector3.Distance(built.localPosition, expectedLocal);
                    matched++;
                    checkedNodes++;
                    if (error > worst)
                    {
                        worst = error;
                        worstNode = source.name + "/" + pair.Key.Substring(pair.Key.LastIndexOf(':') + 1);
                    }
                }

                if (matched > 0)
                {
                    checkedParts++;
                    bool pass = worst <= PositionTolerance;
                    if (!pass)
                    {
                        failures++;
                    }
                    lines.Add($"{proto.name}: nodes={matched} worst={worst:F4} ({worstNode}) => {(pass ? "PASS" : "FAIL")}");
                }

                Object.DestroyImmediate(clone);
            }
        }
        finally
        {
            Object.DestroyImmediate(host);
        }

        lines.Insert(0, $"frame alignment audit: parts={checkedParts} nodes={checkedNodes} failures={failures}");
        File.WriteAllLines(reportPath, lines);
        Debug.Log($"[FrameAudit] parts={checkedParts} nodes={checkedNodes} failures={failures}");
        if (failures > 0)
        {
            throw new System.Exception($"frame alignment audit failures: {failures}");
        }
    }

    /// <summary>Walks the parent chain to find which root the node hangs from.</summary>
    private static string ChainRoot(Dictionary<string, RCR2Port.RoverPortNodeData> nodeData,
        RCR2Port.RoverPortNodeData node)
    {
        var visiting = new HashSet<string>();
        string current = node.id;
        while (!string.IsNullOrEmpty(current) && visiting.Add(current))
        {
            RCR2Port.RoverPortNodeData data;
            if (!nodeData.TryGetValue(current, out data) || data == null || string.IsNullOrEmpty(data.parent))
            {
                return current;
            }
            current = data.parent;
        }
        return null;
    }

    private static Vector3 ToVector3(float[] value)
    {
        if (value == null || value.Length < 2)
        {
            return Vector3.zero;
        }
        return new Vector3(value[0], value[1], value.Length > 2 ? value[2] : 0f);
    }
}
