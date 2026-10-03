using System;
using System.Collections.Generic;
using System.Reflection;
using RCR2Port;
using UnityEngine;

namespace RC3Port
{
    /// <summary>
    /// Loads rc1_catalog.json - every part ported from RC1 - and appends it to
    /// <see cref="RoverPortCatalog"/>.
    ///
    /// RC1 parts deliberately share the RCR2 catalog instance instead of living
    /// in a catalog of their own: GameDataAdapter, ConstructionUI and PartSelector
    /// resolve *every* part index at or above RoverPortCatalog.FirstPartIndex
    /// through RoverPortCatalog alone, so a separate class would make the parts
    /// invisible to the build list and unresolvable when a save is loaded.
    ///
    /// They are told apart from the RCR2 ports by their index range. RC1 uses the
    /// fixed base <see cref="FirstPartIndex"/> (4000) rather than the RCR2 extra
    /// catalog's "max + 1", because the index *is* the save identity
    /// (INContraption stores (SortedPartType, customPartIndex) and the type is
    /// advisory): a fixed base means RCR2 gaining or losing parts can never
    /// renumber an RC1 part and silently repoint an existing blueprint. The base
    /// is also kept clear of the RC3 range (3000+) so the two ports cannot
    /// collide.
    ///
    /// The data source is the Android parts manifest already ported in
    /// _PreviewBuildProject (Assets/AndroidParts/android_parts_manifest.json).
    /// </summary>
    public static class RC1PortCatalog
    {
        /// <summary>
        /// Index base for RC1 parts. Must stay at or above
        /// <see cref="RoverPortCatalog.FirstPartIndex"/>, otherwise the parts stop
        /// being treated as ported and saved RC1 parts are resolved against the
        /// native part factory (which does not know them) and dropped.
        /// </summary>
        public const int FirstPartIndex = 4000;

        /// <summary>The indices this catalog assigned, which is what <see cref="IsRC1Index"/> answers.</summary>
        private static readonly HashSet<int> s_indices = new HashSet<int>();

        private const string CatalogResourcePath = "RC1Ported/rc1_catalog";
        private const string TextureResourceRoot = "RC1Ported/Textures/";
        private static bool s_loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Load();
        }

        /// <summary>
        /// True when a part index belongs to the RC1 catalog.
        /// </summary>
        /// <remarks>
        /// Membership of what this catalog assigned, not a range. RC1 occupies 4000..4031 today, and the
        /// RCR2 *extra* catalog hands out "highest existing index + 1", so it placed part_mecanum_wheel
        /// at 4032 - inside an open-ended "index &gt;= 4000" test, which filed that wheel under the RC1
        /// tab in <c>PartSelector</c> and made the RC1 QA audit count 33 parts instead of 32. The extra
        /// catalog's own base depends on load order, so no fixed range can be right; this set is exactly
        /// what this catalog assigned. Part *resolution* never asks this question
        /// (<c>RoverPortCatalog.GetPart</c> is dictionary-based), so no save changes meaning.
        /// </remarks>
        public static bool IsRC1Index(int index)
        {
            return s_indices.Contains(index);
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
                RoverPortCatalogData catalog = JsonUtility.FromJson<RoverPortCatalogData>(textAsset.text);
                if (catalog == null || catalog.parts == null || catalog.parts.Length == 0)
                {
                    return;
                }

                RoverPortCatalog target = RoverPortCatalog.Instance;
                List<GameObject> parts = GetCatalogField<List<GameObject>>(target, "m_parts");
                Dictionary<string, GameObject> partsById = GetCatalogField<Dictionary<string, GameObject>>(target, "m_partsById");
                Dictionary<int, GameObject> partsByIndex = GetCatalogField<Dictionary<int, GameObject>>(target, "m_partsByIndex");
                Dictionary<string, Texture2D> textures = GetCatalogField<Dictionary<string, Texture2D>>(target, "m_textures");
                if (parts == null || partsById == null || partsByIndex == null || textures == null)
                {
                    Debug.LogError("[RC1Port] RoverPortCatalog collections not found via reflection");
                    return;
                }

                // BuildPart resolves artwork through the catalog's own texture and
                // material tables, so the RC1 guids have to be registered before
                // the first part is built. Without this every renderer is skipped
                // silently and every icon comes back null.
                RegisterTextures(catalog, textures);

                MethodInfo buildPart = typeof(RoverPortCatalog).GetMethod("BuildPart",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (buildPart == null)
                {
                    Debug.LogError("[RC1Port] RoverPortCatalog.BuildPart not found via reflection");
                    return;
                }

                int nextIndex = FirstPartIndex;
                int built = 0;
                foreach (RoverPortPartData partData in catalog.parts)
                {
                    if (partData == null || string.IsNullOrEmpty(partData.id))
                    {
                        continue;
                    }
                    // Consume the index even when the build throws, so the part
                    // numbering stays a pure function of the catalog file.
                    int partIndex = nextIndex++;
                    s_indices.Add(partIndex);
                    try
                    {
                        if (partsById.ContainsKey(partData.id))
                        {
                            Debug.LogWarning("[RC1Port] duplicate part id skipped: " + partData.id);
                            continue;
                        }
                        GameObject part = buildPart.Invoke(target, new object[] { partData, partIndex }) as GameObject;
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
                        Debug.LogError("[RC1Port] failed to build part '" + partData.id + "': " + exception);
                    }
                }
                if (built > 0)
                {
                    Debug.Log("[RC1Port] injected " + built + " RC1 part(s)");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[RC1Port] failed to load the RC1 catalog: " + exception);
            }
        }

        private static void RegisterTextures(RoverPortCatalogData catalog, Dictionary<string, Texture2D> textures)
        {
            foreach (RoverPortTextureData texture in catalog.textures ?? new RoverPortTextureData[0])
            {
                if (texture == null || string.IsNullOrEmpty(texture.guid) ||
                    string.IsNullOrEmpty(texture.canonical_file) || textures.ContainsKey(texture.guid))
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
                    Debug.LogWarning("[RC1Port] missing texture resource: " + resourceName);
                }
            }
        }

        private static T GetCatalogField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = typeof(RoverPortCatalog).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(target) as T;
        }
    }
}
