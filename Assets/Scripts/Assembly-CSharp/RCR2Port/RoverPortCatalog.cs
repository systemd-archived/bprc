using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    public sealed class RoverPortCatalog
    {
        public const int FirstPartIndex = 2000;

        private const string CatalogResourcePath = "RCR2Ported/rcr2_catalog";
        private const string TextureResourceRoot = "RCR2Ported/Textures/";

        private static RoverPortCatalog s_instance;
        // (Sorting orders in the catalog are MAIN's own signed values, not a re-based 0..N rank: a
        // dense rank put every ported renderer into the 0..4 band the level's background and the
        // native parts occupy, so a ported wheel's layers interleaved with the background.)

        private readonly List<GameObject> m_parts = new List<GameObject>();
        private readonly Dictionary<string, GameObject> m_partsById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<int, GameObject> m_partsByIndex = new Dictionary<int, GameObject>();
        private readonly Dictionary<string, Texture2D> m_textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> m_materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        /// <summary>
        /// Shader the part being built asks for, taken from the catalog's material
        /// contract. MAIN builds every imported Android sprite and icon with Unity's
        /// built-in Sprites/Default shader, whereas BPRC's IngameAtlas / UIPartButtonAtlas
        /// templates use _Custom/PreAlpha_Unlit_ColorTransparent_Geometry; the two blend
        /// alpha differently, so the part's own shader has to win.
        /// </summary>
        private string m_partShader;
        private string m_iconShader;
        private string m_currentPartAndroidClass;

        private GameObject m_host;
        private GameObject m_iconHost;

        private RoverPortCatalog()
        {
            Load();
        }

        public static RoverPortCatalog Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new RoverPortCatalog();
                }
                return s_instance;
            }
        }

        public static bool HasParts
        {
            get { return Instance.m_parts.Count > 0; }
        }

        public IReadOnlyList<GameObject> Parts
        {
            get { return m_parts; }
        }

        public List<BasePart> GetPartComponents()
        {
            List<BasePart> result = new List<BasePart>(m_parts.Count);
            foreach (GameObject part in m_parts)
            {
                BasePart component = part == null ? null : part.GetComponent<BasePart>();
                if (component != null)
                {
                    result.Add(component);
                }
            }
            return result;
        }

        public GameObject GetPart(int index)
        {
            GameObject result;
            return m_partsByIndex.TryGetValue(index, out result) ? result : null;
        }

        public bool ContainsPartIndex(int index)
        {
            return index >= FirstPartIndex && m_partsByIndex.ContainsKey(index);
        }

        public GameObject GetPart(string id)
        {
            GameObject result;
            return m_partsById.TryGetValue(id, out result) ? result : null;
        }

        public Texture2D GetTexture(string guid)
        {
            Texture2D result;
            return m_textures.TryGetValue(guid, out result) ? result : null;
        }

        /// <summary>
        /// Destroys a leftover catalog host from a previous play session. A domain reload clears
        /// the static singleton but leaves the DontDestroyOnLoad GameObjects alive, so without this
        /// every session added another host with another full set of part templates.
        /// </summary>
        private static void DestroyExistingHost(string hostName)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate == null || candidate.name != hostName || !candidate.scene.IsValid())
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(candidate);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }
            }
        }

        private void Load()
        {
            try
            {
                TextAsset textAsset = Resources.Load<TextAsset>(CatalogResourcePath);
                if (textAsset == null)
                {
                    return;
                }
                RoverPortCatalogData catalog = JsonUtility.FromJson<RoverPortCatalogData>(textAsset.text);
                if (catalog == null || catalog.parts == null)
                {
                    return;
                }

                // A domain reload clears s_instance, so Load() runs again at the start of every
                // play session - while the previous session's host, DontDestroyOnLoad, is still
                // alive. The port used to leave it behind: two hosts (and two full sets of part
                // templates, each carrying the catalog it was built from) accumulated, which is
                // why a live probe could still read a template's *old* sorting orders after the
                // catalog had been regenerated. Destroy the leftovers first, so exactly one
                // catalog exists and it is always the one on disk.
                DestroyExistingHost("[RCR2 Ported Parts]");
                DestroyExistingHost("[RCR2 Ported Part Icons]");

                m_host = new GameObject("[RCR2 Ported Parts]");
                m_iconHost = new GameObject("[RCR2 Ported Part Icons]");
                m_iconHost.transform.position = new Vector3(10000f, 10000f, 0f);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.DontDestroyOnLoad(m_host);
                    UnityEngine.Object.DontDestroyOnLoad(m_iconHost);
                }
                m_host.SetActive(false);

                foreach (RoverPortTextureData texture in catalog.textures ?? new RoverPortTextureData[0])
                {
                    string resourceName = System.IO.Path.GetFileNameWithoutExtension(texture.canonical_file);
                    Texture2D loaded = Resources.Load<Texture2D>(TextureResourceRoot + resourceName);
                    if (loaded != null)
                    {
                        m_textures[texture.guid] = loaded;
                    }
                }

                int displayIndex = FirstPartIndex;
                foreach (RoverPortPartData partData in catalog.parts)
                {
                    if (partData == null || string.IsNullOrEmpty(partData.id))
                    {
                        continue;
                    }
                    try
                    {
                        GameObject part = BuildPart(partData, displayIndex++);
                        if (part != null)
                        {
                            if (m_partsById.ContainsKey(partData.id))
                            {
                                throw new InvalidOperationException("Duplicate part id: " + partData.id);
                            }
                            m_parts.Add(part);
                            m_partsById[partData.id] = part;
                            RoverPortPart portPart = part.GetComponent<RoverPortPart>();
                            if (portPart != null)
                            {
                                if (m_partsByIndex.ContainsKey(portPart.Index))
                                {
                                    throw new InvalidOperationException("Duplicate part index: " + portPart.Index);
                                }
                                m_partsByIndex[portPart.Index] = part;
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[RCR2Port] Failed to build part '" + partData.id + "': " + exception);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[RCR2Port] Failed to build the ported RoverCraft 2 catalog: " + exception);
            }
        }

        private GameObject BuildPart(RoverPortPartData data, int partIndex)
        {
            GameObject root = new GameObject("RCR2_" + data.id);
            root.transform.SetParent(m_host.transform, false);
            root.SetActive(false);

            m_partShader = data.material == null ? null : data.material.shader;
            m_iconShader = data.icon_material == null ? null : data.icon_material.shader;
            m_currentPartAndroidClass = data.android_class;

            RoverPortPart part;
            if (string.Equals(data.category, "wheel", StringComparison.Ordinal) && data.motion != null && data.motion.passive)
            {
                // A wheel the source builds without a motor rolls with the vehicle: it is a plain wheel
                // part with the source's own bodies and joints, not a drive wheel
                // (see Tools/RCR2/PatchPassiveWheels.py).
                part = root.AddComponent<RoverPortPart>();
            }
            else if (string.Equals(data.category, "cockpit", StringComparison.Ordinal))
            {
                part = root.AddComponent<RoverPortCockpitPart>();
            }
            else if (string.Equals(data.category, "gyroscope", StringComparison.Ordinal))
            {
                // RoverCraft's gyroscope is not an engine. Its data says motion.kind "engine" because it
                // sits on MAIN's Engine class, and the powered branch below then handed it the stock
                // engine drive, so the part pushed the rover along instead of turning it. The source
                // only ever spins the whole rover while one of the two pedals is held (PartSPhGyro), so
                // it is a plain part with RoverPortGyro on it.
                part = root.AddComponent<RoverPortGyroPart>();
                root.AddComponent<RoverPortGyro>();
            }
            else if (data.motion != null && string.Equals(data.motion.kind, "missile", StringComparison.Ordinal))
            {
                // The RC3 missile launcher (part_rc3_missile, RC3 part 70012): a one-shot weapon
                // whose firing behaviour and projectile art live in RoverPortMissilePart.
                part = root.AddComponent<RoverPortMissilePart>();
            }
            else if (data.motion != null && string.Equals(data.motion.kind, "battery", StringComparison.Ordinal))
            {
                // An RC3 battery feeds BPLE's engine power pool; an RCR2 battery keeps the charge
                // model its own audits exercise (see RoverPortPowerCellPart).
                part = RC3Port.RC3PortCatalog.IsRC3Index(partIndex)
                    ? (RoverPortPart)root.AddComponent<RoverPortPowerCellPart>()
                    : root.AddComponent<RoverPortBatteryPart>();
            }
            else if (data.motion != null &&
                     ((string.Equals(data.motion.kind, "wheel", StringComparison.Ordinal) && !data.motion.passive) ||
                      string.Equals(data.motion.kind, "engine", StringComparison.Ordinal) ||
                      string.Equals(data.motion.kind, "propeller", StringComparison.Ordinal) ||
                      string.Equals(data.motion.kind, "thruster", StringComparison.Ordinal)))
            {
                part = root.AddComponent<RoverPortPoweredPart>();
            }
            else
            {
                part = root.AddComponent<RoverPortPart>();
            }

            Vector3 rootOffset = RootOffset(data);
            // The physics prefab is authored around the ME's CenterOfElement - the source spawns the
            // physics element at that point - so the physics frame offset is the model offset minus
            // the center. See BuildNodes for how the two offsets are applied.
            Vector3 physicsOffset = rootOffset - ToVector3(data.center, Vector3.zero);
            Dictionary<string, Transform> nodes = BuildNodes(root.transform, data, rootOffset, physicsOffset);
            List<Renderer> physicsRenderers = BuildRenderers(nodes, data.renderers);
            List<Renderer> constructionRenderers = BuildRenderers(nodes, data.construction_renderers);
            part.ConfigureVisuals(constructionRenderers, physicsRenderers);
            PhysicMaterial physicMaterial = CreateFrictionMaterial(data);
            // Per part, so every collider that shares a source material shares one object.
            Dictionary<string, PhysicMaterial> colliderMaterials = new Dictionary<string, PhysicMaterial>(StringComparer.Ordinal);
            BuildColliders(nodes, data, physicMaterial, colliderMaterials, physicsOffset);
            BuildTrackBand(nodes, data);
            BuildTurbine(nodes, data, part);
            RoverPortParticles.Build(nodes, data, GetTexture);

            GameObject icon = BuildIcon(data);
            part.Configure(data, partIndex, nodes, icon == null ? null : icon.GetComponent<RoverPortIconSprite>());
            return root;
        }

        /// <summary>
        /// Offset subtracted from the RCR2 authoring frame to reach the part root: the rotation
        /// centre minus the half-cell grid alignment recovered from the source (see
        /// Tools/RCR2/GridAlignment.py). Subtracting this moves the artwork onto BPLE cells.
        /// Parts whose alignment is not confidently known keep a zero grid shift.
        /// </summary>
        private static Vector3 RootOffset(RoverPortPartData data)
        {
            Vector3 offset = ToVector3(data.rotation_center, Vector3.zero);
            if (data.grid != null)
            {
                offset -= ToVector3(data.grid.shift, Vector3.zero);
            }
            return offset;
        }

        private static Dictionary<string, Transform> BuildNodes(Transform root, RoverPortPartData data, Vector3 rootOffset, Vector3 physicsOffset)
        {
            Dictionary<string, Transform> nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
            Dictionary<string, RoverPortNodeData> nodeData = new Dictionary<string, RoverPortNodeData>(StringComparer.Ordinal);
            foreach (RoverPortNodeData node in data.nodes ?? new RoverPortNodeData[0])
            {
                if (node != null && !string.IsNullOrEmpty(node.id))
                {
                    nodeData[node.id] = node;
                }
            }

            root.gameObject.name = data.asset_name;
            nodes[data.model_root] = root;
            nodes[data.physics_root] = root;

            foreach (string id in nodeData.Keys)
            {
                ResolveNode(id, root, nodes, nodeData, new HashSet<string>(StringComparer.Ordinal));
            }

            if (rootOffset.sqrMagnitude > 0.000001f || physicsOffset.sqrMagnitude > 0.000001f)
            {
                // The ME prefab is authored relative to the part origin, but the PE prefab is authored
                // relative to the ME's CenterOfElement, so the physics subtree carries that center on
                // top of the shared grid offset. Shifting both trees by the model offset alone left
                // the run-time artwork up to half a cell away from the build view on every part whose
                // center is non-zero (flag, chainsaw, ski wheel, cockpits, deer wheel).
                HashSet<Transform> physicsChildren = CollectDirectChildren(nodes, nodeData, data.physics_root);
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    child.localPosition -= physicsChildren.Contains(child) ? physicsOffset : rootOffset;
                }
                AdjustRootJointAnchors(data, physicsOffset);
            }
            return nodes;
        }

        /// <summary>
        /// Transforms that hang directly under the physics root: the subtree the source authors around
        /// the ME's CenterOfElement, which takes the center translation in addition to the grid offset.
        /// </summary>
        private static HashSet<Transform> CollectDirectChildren(Dictionary<string, Transform> nodes, Dictionary<string, RoverPortNodeData> nodeData, string rootId)
        {
            HashSet<Transform> children = new HashSet<Transform>();
            foreach (KeyValuePair<string, RoverPortNodeData> pair in nodeData)
            {
                if (string.Equals(pair.Value.parent, rootId, StringComparison.Ordinal))
                {
                    Transform child;
                    if (nodes.TryGetValue(pair.Key, out child) && child != null)
                    {
                        children.Add(child);
                    }
                }
            }
            return children;
        }

        private static void AdjustRootJointAnchors(RoverPortPartData data, Vector3 offset)
        {
            foreach (RoverPortJointData joint in data.joints ?? new RoverPortJointData[0])
            {
                if (joint == null)
                {
                    continue;
                }
                if (joint.node == data.physics_root)
                {
                    SubtractPivot(joint.anchor, offset);
                }
                if (joint.connected_body == data.physics_root)
                {
                    SubtractPivot(joint.connected_anchor, offset);
                }
            }
        }

        private static void SubtractPivot(float[] value, Vector3 offset)
        {
            if (value == null || value.Length < 2)
            {
                return;
            }
            value[0] -= offset.x;
            value[1] -= offset.y;
            if (value.Length > 2)
            {
                value[2] -= offset.z;
            }
        }

        private static Transform ResolveNode(string id, Transform root, Dictionary<string, Transform> nodes, Dictionary<string, RoverPortNodeData> nodeData, HashSet<string> visiting)
        {
            Transform existing;
            if (nodes.TryGetValue(id, out existing))
            {
                return existing;
            }
            RoverPortNodeData data;
            if (!nodeData.TryGetValue(id, out data))
            {
                return root;
            }
            if (!visiting.Add(id))
            {
                return root;
            }

            Transform parent = root;
            if (!string.IsNullOrEmpty(data.parent) && data.parent != id)
            {
                parent = ResolveNode(data.parent, root, nodes, nodeData, visiting);
            }

            GameObject gameObject = new GameObject(string.IsNullOrEmpty(data.name) ? "Node" : data.name);
            gameObject.SetActive(data.active);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = ToVector3(data.position, Vector3.zero);
            gameObject.transform.localRotation = ToQuaternion(data.rotation);
            gameObject.transform.localScale = ToVector3(data.scale, Vector3.one);
            nodes[id] = gameObject.transform;
            visiting.Remove(id);
            return gameObject.transform;
        }

        private List<Renderer> BuildRenderers(Dictionary<string, Transform> nodes, RoverPortRendererData[] renderers)
        {
            List<Renderer> enabledRenderers = new List<Renderer>();
            int minimumOrder = 0;
            if (!string.IsNullOrEmpty(m_currentPartAndroidClass))
                foreach (var entry in renderers ?? System.Array.Empty<RoverPortRendererData>())
                    if (entry != null) minimumOrder = Mathf.Min(minimumOrder, entry.sorting_order);
            foreach (RoverPortRendererData rendererData in renderers ?? new RoverPortRendererData[0])
            {
                Transform node;
                if (rendererData == null || rendererData.sprite == null || !nodes.TryGetValue(rendererData.node, out node))
                {
                    continue;
                }
                Texture2D texture = GetTexture(rendererData.sprite.texture_guid);
                Material material = GetMaterial(rendererData.sprite.texture_guid, "IngameAtlas", "part");
                if (texture == null || material == null)
                {
                    continue;
                }

                RoverPortMeshSprite sprite = node.GetComponent<RoverPortMeshSprite>();
                if (sprite == null)
                {
                    sprite = node.gameObject.AddComponent<RoverPortMeshSprite>();
                }
                Color rendererColor = ToColor(rendererData.color);
                // Android Parts use opaque Sprites/Default materials. Keep the
                // source renderer alpha independent from any atlas/template tint;
                // the source prefab's renderer colors are all (1,1,1,1).
                if (!string.IsNullOrEmpty(m_currentPartAndroidClass))
                {
                    rendererColor.a = 1f;
                }
                sprite.Configure(
                    texture,
                    rendererData.sprite,
                    material,
                    rendererColor,
                    rendererData.flip_x,
                    rendererData.flip_y,
                    string.IsNullOrEmpty(m_currentPartAndroidClass) ? rendererData.sorting_order : rendererData.sorting_order - minimumOrder + 1,
                    rendererData.enabled);
                if (rendererData.enabled) enabledRenderers.Add(node.GetComponent<MeshRenderer>());
            }
            return enabledRenderers;
        }

        private static void BuildColliders(Dictionary<string, Transform> nodes, RoverPortPartData data, PhysicMaterial physicMaterial, Dictionary<string, PhysicMaterial> materialCache, Vector3 physicsOffset)
        {
            Transform transformRoot;
            nodes.TryGetValue(data.physics_root, out transformRoot);
            foreach (RoverPortColliderData colliderData in data.colliders ?? new RoverPortColliderData[0])
            {
                Transform node;
                if (colliderData == null || !nodes.TryGetValue(colliderData.node, out node))
                {
                    continue;
                }
                GameObject gameObject = node.gameObject;
                Vector3 offset = ToVector3(colliderData.offset, Vector3.zero);
                if (node == transformRoot)
                {
                    offset -= physicsOffset;
                }
                // The collider's own material when the catalog carries one (a source Unity
                // prefab's per-collider PhysicMaterial), otherwise the part-level friction
                // material. See RoverPortColliderMaterials.
                PhysicMaterial colliderMaterial = RoverPortColliderMaterials.Resolve(colliderData, data, physicMaterial, materialCache);
                switch (colliderData.type)
                {
                    case "box":
                    {
                        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                        Vector2 size = ToVector2(colliderData.size, Vector2.one);
                        collider.center = offset;
                        collider.size = new Vector3(size.x, size.y, RoverPortColliderMaterials.Depth(colliderData));
                        collider.isTrigger = colliderData.trigger;
                        collider.enabled = colliderData.enabled;
                        collider.sharedMaterial = colliderMaterial;
                        break;
                    }
                    case "circle":
                    {
                        SphereCollider collider = gameObject.AddComponent<SphereCollider>();
                        collider.center = offset;
                        collider.radius = Mathf.Max(0.01f, colliderData.radius);
                        collider.isTrigger = colliderData.trigger;
                        collider.enabled = colliderData.enabled;
                        collider.sharedMaterial = colliderMaterial;
                        break;
                    }
                    case "capsule":
                    {
                        CapsuleCollider collider = gameObject.AddComponent<CapsuleCollider>();
                        Vector2 size = ToVector2(colliderData.size, new Vector2(1f, 2f));
                        collider.center = offset;
                        collider.direction = colliderData.direction == 0 ? 1 : 0;
                        collider.radius = Mathf.Max(0.01f, Mathf.Min(size.x, size.y) * 0.5f);
                        collider.height = Mathf.Max(collider.radius * 2f, Mathf.Max(size.x, size.y));
                        collider.isTrigger = colliderData.trigger;
                        collider.enabled = colliderData.enabled;
                        collider.sharedMaterial = colliderMaterial;
                        break;
                    }
                    case "polygon":
                    case "edge":
                    {
                        if (!BuildPolygonCollider(gameObject, colliderData, offset, colliderMaterial))
                        {
                            Bounds bounds = PolygonBounds(colliderData.points, offset);
                            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                            collider.center = bounds.center;
                            collider.size = new Vector3(Mathf.Max(0.01f, bounds.size.x), Mathf.Max(0.01f, bounds.size.y), RoverPortColliderMaterials.Depth(colliderData));
                            collider.isTrigger = colliderData.trigger;
                            collider.enabled = colliderData.enabled;
                            collider.sharedMaterial = colliderMaterial;
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Adds the rig of a rotating turbine part: the rotor spins one way only while the engine
        /// thrusts and settles back to its resting rotation when it stops, and the nozzle plays its
        /// enable / shutdown animation - see <see cref="RoverPortTurbine"/>.
        /// </summary>
        private static void BuildTurbine(Dictionary<string, Transform> nodes, RoverPortPartData data, RoverPortPart part)
        {
            if (data.id != "part_ice_gyroturbine" && data.id != "part_terra_turbine_basic")
            {
                return;
            }
            RoverPortTurbine turbine = part.gameObject.AddComponent<RoverPortTurbine>();
            turbine.Configure(data, nodes, part as RoverPortPoweredPart);
        }

        /// <summary>
        /// Adds the rubber band of a tracked part. RCR2 builds that band at runtime - the belt link
        /// sprite is referenced by no prefab - so the catalog carries a dedicated band block (see
        /// Tools/RCR2/PatchTrackBand.py) with the link sprite and the chain of link nodes to wrap.
        /// The band keeps its own renderer, independent of the construction/physics sets, so it shows
        /// up in the build view as well as while driving.
        /// </summary>
        private void BuildTrackBand(Dictionary<string, Transform> nodes, RoverPortPartData data)
        {
            if (data.band == null || data.band.sprite == null)
            {
                return;
            }
            Transform host;
            if (!nodes.TryGetValue(data.band.node ?? string.Empty, out host) || host == null)
            {
                return;
            }
            Texture2D texture = GetTexture(data.band.sprite.texture_guid);
            Material material = GetMaterial(data.band.sprite.texture_guid, "IngameAtlas", "part");
            if (texture == null || material == null)
            {
                return;
            }
            RoverPortTrackBand band = host.GetComponent<RoverPortTrackBand>();
            if (band == null)
            {
                band = host.gameObject.AddComponent<RoverPortTrackBand>();
            }
            band.Configure(data.band, nodes, texture, material);
        }

        /// <summary>
        /// The part-level material, for catalogs whose source is not a Unity prefab and which
        /// therefore describe friction as one number on the part, on a scale of their own.
        /// RC1 does not use this any more - its source prefabs carry real PhysicMaterials,
        /// which the catalog now reproduces per collider
        /// (<see cref="RoverPortColliderMaterials"/>). The old note here, that BPLE's own parts
        /// "run on Contraption_PhysMat" and that a driven tyre should therefore be floored at
        /// 0.7, was wrong on both counts: BPLE's own wheels put
        /// Contraption_WheelFriction_PhysMat (dynamic 0.025, static 0, Multiply) on the tyre
        /// and Contraption_PhysMat only on the SupportCollider above it, and MAIN's imported
        /// Android wheels do exactly the same.
        /// </summary>
        private static PhysicMaterial CreateFrictionMaterial(RoverPortPartData data)
        {
            float friction = data.friction > 0f ? data.friction
                : data.motion != null ? data.motion.friction : 0f;
            bool driven = data.motion != null && string.Equals(data.motion.kind, "wheel", StringComparison.Ordinal);
            // Driven parts reach the ground through contact patches, so read their grip on the wheel
            // half of RCR2's scale: 10 is a tyre, 20 is the rubber track of a tracked part, which then
            // reaches full friction - in the original the track bites harder than any tyre.
            float value = Mathf.Clamp01(friction / (driven ? 20f : 50f));
            if (driven)
            {
                value = Mathf.Max(value, RoverPortPoweredPart.WheelMinimumFriction);
            }
            PhysicMaterial material = new PhysicMaterial("RCR2_" + data.id);
            material.dynamicFriction = value;
            material.staticFriction = value;
            material.bounciness = 0f;
            return material;
        }

        private static bool BuildPolygonCollider(GameObject gameObject, RoverPortColliderData colliderData, Vector3 offset, PhysicMaterial physicMaterial)
        {
            if (colliderData.points == null || colliderData.points.Length < 3)
            {
                return false;
            }
            int count = colliderData.points.Length;
            Vector3[] vertices = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 point = new Vector2(colliderData.points[i].x, colliderData.points[i].y);
                vertices[i] = new Vector3(point.x + offset.x, point.y + offset.y, 0f);
            }
            int[] triangles = new int[(count - 2) * 3];
            int triangleIndex = 0;
            for (int i = 1; i < count - 1; i++)
            {
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + 1;
            }
            Mesh mesh = new Mesh();
            mesh.name = "RCR2Collider_" + gameObject.name;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            MeshCollider collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMaterial = physicMaterial;
            collider.sharedMesh = mesh;
            collider.convex = true;
            collider.isTrigger = colliderData.trigger;
            collider.enabled = colliderData.enabled;
            return true;
        }

        private static Bounds PolygonBounds(RoverPortVector2Data[] points, Vector3 offset)
        {
            if (points == null || points.Length == 0)
            {
                return new Bounds(offset, new Vector3(0.5f, 0.5f, 1f));
            }
            Vector2 first = new Vector2(points[0].x, points[0].y);
            Bounds bounds = new Bounds(new Vector3(first.x + offset.x, first.y + offset.y, 0f), Vector3.zero);
            foreach (RoverPortVector2Data point in points)
            {
                Vector2 value = new Vector2(point.x, point.y);
                bounds.Encapsulate(new Vector3(value.x + offset.x, value.y + offset.y, 0f));
            }
            Vector3 size = bounds.size;
            size.z = 1f;
            bounds.size = size;
            return bounds;
        }

        private static void BuildBodies(Dictionary<string, Transform> nodes, RoverPortPartData data)
        {
            foreach (RoverPortBodyData bodyData in data.bodies ?? new RoverPortBodyData[0])
            {
                Transform node;
                if (bodyData == null || !nodes.TryGetValue(bodyData.node, out node))
                {
                    continue;
                }
                Rigidbody body = node.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = node.gameObject.AddComponent<Rigidbody>();
                }
                body.mass = Mathf.Max(0.01f, bodyData.mass);
                body.drag = Mathf.Max(0f, bodyData.linear_drag);
                body.angularDrag = Mathf.Max(0f, bodyData.angular_drag);
                body.useGravity = bodyData.gravity_scale != 0f;
                body.isKinematic = bodyData.body_type != 0 || !bodyData.simulated;
                body.constraints = ConvertConstraints(bodyData.constraints);
                RoverPortGravityBody gravityBody = node.GetComponent<RoverPortGravityBody>();
                if (gravityBody == null)
                {
                    gravityBody = node.gameObject.AddComponent<RoverPortGravityBody>();
                }
                gravityBody.Configure(body, bodyData.gravity_scale);
            }
        }

        private GameObject BuildIcon(RoverPortPartData data)
        {
            if (data.icon == null)
            {
                return null;
            }
            Texture2D texture = GetTexture(data.icon.texture_guid);
            Material material = GetMaterial(data.icon.texture_guid, "UIPartButtonAtlas", "icon");
            if (texture == null || material == null)
            {
                return null;
            }
            GameObject icon = new GameObject("Icon_" + data.asset_name);
            icon.transform.SetParent(m_iconHost.transform, false);
            int hudLayer = LayerMask.NameToLayer("HUD");
            if (hudLayer >= 0)
            {
                icon.layer = hudLayer;
            }
            RoverPortIconSprite sprite = icon.AddComponent<RoverPortIconSprite>();
            sprite.Configure(texture, data.icon, material);
            return icon;
        }

        private Material GetMaterial(string textureGuid, string templateName, string usage)
        {
            if (string.IsNullOrEmpty(textureGuid))
            {
                return null;
            }
            string key = textureGuid + "|" + usage;
            Material cached;
            if (m_materials.TryGetValue(key, out cached))
            {
                return cached;
            }

            Material template = null;
            try
            {
                template = INUnity.LoadMaterial(templateName);
            }
            catch (Exception)
            {
            }

            Shader shader = template == null ? null : template.shader;
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                Debug.LogError("[RCR2Port] Missing material/shader for ported texture " + textureGuid);
                return null;
            }

            // A part that names its own shader keeps it instead of inheriting the atlas
            // template's (see m_partShader above).
            string forced = string.Equals(usage, "icon", StringComparison.Ordinal) ? m_iconShader : m_partShader;
            if (!string.IsNullOrEmpty(forced))
            {
                Shader forcedShader = Shader.Find(forced);
                if (forcedShader != null)
                {
                    Material forcedMaterial = new Material(forcedShader);
                    forcedMaterial.name = "RCR2_" + usage + "_" + textureGuid;
                    forcedMaterial.mainTexture = GetTexture(textureGuid);
                    if (forcedMaterial.HasProperty("_Color"))
                    {
                        Color color = forcedMaterial.GetColor("_Color");
                        color.a = 1f;
                        forcedMaterial.SetColor("_Color", color);
                    }
                    forcedMaterial.hideFlags = HideFlags.DontSave;
                    m_materials[key] = forcedMaterial;
                    return forcedMaterial;
                }
            }

            Material material = template == null ? new Material(shader) : new Material(template);
            material.name = "RCR2_" + usage + "_" + textureGuid;
            material.mainTexture = GetTexture(textureGuid);
            if (material.HasProperty("_Color"))
            {
                Color color = material.GetColor("_Color");
                color.a = 1f;
                material.SetColor("_Color", color);
            }
            material.hideFlags = HideFlags.DontSave;
            m_materials[key] = material;
            return material;
        }

        private static RigidbodyConstraints ConvertConstraints(int constraints)
        {
            RigidbodyConstraints result = RigidbodyConstraints.None;
            if ((constraints & 1) != 0)
            {
                result |= RigidbodyConstraints.FreezePositionX;
            }
            if ((constraints & 2) != 0)
            {
                result |= RigidbodyConstraints.FreezePositionY;
            }
            if ((constraints & 4) != 0)
            {
                result |= RigidbodyConstraints.FreezeRotation;
            }
            return result;
        }

        private static Vector3 ToVector3(float[] value, Vector3 fallback)
        {
            if (value == null || value.Length < 2)
            {
                return fallback;
            }
            return new Vector3(value[0], value[1], value.Length > 2 ? value[2] : 0f);
        }

        private static Vector2 ToVector2(float[] value, Vector2 fallback)
        {
            if (value == null || value.Length < 2)
            {
                return fallback;
            }
            return new Vector2(value[0], value[1]);
        }

        private static Quaternion ToQuaternion(float[] value)
        {
            if (value == null || value.Length < 4)
            {
                return Quaternion.identity;
            }
            return new Quaternion(value[0], value[1], value[2], value[3]);
        }

        private static Color ToColor(float[] value)
        {
            if (value == null || value.Length < 4)
            {
                return Color.white;
            }
            return new Color(value[0], value[1], value[2], value[3]);
        }
    }
}







