using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class Box3DMeshCache : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public Mesh source;
        public Vector3[] vertices;
        public int[] triangles;
    }
    public List<Entry> entries = new List<Entry>();
    private static Box3DMeshCache loaded;
    private Dictionary<Mesh, Entry> lookup;

    public static void Read(Mesh mesh, out Vector3[] vertices, out int[] triangles)
    {
        if (mesh.isReadable) { vertices = mesh.vertices; triangles = mesh.triangles; return; }
        if (!loaded) loaded = Resources.Load<Box3DMeshCache>("Box3DPhysicsMeshCache");
        if (loaded)
        {
            if (loaded.lookup == null)
            {
                loaded.lookup = new Dictionary<Mesh, Entry>();
                foreach (var e in loaded.entries) if (e.source) loaded.lookup[e.source] = e;
            }
            if (loaded.lookup.TryGetValue(mesh, out var entry))
            { vertices = entry.vertices; triangles = entry.triangles; return; }
        }
        throw new NotSupportedException("Missing Box3D CPU geometry for unreadable mesh '" + mesh.name +
            "'. Run Tools > Box3D > Bake Mesh Cache, or enable Read/Write for this mesh.");
    }
}
