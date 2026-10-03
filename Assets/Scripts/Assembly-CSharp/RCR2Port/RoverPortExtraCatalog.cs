using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Loads rcr2_extra_catalog.json - parts ported from other RoverCraft 2
    /// versions (currently the 1.1.0 "super wheel", renamed Mecanumwheel) -
    /// and appends them to <see cref="RoverPortCatalog"/> without touching any
    /// of its files: the parts are injected into the catalog's collections via
    /// reflection, so the main catalog and this loader stay independently
    /// editable. See Tools/RCR2/ExtractMecanumWheel.py for the data source.
    /// </summary>
    public static class RoverPortExtraCatalog
    {
        private const string CatalogResourcePath = "RCR2Ported/rcr2_extra_catalog";
        private const string TextureResourceRoot = "RCR2Ported/Textures/";
        private static bool s_loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Load();
        }

        public static void Load()
        {
            if (s_loaded)
            {
                return;
            }
            s_loaded = true;
            try
            {
                TextAsset textAsset = Resources.Load<TextAsset>(CatalogResourcePath);
                if (textAsset == null)
                {
                    return;
                }
                RoverPortExtraCatalogFile catalog = JsonUtility.FromJson<RoverPortExtraCatalogFile>(textAsset.text);
                if (catalog == null || catalog.parts == null || catalog.parts.Length == 0)
                {
                    return;
                }

                RoverPortCatalog target = RoverPortCatalog.Instance;
                Dictionary<string, Texture2D> textures = LoadTextures(catalog);

                List<GameObject> parts = GetPartsList(target);
                Dictionary<string, GameObject> partsById = GetDictionary<string>(target, "m_partsById");
                Dictionary<int, GameObject> partsByIndex = GetDictionary<int>(target, "m_partsByIndex");
                if (parts == null || partsById == null || partsByIndex == null)
                {
                    Debug.LogError("[RCR2Extra] RoverPortCatalog collections not found via reflection");
                    return;
                }

                int nextIndex = RoverPortCatalog.FirstPartIndex;
                foreach (KeyValuePair<int, GameObject> pair in partsByIndex)
                {
                    nextIndex = Mathf.Max(nextIndex, pair.Key + 1);
                }

                Transform host = GetTransform(target, "m_host");
                Transform iconHost = GetTransform(target, "m_iconHost");
                bool ownHosts = host == null || iconHost == null;
                if (ownHosts)
                {
                    GameObject hostObject = new GameObject("[RCR2 Extra Parts]");
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.DontDestroyOnLoad(hostObject);
                    }
                    host = hostObject.transform;
                    hostObject.SetActive(false);
                    GameObject iconObject = new GameObject("[RCR2 Extra Part Icons]");
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.DontDestroyOnLoad(iconObject);
                    }
                    iconObject.transform.position = new Vector3(10000f, 10000f, 0f);
                    iconHost = iconObject.transform;
                }

                Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
                int built = 0;
                foreach (RoverPortPartData partData in catalog.parts)
                {
                    if (partData == null || string.IsNullOrEmpty(partData.id))
                    {
                        continue;
                    }
                    try
                    {
                        if (partsById.ContainsKey(partData.id))
                        {
                            Debug.LogWarning("[RCR2Extra] duplicate part id skipped: " + partData.id);
                            continue;
                        }
                        GameObject part = BuildPart(partData, textures, materials, host, iconHost, nextIndex++);
                        if (part == null)
                        {
                            continue;
                        }
                        parts.Add(part);
                        partsById[partData.id] = part;
                        RoverPortPart component = part.GetComponent<RoverPortPart>();
                        if (component != null)
                        {
                            partsByIndex[component.Index] = part;
                        }
                        built++;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[RCR2Extra] failed to build part '" + partData.id + "': " + exception);
                    }
                }
                if (built > 0)
                {
                    Debug.Log("[RCR2Extra] injected " + built + " extra ported part(s)");
                }

                ApplyOverrides(catalog.overrides, textures, materials, iconHost, partsById);
            }
            catch (Exception exception)
            {
                Debug.LogError("[RCR2Extra] failed to load the extra RoverCraft 2 catalog: " + exception);
            }
        }

        /// <summary>
        /// Restyles already-ported parts with artwork from other RCR2 versions
        /// (e.g. the stabilizer's 1.1.0 look) and attaches version-specific
        /// behaviour. Node positions are applied as root-relative deltas so the
        /// shared hierarchy between versions does not matter; connection points,
        /// colliders that are not moved with their node, and the main catalog
        /// data are left untouched.
        /// </summary>
        private static void ApplyOverrides(RoverPortOverrideData[] overrides,
            Dictionary<string, Texture2D> textures, Dictionary<string, Material> materials,
            Transform iconHost, Dictionary<string, GameObject> partsById)
        {
            foreach (RoverPortOverrideData data in overrides ?? new RoverPortOverrideData[0])
            {
                if (data == null || string.IsNullOrEmpty(data.part))
                {
                    continue;
                }
                GameObject partObject;
                if (!partsById.TryGetValue(data.part, out partObject) || partObject == null)
                {
                    Debug.LogWarning("[RCR2Extra] override target missing: " + data.part);
                    continue;
                }
                try
                {
                    RoverPortPart part = partObject.GetComponent<RoverPortPart>();
                    if (part == null)
                    {
                        continue;
                    }
                    foreach (RoverPortNodeOverrideData node in data.nodes ?? new RoverPortNodeOverrideData[0])
                    {
                        Transform transform = FindChildByName(partObject.transform, node.node);
                        if (transform == null)
                        {
                            Debug.LogWarning("[RCR2Extra] override node missing: " + data.part + "/" + node.node);
                            continue;
                        }
                        Vector3 target = ToVector3(node.position, Vector3.zero);
                        Vector3 current = RootRelativePosition(partObject.transform, transform);
                        transform.localPosition += target - current;
                    }

                    ApplyRendererOverrides(partObject, data.renderers, textures, materials);
                    ApplyRendererOverrides(partObject, data.construction_renderers, textures, materials);

                    if (data.mounts != null && data.mounts.Length > 0)
                    {
                        part.SetConnectionMounts(data.mounts);
                        Debug.Log("[RCR2Extra] " + data.mounts.Length + " separate joint mount(s) applied to " + data.part);
                    }

                    if (data.icon != null)
                    {
                        // The part buttons and the drag preview draw the part's own
                        // icon object, so bind it directly instead of relying on the
                        // icon host layout.
                        RoverPortIconSprite iconSprite = part.m_constructionIconSprite as RoverPortIconSprite;
                        if (iconSprite == null)
                        {
                            Transform icon = iconHost == null ? null : iconHost.Find("Icon_" + partObject.name);
                            if (icon != null)
                            {
                                iconSprite = icon.GetComponent<RoverPortIconSprite>();
                            }
                        }
                        Texture2D texture = GetTexture(textures, data.icon.texture_guid);
                        Material material = GetMaterial(data.icon.texture_guid, "UIPartButtonAtlas", "icon", textures, materials);
                        if (iconSprite != null && texture != null && material != null)
                        {
                            iconSprite.Configure(texture, data.icon, material);
                        }
                        else
                        {
                            Debug.LogWarning("[RCR2Extra] icon override skipped for " + data.part
                                + " (icon=" + (iconSprite != null) + ", texture=" + (texture != null)
                                + ", material=" + (material != null) + ")");
                        }
                    }

                    if (string.Equals(data.special, "gyroplatform", StringComparison.Ordinal) && data.muscles != null)
                    {
                        RoverPortGyroPlatform gyro = partObject.GetComponent<RoverPortGyroPlatform>();
                        if (gyro == null)
                        {
                            gyro = partObject.AddComponent<RoverPortGyroPlatform>();
                        }
                        gyro.Configure(part, data.muscles.stick_node, data.muscles.platform_node,
                            data.muscles.stick, data.muscles.platform, data.muscles.mass_multiplier);
                    }

                    // the new artwork changes the measured footprint
                    InvokePrivate(part, "RecalculateGridFootprint");
                    Debug.Log("[RCR2Extra] override applied to " + data.part);
                }
                catch (Exception exception)
                {
                    Debug.LogError("[RCR2Extra] failed to apply override to '" + data.part + "': " + exception);
                }
            }
        }

        private static void ApplyRendererOverrides(GameObject partObject, RoverPortRendererData[] renderers,
            Dictionary<string, Texture2D> textures, Dictionary<string, Material> materials)
        {
            foreach (RoverPortRendererData rendererData in renderers ?? new RoverPortRendererData[0])
            {
                if (rendererData == null || rendererData.sprite == null)
                {
                    continue;
                }
                Transform node = FindChildByName(partObject.transform, rendererData.node);
                if (node == null)
                {
                    continue;
                }
                Texture2D texture = GetTexture(textures, rendererData.sprite.texture_guid);
                Material material = GetMaterial(rendererData.sprite.texture_guid, "IngameAtlas", "part", textures, materials);
                if (texture == null || material == null)
                {
                    continue;
                }
                RoverPortMeshSprite sprite = node.GetComponent<RoverPortMeshSprite>();
                if (sprite == null)
                {
                    sprite = node.gameObject.AddComponent<RoverPortMeshSprite>();
                }
                sprite.Configure(
                    texture,
                    rendererData.sprite,
                    material,
                    ToColor(rendererData.color),
                    rendererData.flip_x,
                    rendererData.flip_y,
                    rendererData.sorting_order,
                    rendererData.enabled);
            }
        }

        private static Vector3 RootRelativePosition(Transform root, Transform node)
        {
            Vector3 position = Vector3.zero;
            for (Transform current = node; current != null && current != root; current = current.parent)
            {
                position += current.localPosition;
            }
            return position;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static Texture2D GetTexture(Dictionary<string, Texture2D> textures, string guid)
        {
            Texture2D texture;
            textures.TryGetValue(guid, out texture);
            return texture;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(target, null);
            }
        }

        private static Dictionary<string, Texture2D> LoadTextures(RoverPortExtraCatalogFile catalog)
        {
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (RoverPortTextureData texture in catalog.textures ?? new RoverPortTextureData[0])
            {
                if (texture == null || string.IsNullOrEmpty(texture.canonical_file))
                {
                    continue;
                }
                string resourceName = System.IO.Path.GetFileNameWithoutExtension(texture.canonical_file);
                Texture2D loaded = Resources.Load<Texture2D>(TextureResourceRoot + resourceName);
                if (loaded != null)
                {
                    textures[texture.guid] = loaded;
                }
                else
                {
                    Debug.LogWarning("[RCR2Extra] missing texture resource: " + resourceName);
                }
            }
            return textures;
        }

        private static GameObject BuildPart(RoverPortPartData data, Dictionary<string, Texture2D> textures,
            Dictionary<string, Material> materials, Transform host, Transform iconHost, int partIndex)
        {
            GameObject root = new GameObject("RCR2_" + data.id);
            root.transform.SetParent(host, false);
            root.SetActive(false);

            RoverPortPart part;
            if (string.Equals(data.motion.kind, "battery", StringComparison.Ordinal))
            {
                part = root.AddComponent<RoverPortBatteryPart>();
            }
            else if (string.Equals(data.motion.kind, "missile", StringComparison.Ordinal))
            {
                // The RC3 missile launcher; behaviour and sprite data live in
                // RoverPortMissilePart (it reads them from data via Configure).
                part = root.AddComponent<RoverPortMissilePart>();
            }
            else if (string.Equals(data.motion.kind, "wheel", StringComparison.Ordinal) ||
                     string.Equals(data.motion.kind, "engine", StringComparison.Ordinal) ||
                     string.Equals(data.motion.kind, "propeller", StringComparison.Ordinal))
            {
                part = root.AddComponent<RoverPortPoweredPart>();
            }
            else
            {
                part = root.AddComponent<RoverPortPart>();
            }

            Vector3 rootOffset = RootOffset(data);
            Dictionary<string, Transform> nodes = BuildNodes(root.transform, data, rootOffset);
            List<Renderer> physicsRenderers = BuildRenderers(nodes, data.renderers, textures, materials);
            List<Renderer> constructionRenderers = BuildRenderers(nodes, data.construction_renderers, textures, materials);
            part.ConfigureVisuals(constructionRenderers, physicsRenderers);
            PhysicMaterial physicMaterial = CreateFrictionMaterial(data);
            // Per part, so every collider that shares a source material shares one object.
            Dictionary<string, PhysicMaterial> colliderMaterials = new Dictionary<string, PhysicMaterial>(StringComparer.Ordinal);
            BuildColliders(nodes, data, physicMaterial, colliderMaterials, rootOffset);

            GameObject icon = BuildIcon(data, textures, materials, iconHost);
            part.Configure(data, partIndex, nodes, icon == null ? null : icon.GetComponent<RoverPortIconSprite>());
            return root;
        }

        private static Vector3 RootOffset(RoverPortPartData data)
        {
            Vector3 offset = ToVector3(data.rotation_center, Vector3.zero);
            if (data.grid != null)
            {
                offset -= ToVector3(data.grid.shift, Vector3.zero);
            }
            return offset;
        }

        private static Dictionary<string, Transform> BuildNodes(Transform root, RoverPortPartData data, Vector3 rootOffset)
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

            if (rootOffset.sqrMagnitude > 0.000001f)
            {
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    child.localPosition -= rootOffset;
                }
                AdjustRootJointAnchors(data, rootOffset);
            }
            return nodes;
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

        private static Transform ResolveNode(string id, Transform root, Dictionary<string, Transform> nodes,
            Dictionary<string, RoverPortNodeData> nodeData, HashSet<string> visiting)
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

        private static List<Renderer> BuildRenderers(Dictionary<string, Transform> nodes, RoverPortRendererData[] renderers,
            Dictionary<string, Texture2D> textures, Dictionary<string, Material> materials)
        {
            List<Renderer> enabledRenderers = new List<Renderer>();
            foreach (RoverPortRendererData rendererData in renderers ?? new RoverPortRendererData[0])
            {
                Transform node;
                if (rendererData == null || rendererData.sprite == null || !nodes.TryGetValue(rendererData.node, out node))
                {
                    continue;
                }
                Texture2D texture;
                textures.TryGetValue(rendererData.sprite.texture_guid, out texture);
                Material material = GetMaterial(rendererData.sprite.texture_guid, "IngameAtlas", "part", textures, materials);
                if (texture == null || material == null)
                {
                    continue;
                }

                RoverPortMeshSprite sprite = node.GetComponent<RoverPortMeshSprite>();
                if (sprite == null)
                {
                    sprite = node.gameObject.AddComponent<RoverPortMeshSprite>();
                }
                sprite.Configure(
                    texture,
                    rendererData.sprite,
                    material,
                    ToColor(rendererData.color),
                    rendererData.flip_x,
                    rendererData.flip_y,
                    rendererData.sorting_order,
                    rendererData.enabled);
                if (rendererData.enabled)
                {
                    enabledRenderers.Add(node.GetComponent<MeshRenderer>());
                }
            }
            return enabledRenderers;
        }

        private static void BuildColliders(Dictionary<string, Transform> nodes, RoverPortPartData data,
            PhysicMaterial physicMaterial, Dictionary<string, PhysicMaterial> materialCache, Vector3 rootOffset)
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
                    offset -= rootOffset;
                }
                // Same contract as RoverPortCatalog: a per-collider material when the catalog
                // carries one, otherwise the part-level friction material. See
                // RoverPortColliderMaterials.
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

        private static bool BuildPolygonCollider(GameObject gameObject, RoverPortColliderData colliderData,
            Vector3 offset, PhysicMaterial physicMaterial)
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

        private static PhysicMaterial CreateFrictionMaterial(RoverPortPartData data)
        {
            float friction = data.friction > 0f ? data.friction
                : data.motion != null ? data.motion.friction : 0f;
            bool driven = data.motion != null && string.Equals(data.motion.kind, "wheel", StringComparison.Ordinal);
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

        private static GameObject BuildIcon(RoverPortPartData data, Dictionary<string, Texture2D> textures,
            Dictionary<string, Material> materials, Transform iconHost)
        {
            if (data.icon == null)
            {
                return null;
            }
            Texture2D texture;
            textures.TryGetValue(data.icon.texture_guid, out texture);
            Material material = GetMaterial(data.icon.texture_guid, "UIPartButtonAtlas", "icon", textures, materials);
            if (texture == null || material == null)
            {
                return null;
            }
            GameObject icon = new GameObject("Icon_" + data.asset_name);
            icon.transform.SetParent(iconHost, false);
            int hudLayer = LayerMask.NameToLayer("HUD");
            if (hudLayer >= 0)
            {
                icon.layer = hudLayer;
            }
            RoverPortIconSprite sprite = icon.AddComponent<RoverPortIconSprite>();
            sprite.Configure(texture, data.icon, material);
            return icon;
        }

        private static Material GetMaterial(string textureGuid, string templateName, string usage,
            Dictionary<string, Texture2D> textures, Dictionary<string, Material> materials)
        {
            if (string.IsNullOrEmpty(textureGuid))
            {
                return null;
            }
            string key = textureGuid + "|" + usage;
            Material cached;
            if (materials.TryGetValue(key, out cached))
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
                Debug.LogError("[RCR2Extra] missing material/shader for extra texture " + textureGuid);
                return null;
            }

            Material material = template == null ? new Material(shader) : new Material(template);
            material.name = "RCR2_" + usage + "_" + textureGuid;
            Texture2D texture;
            textures.TryGetValue(textureGuid, out texture);
            material.mainTexture = texture;
            material.hideFlags = HideFlags.DontSave;
            materials[key] = material;
            return material;
        }

        private static List<GameObject> GetPartsList(RoverPortCatalog catalog)
        {
            FieldInfo field = typeof(RoverPortCatalog).GetField("m_parts",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(catalog) as List<GameObject>;
        }

        private static Dictionary<TKey, GameObject> GetDictionary<TKey>(RoverPortCatalog catalog, string fieldName)
        {
            FieldInfo field = typeof(RoverPortCatalog).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(catalog) as Dictionary<TKey, GameObject>;
        }

    private static Transform GetTransform(RoverPortCatalog catalog, string fieldName)
    {
        FieldInfo field = typeof(RoverPortCatalog).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            return null;
        }
        object value = field.GetValue(catalog);
        GameObject gameObject = value as GameObject;
        if (gameObject != null)
        {
            return gameObject.transform;
        }
        return value as Transform;
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

    /// <summary>
    /// Root schema of rcr2_extra_catalog.json: the standalone extra parts plus
    /// override blocks that restyle already-ported main-catalog parts with
    /// artwork from other RCR2 versions.
    /// </summary>
    [Serializable]
    public class RoverPortExtraCatalogFile
    {
        public int version;
        public string source;
        public RoverPortPartData[] parts;
        public RoverPortTextureData[] textures;
        public RoverPortOverrideData[] overrides;
    }

    [Serializable]
    public class RoverPortOverrideData
    {
        public string part;
        public string special;
        public RoverPortNodeOverrideData[] nodes;
        public RoverPortRendererData[] renderers;
        public RoverPortRendererData[] construction_renderers;
        public RoverPortSpriteData icon;
        public RoverPortGyroMuscleSetData muscles;
        /// <summary>
        /// RCR2 mounts whose joint body is not the part root (the stabilizer's
        /// servo platform): a neighbour part attached at such a point has to be
        /// welded to that body so it turns with it.
        /// </summary>
        public RoverPortMountData[] mounts;
    }

    /// <summary>Connection point name plus the body a neighbour is jointed to.</summary>
    [Serializable]
    public class RoverPortMountData
    {
        public string point;
        public string body;
    }

    /// <summary>Moves one named art node to a root-relative position.</summary>
    [Serializable]
    public class RoverPortNodeOverrideData
    {
        public string node;
        public float[] position;
    }

    [Serializable]
    public class RoverPortGyroMuscleSetData
    {
        public string stick_node;
        public string platform_node;
        public RoverPortMuscleData stick;
        public RoverPortMuscleData platform;
        public float mass_multiplier;
    }

    [Serializable]
    public class RoverPortMuscleData
    {
        public float target_angle;
        public float force;
        public bool enabled;
        public bool use_velocity_change;
        public bool use_force_change;
        public float torque_multiplier;
        public RoverPortCurveKeyData[] torque_curve;
    }

    [Serializable]
    public class RoverPortCurveKeyData
    {
        public float time;
        public float value;
    }
}
