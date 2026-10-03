using UnityEngine;

/// <summary>
/// Supplies CPU-side geometry for <see cref="MeshCollider"/>s to the Jolt backend.
/// </summary>
/// <remarks>
/// The Jolt backend needs vertices and indices for the same meshes as Box3D, and this project has
/// exactly one baked cache for that (<c>Resources/Box3DPhysicsMeshCache</c>, produced by
/// <c>Tools &gt; Box3D &gt; Bake Mesh Cache</c>). Delegating rather than adding a second cache keeps a
/// single source of truth and means the Jolt backend works wherever Box3D already does.
/// </remarks>
public static class JoltMeshCache
{
    /// <summary>
    /// Returns the mesh's vertices and triangle indices, reading the mesh directly when it is
    /// read/write enabled and falling back to the baked cache otherwise.
    /// </summary>
    public static void Read(Mesh mesh, out Vector3[] vertices, out int[] triangles)
        => Box3DMeshCache.Read(mesh, out vertices, out triangles);
}
