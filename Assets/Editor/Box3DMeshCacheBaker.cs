using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class Box3DMeshCacheBaker : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) { Bake(); }

    [MenuItem("Tools/Box3D/Bake Mesh Cache")]
    public static void Bake()
    {
        const string path = "Assets/Resources/Box3DPhysicsMeshCache.asset";
        var cache = AssetDatabase.LoadAssetAtPath<Box3DMeshCache>(path);
        if (!cache) { cache = ScriptableObject.CreateInstance<Box3DMeshCache>(); AssetDatabase.CreateAsset(cache, path); }
        cache.entries.Clear();
        var visited = new HashSet<Mesh>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (!prefab) continue;
            foreach (var collider in prefab.GetComponentsInChildren<MeshCollider>(true))
            {
                Mesh mesh = collider.sharedMesh;
                if (!mesh || mesh.isReadable || !visited.Add(mesh)) continue;
                using (var meshData = MeshUtility.AcquireReadOnlyMeshData(mesh))
                using (var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
                {
                    var data = meshData[0]; data.GetVertices(vertices);
                    var triangles = new List<int>();
                    for (int sub = 0; sub < data.subMeshCount; sub++)
                    {
                        var descriptor = data.GetSubMesh(sub);
                        if (descriptor.topology != MeshTopology.Triangles) continue;
                        using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                        {
                            data.GetIndices(indices, sub);
                            triangles.AddRange(indices.ToArray());
                        }
                    }
                    if (triangles.Count == 0) continue;
                    cache.entries.Add(new Box3DMeshCache.Entry { source = mesh, vertices = vertices.ToArray(), triangles = triangles.ToArray() });
                }
            }
        }
        EditorUtility.SetDirty(cache); AssetDatabase.SaveAssets();
        Debug.Log("Box3D mesh cache: " + cache.entries.Count + " unreadable collision meshes");
    }
}
