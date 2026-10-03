using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using RCR2Port;
using UnityEngine;

namespace RC3Port
{
    /// <summary>
    /// TEMPORARY DIAGNOSTIC - SAFE TO DELETE (this file and its .cs.meta).
    ///
    /// It exists because "RC1 部件拖动时能看到，放下去看不到" cannot be judged from a
    /// screenshot by whoever reads this code: the log line is the evidence. Delete this
    /// file when the RC1 port is signed off; nothing else references it.
    ///
    /// It runs once, after the scene has loaded, over the RC1 part *templates* the catalog
    /// built (RC1PortCatalog injects them from
    /// Assets/Resources/RC1Ported/rc1_catalog.json through RoverPortCatalog). Grep the
    /// player log for the prefix:
    ///
    ///     [RC1Diag]
    ///
    /// Healthy (one line per part, then one line per renderer of each set):
    ///
    ///     [RC1Diag] part=part_rc1_1001 index=4000 renderers=2 enabled=1 ctorList=1
    ///               ctorEnabled=1 physList=1 physEnabled=0 aliased=0 orderMin=0 orderMax=0
    ///               activeInHierarchy=0 rootActive=False points=8 cpHolders=2 mounts=4
    ///               colliders=1 colliderMaxOffset=0.000
    ///     [RC1Diag]   ctor[0] node=... path=RCR2_part_rc1_1001/AndroidSource/... layerID=0
    ///               layer=Default order=0 enabled=True active=True pos=(0.000,0.000,0.000)
    ///               scale=(1.000,1.000,1.000) relPos=(0.000,0.000,0.000)
    ///     [RC1Diag]   phys[0] ... enabled=False ...
    ///     [RC1Diag]   point[0] name=Point-Bottom-0-0 parent=ConnectionPoints
    ///               world=(0.000,-0.500,0.000) local=(0.000,-0.500,0.000)
    ///     [RC1Diag] summary parts=32 renderers=... enabled=... ctorEnabled=... physEnabled=...
    ///               activeInHierarchy=0 aliased=0 negativeOrders=0 otherPortedParts=149
    ///               points=244 partsWithNoPoints=0 cpMissing=0 mountMismatch=0
    ///               colliderMaxOffset=0.543 collidersOutsideFootprint=0
    ///               pivots=38 pivotResolved=38 pivotRenderersUnder=... wheelsWithoutPivots=0
    ///               wheelsWithoutCollisionRule=0 particleDrivers=3 particleSystemResolved=3
    ///               particleOwnerResolved=3 cloneProbes=13 clonePivotResolved=... 
    ///               cloneSystemResolved=3 cloneOwnerResolved=3 cloneSpinMissing=0
    ///
    /// and, for every wheel and every rebuilt particle system (the three bug reports):
    ///
    ///     [RC1Diag]   pivot[0] node=PE:wp name=WheelPivot radius=0.450 mode=surface
    ///                 source=MotorWheel resolved=True renderersUnder=4
    ///     [RC1Diag]   particle[0] driver=PS_Fire_Blue system=True owner=True playing=False
    ///                 gate=False
    ///     [RC1Diag]   clone part=part_rc1_1009 pivots=2 pivotsResolved=2 spins=0 drivers=0
    ///                 system=0 owner=0 kind=wheel consumption=100.0 maxSpeedRpm=900.0
    ///                 energyScale=0.000 enabled=False
    ///
    /// What each field proves:
    ///   * ctorEnabled == ctorList -> ConfigureVisuals left the build artwork switched on,
    ///     so the part is visible in the build view. The broken state was
    ///     ctorList == physList on the *same* nodes (aliased != 0): ConfigureVisuals then
    ///     disabled the physics list, which was the only list, and the part went dark.
    ///   * aliased == 0 -> no Renderer object is in both lists. Broken state:
    ///     aliased == renderers.
    ///   * physEnabled == 0 on a template -> normal: EnsureRigidbody() switches the physics
    ///     set on when the contraption starts. A placed part that stays dark has
    ///     ctorEnabled == 0 as well.
    ///   * negativeOrders == 0 -> nothing draws behind the ground/background. Broken
    ///     state: order=-200 on parts whose source z was positive.
    ///   * orderMin/orderMax inside 0..4 -> the RC3 convention, applied by
    ///     Tools/RC1/BuildRC1Catalog.ps1.
    ///   * points == 2 * mounts and cpHolders == 2 -> BOTH node trees expose every mount,
    ///     which is what RoverPortPart.GetConnectionPoints() reads (it walks the part's
    ///     node table for `Point-*` names under a `ConnectionPoints` ancestor and returns
    ///     their world positions, ignoring model_root/physics_root). Broken state:
    ///     points=0 cpHolders=0 mounts=4 on every part, which is the "no RC1 part connects
    ///     to anything" report: TryGetConnection could never match an anchor, so
    ///     CreateCustomJoints added no joint.
    ///   * point[i] lines give the anchor positions themselves: healthy ones are on the
    ///     part's cell edges (+-0.5 offsets on a 1x1 part), broken ones do not exist.
    ///     local == world when the template is at the origin, which is where the catalog
    ///     builds it.
    ///   * colliderMaxOffset <= (footprint + 1 cell) -> every collider is inside its own
    ///     part. Broken state: 30..40, i.e. the collider sat tens of cells below its part,
    ///     inside the ground, and the solver threw the interpenetrating part out - the
    ///     "parts fly around after being placed" report. 37 of the 46 RC1 colliders were
    ///     like that before the frame fix in Tools/RC1/BuildRC1Catalog.ps1; none of RC3's
    ///     498 or RCR2's 610 are.
    ///   * rootActive=False and activeInHierarchy=0 are EXPECTED here: the catalog builds
    ///     every template with root.SetActive(false) and the build view clones + activates
    ///     it on demand.
    ///   * pivot[i] lines are the wheel artwork the source's own component turns, resolved
    ///     from the prefab by Tools/RC1/BuildRC1Catalog.ps1 (`motion.wheel_pivots`): the
    ///     `WheelPivot` for the motor wheels/generator, `AndroidBigfootMotion.wheelPivot`
    ///     for the two bigfoot wheels, and all nine `AndroidTrackMotion` rollers for the
    ///     track. Healthy: resolved=True, radius>0, renderersUnder>0 on every line.
    ///     Broken: no pivot lines at all (`pivots=0`, wheelsWithoutPivots>0) - that was the
    ///     "RC1 轮子不会转" report, because a ported wheel's artwork is a plain child of the
    ///     part root and the catalog ships no `bodies` for a RoverPortFollower to copy.
    ///   * wheelCollisionRule=on on the three wheel parts is the source's own rule that a
    ///     wheel never collides with its own vehicle (AndroidTrackMotion.RefreshConnections,
    ///     OffRoadWheel.PostInitialize). It is read from `motion.kind == "wheel"`, the same
    ///     test RoverPortPart.AttachWheelMotion uses, so `off` on a wheel means the
    ///     RoverPortContraptionCollision component was not attached.
    ///   * particle[i] / clone lines: `system=True owner=True` is what makes the exhaust
    ///     smoke and the thruster flame work. `clone` is the decisive one: a placed part is
    ///     an Object.Instantiate clone (ConstructionUI.SetPartAt), so a driver whose
    ///     ParticleSystem reference is not re-resolved reports system=0 on the clone and
    ///     can never start its plume - which was the "排气管和喷气火焰不工作" report. `gate`
    ///     is False on a template by design (both source drivers need a running contraption).
    ///   * clone maxSpeedRpm=900.0 on an RC1 wheel is the healthy value: MAIN's MotorWheel
    ///     hard-codes `m_maximumSpeed = 15 * enginePowerFactor`, so an imported wheel whose
    ///     catalog says 0 is given ReferenceMotorWheelRpm (the rpm that maps to those 15 m/s).
    ///     Broken state was 0.0, and then WheelDriveForce's falloff is 0: the wheel pushed
    ///     nothing and never turned.
    /// </summary>
    public static class RC1PortDiagnostics
    {
        private const string Prefix = "[RC1Diag]";

        private static readonly FieldInfo s_constructionField = typeof(RoverPortPart).GetField(
            "m_constructionRenderers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_physicsField = typeof(RoverPortPart).GetField(
            "m_physicsRenderers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_portDataField = typeof(RoverPortPart).GetField(
            "m_portData", BindingFlags.Instance | BindingFlags.NonPublic);

        // The drive/particle gates the three bug reports are about. Read by reflection because
        // they are private state, not API: m_powerConsumption (wheels must be > 0 for
        // Contraption.SomePoweredPartsEnabled(), which is what opens the exhaust smoke),
        // m_enabled / m_lastEnergyScale (the thruster flame gate
        // RoverPortPoweredPart.ShouldEmitParticles("PS_Fire_Blue")), m_maximumSpeed and
        // m_motionKind (the wheel drive gate: WheelDriveForce's falloff is 0 while the rated
        // speed is 0, which is what left an RC1 wheel unable to turn or push).
        //
        // These have to be resolved from RoverPortPoweredPart, not RoverPortPart. FindField only
        // walks *up* the hierarchy, and m_enabled / m_lastEnergyScale / m_maximumSpeed are
        // declared on the powered subclass, so asking for them on RoverPortPart returned null and
        // every powered line read back as `kind= rpm=0.0 energyScale=0.000` - a probe that looked
        // like "the thruster has no energy" while the value was never read at all.
        internal static readonly FieldInfo s_powerConsumptionField = FindField(typeof(RoverPortPoweredPart), "m_powerConsumption");
        internal static readonly FieldInfo s_enabledField = FindField(typeof(RoverPortPoweredPart), "m_enabled");
        internal static readonly FieldInfo s_energyScaleField = FindField(typeof(RoverPortPoweredPart), "m_lastEnergyScale");
        internal static readonly FieldInfo s_maximumSpeedField = FindField(typeof(RoverPortPoweredPart), "m_maximumSpeed");
        internal static readonly FieldInfo s_motionKindField = FindField(typeof(RoverPortPoweredPart), "m_motionKind");

        /// <summary>
        /// Bridge probe: every MeshRenderer whose hierarchy path contains <paramref name="needle"/>,
        /// with the state the bridge itself cannot read (a `MeshRenderer` type name resolves to a
        /// stub assembly for it). Reports `enabled`, the mesh's vertex count, the material, the
        /// material's shader and main texture size, and the sorting order - enough to tell "the
        /// sprite is switched off" from "the sprite has no mesh" from "the material lost its
        /// texture", which look identical in a screenshot.
        /// </summary>
        public static string RendererProbe(string needle)
        {
            StringBuilder text = new StringBuilder();
            int shown = 0;
            foreach (MeshRenderer renderer in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (renderer == null)
                {
                    continue;
                }
                string path = PathOf(renderer.transform);
                if (path.IndexOf(needle, StringComparison.Ordinal) < 0)
                {
                    continue;
                }
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Material material = renderer.sharedMaterial;
                Texture texture = material == null ? null : material.mainTexture;
                text.Append(" | ").Append(path)
                    .Append(" enabled=").Append(renderer.enabled)
                    .Append(" active=").Append(renderer.gameObject.activeInHierarchy)
                    .Append(" verts=").Append(filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertexCount : -1)
                    .Append(" mat=").Append(material != null ? material.name : "none")
                    .Append(" shader=").Append(material != null && material.shader != null ? material.shader.name : "none")
                    .Append(" tex=").Append(texture != null ? texture.name + " " + texture.width + "x" + texture.height : "none")
                    .Append(" order=").Append(renderer.sortingOrder)
                    .Append(" layer=").Append(renderer.sortingLayerName);
                if (++shown >= 20)
                {
                    text.Append(" | ...");
                    break;
                }
            }
            return text.Length == 0 ? "no renderer matching " + needle : text.ToString();
        }

        /// <summary>
        /// Bridge probe: activate one catalog template, run the same start-of-run pass a placed
        /// part gets (`RoverPortPart.EnsureRigidbody`), and report every renderer's state before
        /// and after. This is the exact code path that decides which of the two artwork copies
        /// (build view vs running view) is switched on, so it shows which sprite the run-time
        /// switch turns off - the "the wheel lost its tyre" report.
        /// </summary>
        public static string EnsureProbe(string partName)
        {
            RoverPortCatalog catalog = RoverPortCatalog.Instance;
            GameObject part = null;
            foreach (GameObject candidate in catalog.Parts)
            {
                if (candidate != null && candidate.name == partName)
                {
                    part = candidate;
                    break;
                }
            }
            if (part == null)
            {
                return "no template named " + partName;
            }
            StringBuilder text = new StringBuilder();
            Transform host = part.transform.root;
            host.gameObject.SetActive(true);
            part.SetActive(true);
            text.Append("before").Append(DumpRenderers(part));
            RoverPortPart portPart = part.GetComponent<RoverPortPart>();
            if (portPart == null)
            {
                return text.Append(" | no RoverPortPart").ToString();
            }
            portPart.EnsureRigidbody();
            text.Append(" | after").Append(DumpRenderers(part));
            // Leave it standing in the open next to the bridge's camera so the render can be
            // photographed; CatalogProbe("hide") puts the host back.
            s_probeHost = host;
            s_probeRestorePosition = host.position;
            host.position = new Vector3(0f, 50f, 0f);
            return text.Append(" | AT ").Append(host.position)
                .Append(" partPos=").Append(part.transform.position).ToString();
        }

        private static string DumpRenderers(GameObject part)
        {
            StringBuilder text = new StringBuilder();
            foreach (MeshRenderer renderer in part.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                text.Append(" [").Append(renderer.transform.parent == null ? renderer.name : renderer.transform.parent.name + "/" + renderer.name)
                    .Append(" enabled=").Append(renderer.enabled)
                    .Append(" verts=").Append(filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertexCount : -1)
                    .Append(" tex=").Append(renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null
                        ? renderer.sharedMaterial.mainTexture.name : "none")
                    .Append(" order=").Append(renderer.sortingOrder).Append("]");
            }
            return text.ToString();
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return path;
        }

        internal static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    return field;
                }
            }
            return null;
        }

        // "show"/"hide" swap these while a bridge screenshot is taken.
        private static Transform s_probeHost;
        private static Vector3 s_probeRestorePosition;
        private static bool s_probeWasActive;
        private static readonly List<GameObject> s_probeParts = new List<GameObject>();

        /// <summary>
        /// Bridge entry point for the RC1 run-time checks (temporary, like the rest of this file).
        ///
        /// The DSH bridge can only find *active* objects by name and cannot instantiate, and
        /// <see cref="RoverPortCatalog"/> keeps its part templates under an inactive host in the
        /// DontDestroyOnLoad scene - so the templates are unreachable from the bridge directly.
        /// This goes through the singleton instead and reports what the catalog ACTUALLY built:
        ///
        ///   mode "report" - every ported particle system's texture-sheet curve mode (`Curve` once
        ///                   the curve keys are objects JsonUtility can deserialize, `Constant`
        ///                   while they were a nested array it silently dropped - which pinned
        ///                   `frameOverTime` to the smoke sheet's empty last tile), plus the wheel
        ///                   pivots each deserialized record carries.
        ///   mode "show"   - parks the template host in the open, activates it and plays the
        ///                   exhaust plume so a screenshot can prove the smoke draws.
        ///   mode "hide"   - stops the plume and puts the host back the way it was.
        /// </summary>
        public static string CatalogProbe(string mode)
        {
            RoverPortCatalog catalog = RoverPortCatalog.Instance;
            StringBuilder text = new StringBuilder();
            if (catalog == null)
            {
                return "no catalog";
            }
            IReadOnlyList<GameObject> parts = catalog.Parts;
            text.Append("parts=").Append(parts.Count);
            GameObject plumePart = null;
            ParticleSystem plume = null;
            s_probeParts.Clear();
            foreach (GameObject part in parts)
            {
                if (part == null)
                {
                    continue;
                }
                bool hasDriver = false;
                foreach (RoverPortParticleDriver driver in part.GetComponentsInChildren<RoverPortParticleDriver>(true))
                {
                    hasDriver = true;
                    ParticleSystem system = driver.System;
                    if (system == null)
                    {
                        continue;
                    }
                    ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
                    text.Append(" | ").Append(part.name).Append('/').Append(driver.Driver)
                        .Append(" curve=").Append(sheet.frameOverTime.mode)
                        .Append(" tiles=").Append(sheet.numTilesX)
                        .Append(" fps=").Append(sheet.fps)
                        .Append(" playing=").Append(system.isPlaying)
                        .Append(" count=").Append(system.particleCount);
                    if (driver.Driver == "FxSmoke")
                    {
                        plumePart = part;
                        plume = system;
                    }
                }
                if (hasDriver)
                {
                    s_probeParts.Add(part);
                }
                RoverPortPart portPart = part.GetComponent<RoverPortPart>();
                if (portPart == null || s_portDataField == null)
                {
                    continue;
                }
                RoverPortPartData data = s_portDataField.GetValue(portPart) as RoverPortPartData;
                if (data == null || data.motion == null || data.motion.wheel_pivots == null)
                {
                    continue;
                }
                foreach (RoverPortWheelPivotData pivot in data.motion.wheel_pivots)
                {
                    text.Append(" | ").Append(data.id)
                        .Append(" pivot=").Append(pivot.node)
                        .Append(" travel=").Append(pivot.travel.ToString("F4"))
                        .Append(" hz=").Append(pivot.spring_frequency.ToString("F1"))
                        .Append(" damp=").Append(pivot.damping.ToString("F2"));
                }
            }
            if (mode == "show" && plumePart != null && plume != null)
            {
                s_probeHost = plumePart.transform.root;
                s_probeRestorePosition = s_probeHost.position;
                s_probeWasActive = s_probeHost.gameObject.activeSelf;
                s_probeHost.position = new Vector3(0f, 50f, 0f);
                // The host AND the part template are both inactive: the port keeps every template
                // switched off, so activating the host alone still leaves the part out of the
                // hierarchy and the bridge cannot find it by name.
                s_probeHost.gameObject.SetActive(true);
                plumePart.SetActive(true);
                for (int i = 0; i < s_probeParts.Count; i++)
                {
                    s_probeParts[i].SetActive(true);
                }
                RoverPortParticleDriver driver = plume.GetComponent<RoverPortParticleDriver>();
                if (driver != null)
                {
                    driver.enabled = false;
                }
                ParticleSystem.MainModule main = plume.main;
                main.playOnAwake = true;
                plume.Play(true);
                text.Append(" | SHOW host=").Append(s_probeHost.name)
                    .Append(" pos=").Append(s_probeHost.position)
                    .Append(" part=").Append(plumePart.name)
                    .Append(" partActive=").Append(plumePart.activeInHierarchy)
                    .Append(" plumeActive=").Append(plume.gameObject.activeInHierarchy);
            }
            else if (mode == "hide" && s_probeHost != null)
            {
                if (plume != null)
                {
                    plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ParticleSystem.MainModule main = plume.main;
                    main.playOnAwake = false;
                    RoverPortParticleDriver driver = plume.GetComponent<RoverPortParticleDriver>();
                    if (driver != null)
                    {
                        driver.enabled = true;
                    }
                    if (plumePart != null)
                    {
                        plumePart.SetActive(false);
                    }
                }
                for (int i = 0; i < s_probeParts.Count; i++)
                {
                    if (s_probeParts[i] != null)
                    {
                        s_probeParts[i].SetActive(false);
                    }
                }
                s_probeParts.Clear();
                s_probeHost.position = s_probeRestorePosition;
                s_probeHost.gameObject.SetActive(s_probeWasActive);
                text.Append(" | HIDDEN");
                s_probeHost = null;
            }
            return text.ToString();
        }

        internal static float ReadFloat(FieldInfo field, object target)
        {
            return field == null || target == null ? 0f : Convert.ToSingle(field.GetValue(target));
        }

        internal static bool ReadBool(FieldInfo field, object target)
        {
            return field != null && target != null && Convert.ToBoolean(field.GetValue(target));
        }

        internal static string ReadString(FieldInfo field, object target)
        {
            return field == null || target == null ? string.Empty : Convert.ToString(field.GetValue(target));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            try
            {
                Run();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(Prefix + " failed: " + exception);
            }
        }

        /// <summary>Logs the RC1 templates the catalog injected. Safe to call again.</summary>
        public static void Run()
        {
            IReadOnlyList<GameObject> parts = RoverPortCatalog.Instance.Parts;
            if (parts == null || parts.Count == 0)
            {
                Debug.Log(Prefix + " catalog is empty - rc1_catalog.json was not loaded");
                return;
            }

            StringBuilder report = new StringBuilder();
            int rc1Parts = 0;
            int otherParts = 0;
            int rendererTotal = 0;
            int enabledTotal = 0;
            int activeTotal = 0;
            int ctorTotal = 0;
            int ctorEnabledTotal = 0;
            int physTotal = 0;
            int physEnabledTotal = 0;
            int aliasedTotal = 0;
            int negativeOrders = 0;
            int unexpectedNegativeOrders = 0;
            int partsBehindBackground = 0;
            string firstUnexpectedOrder = string.Empty;
            string firstPartBehindBackground = string.Empty;
            int pointTotal = 0;
            int partsWithNoPoints = 0;
            int cpMissing = 0;
            int mountMismatch = 0;
            string firstMountMismatch = string.Empty;
            float colliderMaxOffset = 0f;
            int collidersOutsideFootprint = 0;
            // The wheel/particle gates (bugs 2 and 3, and the exhaust/flame gate of bug 1).
            int pivotTotal = 0;
            int pivotResolvedTotal = 0;
            int pivotRendererTotal = 0;
            int wheelsWithoutPivots = 0;
            int wheelsWithoutCollisionRule = 0;
            int particleDriverTotal = 0;
            int particleSystemResolved = 0;
            int particleOwnerResolved = 0;
            int cloneProbes = 0;
            int clonePivotResolved = 0;
            int cloneSystemResolved = 0;
            int cloneOwnerResolved = 0;
            int cloneSpinMissing = 0;
            string firstBrokenPivot = string.Empty;
            string firstNoPivotWheel = string.Empty;
            string firstBrokenParticle = string.Empty;

            foreach (GameObject partObject in parts)
            {
                if (partObject == null)
                {
                    continue;
                }
                RoverPortPart part = partObject.GetComponent<RoverPortPart>();
                if (part == null || !IsRC1Part(part))
                {
                    otherParts++;
                    continue;
                }
                rc1Parts++;

                List<Renderer> construction = GetRenderers(s_constructionField, part);
                List<Renderer> physics = GetRenderers(s_physicsField, part);
                HashSet<Renderer> physicsSet = new HashSet<Renderer>(physics);
                int aliased = 0;
                foreach (Renderer renderer in construction)
                {
                    if (renderer != null && physicsSet.Contains(renderer))
                    {
                        aliased++;
                    }
                }

                Renderer[] all = part.GetComponentsInChildren<Renderer>(true);
                int enabled = 0;
                int active = 0;
                int minOrder = int.MaxValue;
                int maxOrder = int.MinValue;
                foreach (Renderer renderer in all)
                {
                    if (renderer == null)
                    {
                        continue;
                    }
                    if (renderer.enabled)
                    {
                        enabled++;
                    }
                    if (renderer.gameObject.activeInHierarchy)
                    {
                        active++;
                    }
                    if (renderer.sortingOrder < minOrder)
                    {
                        minOrder = renderer.sortingOrder;
                    }
                    if (renderer.sortingOrder > maxOrder)
                    {
                        maxOrder = renderer.sortingOrder;
                    }
                    if (renderer.sortingOrder < 0)
                    {
                        negativeOrders++;
                        if (!IsIntentionalLowOrder(renderer))
                        {
                            unexpectedNegativeOrders++;
                            if (firstUnexpectedOrder.Length == 0)
                            {
                                firstUnexpectedOrder = part.PortId + " "
                                    + PathOf(renderer.transform, part.transform) + " order="
                                    + renderer.sortingOrder;
                            }
                        }
                    }
                }
                if (minOrder == int.MaxValue)
                {
                    minOrder = 0;
                    maxOrder = 0;
                }
                if (maxOrder < 0)
                {
                    partsBehindBackground++;
                    if (firstPartBehindBackground.Length == 0)
                    {
                        firstPartBehindBackground = part.PortId + " (orderMax=" + maxOrder + ")";
                    }
                }

                int ctorEnabled = CountEnabled(construction);
                int physEnabled = CountEnabled(physics);

                // --- connection points: what GetConnectionPoints() can actually see ---
                List<Vector3> points = new List<Vector3>(part.GetConnectionPoints());
                int cpHolders = 0;
                int pointNodes = 0;
                Transform[] allTransforms = part.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in allTransforms)
                {
                    if (candidate == null)
                    {
                        continue;
                    }
                    if (candidate.name == "ConnectionPoints")
                    {
                        cpHolders++;
                    }
                    else if (candidate.name.StartsWith("Point-", StringComparison.Ordinal))
                    {
                        pointNodes++;
                    }
                }
                int mounts = ReadMountPoints(part);
                if (points.Count == 0)
                {
                    partsWithNoPoints++;
                }
                if (cpHolders == 0)
                {
                    cpMissing++;
                }
                if (points.Count != mounts * 2)
                {
                    mountMismatch++;
                    if (firstMountMismatch.Length == 0)
                    {
                        firstMountMismatch = part.PortId + " (points=" + points.Count
                            + " mounts=" + mounts + ")";
                    }
                }
                pointTotal += points.Count;

                // --- colliders: nothing may sit outside the part's own cells ---
                int colliders = 0;
                float worstOffset = 0f;
                RoverPortPartData portData = s_portDataField == null
                    ? null : s_portDataField.GetValue(part) as RoverPortPartData;
                if (portData != null && portData.colliders != null)
                {
                    int xmin = 0, xmax = 0, ymin = 0, ymax = 0;
                    if (portData.grid != null && portData.grid.cells != null)
                    {
                        xmin = portData.grid.cells.xmin;
                        xmax = portData.grid.cells.xmax;
                        ymin = portData.grid.cells.ymin;
                        ymax = portData.grid.cells.ymax;
                    }
                    foreach (RoverPortColliderData collider in portData.colliders)
                    {
                        if (collider == null || collider.offset == null || collider.offset.Length < 2)
                        {
                            continue;
                        }
                        colliders++;
                        float x = collider.offset[0];
                        float y = collider.offset[1];
                        float magnitude = Mathf.Sqrt(x * x + y * y);
                        if (magnitude > worstOffset)
                        {
                            worstOffset = magnitude;
                        }
                        if (x < xmin - 1f || x > xmax + 1f || y < ymin - 1f || y > ymax + 1f)
                        {
                            collidersOutsideFootprint++;
                        }
                    }
                }
                if (worstOffset > colliderMaxOffset)
                {
                    colliderMaxOffset = worstOffset;
                }

                // --- the three bug gates -------------------------------------------
                // Everything here is read out of the catalog record and the built template, so it
                // is the same data RoverPortPart.AttachWheelMotion / RoverPortParticleDriver use.
                RoverPortPartData data = s_portDataField == null
                    ? null : s_portDataField.GetValue(part) as RoverPortPartData;
                string kind = data == null || data.motion == null ? string.Empty : data.motion.kind;
                bool passive = data != null && data.motion != null && data.motion.passive;
                RoverPortWheelPivotData[] pivots = data == null || data.motion == null || data.motion.wheel_pivots == null
                    ? new RoverPortWheelPivotData[0] : data.motion.wheel_pivots;
                int pivotResolved = 0;
                int pivotRenderersUnder = 0;
                foreach (RoverPortWheelPivotData pivot in pivots)
                {
                    Transform pivotNode = pivot == null ? null : part.ResolvePortNode(pivot.node);
                    if (pivotNode == null)
                    {
                        continue;
                    }
                    pivotResolved++;
                    foreach (Renderer renderer in PivotRenderers(pivot.node, construction, physics))
                    {
                        if (renderer == null)
                        {
                            continue;
                        }
                        for (Transform current = renderer.transform; current != null; current = current.parent)
                        {
                            if (current == pivotNode)
                            {
                                pivotRenderersUnder++;
                                break;
                            }
                        }
                    }
                }
                bool isWheel = string.Equals(kind, "wheel", StringComparison.Ordinal);
                bool hasCollisionRule = isWheel;
                RoverPortParticleDriver[] drivers = part.GetComponentsInChildren<RoverPortParticleDriver>(true);

                pivotTotal += pivots.Length;
                pivotResolvedTotal += pivotResolved;
                pivotRendererTotal += pivotRenderersUnder;
                if (isWheel && !passive && pivots.Length == 0)
                {
                    wheelsWithoutPivots++;
                    if (firstNoPivotWheel.Length == 0)
                    {
                        firstNoPivotWheel = part.PortId;
                    }
                }
                if (isWheel && !hasCollisionRule)
                {
                    wheelsWithoutCollisionRule++;
                }
                if (pivotResolved < pivots.Length && firstBrokenPivot.Length == 0)
                {
                    firstBrokenPivot = part.PortId;
                }
                particleDriverTotal += drivers.Length;
                foreach (RoverPortParticleDriver driver in drivers)
                {
                    if (driver == null)
                    {
                        continue;
                    }
                    if (driver.SystemResolved)
                    {
                        particleSystemResolved++;
                    }
                    else if (firstBrokenParticle.Length == 0)
                    {
                        firstBrokenParticle = part.PortId + "/" + driver.Driver;
                    }
                    if (driver.OwnerResolved)
                    {
                        particleOwnerResolved++;
                    }
                }

                rendererTotal += all.Length;
                enabledTotal += enabled;
                activeTotal += active;
                ctorTotal += construction.Count;
                ctorEnabledTotal += ctorEnabled;
                physTotal += physics.Count;
                physEnabledTotal += physEnabled;
                aliasedTotal += aliased;

                report.Append(Prefix)
                    .Append(" part=").Append(part.PortId)
                    .Append(" index=").Append(part.Index)
                    .Append(" renderers=").Append(all.Length)
                    .Append(" enabled=").Append(enabled)
                    .Append(" ctorList=").Append(construction.Count)
                    .Append(" ctorEnabled=").Append(ctorEnabled)
                    .Append(" physList=").Append(physics.Count)
                    .Append(" physEnabled=").Append(physEnabled)
                    .Append(" aliased=").Append(aliased)
                    .Append(" orderMin=").Append(minOrder)
                    .Append(" orderMax=").Append(maxOrder)
                    .Append(" activeInHierarchy=").Append(active)
                    .Append(" rootActive=").Append(partObject.activeSelf)
                    .Append(" points=").Append(points.Count)
                    .Append(" pointNodes=").Append(pointNodes)
                    .Append(" cpHolders=").Append(cpHolders)
                    .Append(" mounts=").Append(mounts)
                    .Append(" colliders=").Append(colliders)
                    .Append(" colliderMaxOffset=").Append(worstOffset.ToString("F3"))
                    .Append(" kind=").Append(kind.Length == 0 ? "-" : kind)
                    .Append(" pivots=").Append(pivots.Length)
                    .Append(" pivotResolved=").Append(pivotResolved)
                    .Append(" pivotRenderersUnder=").Append(pivotRenderersUnder)
                    .Append(" wheelCollisionRule=").Append(hasCollisionRule ? "on" : "off")
                    .Append(" particleDrivers=").Append(drivers.Length)
                    .Append('\n');

                AppendSet(report, "ctor", construction, part.transform);
                AppendSet(report, "phys", physics, part.transform);
                AppendPoints(report, points, part.transform);
                AppendPivots(report, pivots, part, construction, physics);
                AppendParticles(report, drivers);
                AppendCloneProbe(report, partObject, part, pivots, drivers,
                    ref cloneProbes, ref clonePivotResolved, ref cloneSystemResolved,
                    ref cloneOwnerResolved, ref cloneSpinMissing);
            }

            report.Append(Prefix)
                .Append(" summary parts=").Append(rc1Parts)
                .Append(" renderers=").Append(rendererTotal)
                .Append(" enabled=").Append(enabledTotal)
                .Append(" ctorEnabled=").Append(ctorEnabledTotal).Append('/').Append(ctorTotal)
                .Append(" physEnabled=").Append(physEnabledTotal).Append('/').Append(physTotal)
                .Append(" activeInHierarchy=").Append(activeTotal)
                .Append(" aliased=").Append(aliasedTotal)
                .Append(" negativeOrders=").Append(negativeOrders)
                .Append(" unexpectedNegativeOrders=").Append(unexpectedNegativeOrders)
                .Append(" intentionalLowOrders=").Append(negativeOrders - unexpectedNegativeOrders)
                .Append(" partsBehindBackground=").Append(partsBehindBackground)
                .Append(" otherPortedParts=").Append(otherParts)
                .Append(" points=").Append(pointTotal)
                .Append(" partsWithNoPoints=").Append(partsWithNoPoints)
                .Append(" cpMissing=").Append(cpMissing)
                .Append(" mountMismatch=").Append(mountMismatch)
                .Append(" colliderMaxOffset=").Append(colliderMaxOffset.ToString("F3"))
                .Append(" collidersOutsideFootprint=").Append(collidersOutsideFootprint)
                .Append(" pivots=").Append(pivotTotal)
                .Append(" pivotResolved=").Append(pivotResolvedTotal)
                .Append(" pivotRenderersUnder=").Append(pivotRendererTotal)
                .Append(" wheelsWithoutPivots=").Append(wheelsWithoutPivots)
                .Append(" wheelsWithoutCollisionRule=").Append(wheelsWithoutCollisionRule)
                .Append(" particleDrivers=").Append(particleDriverTotal)
                .Append(" particleSystemResolved=").Append(particleSystemResolved)
                .Append(" particleOwnerResolved=").Append(particleOwnerResolved)
                .Append(" cloneProbes=").Append(cloneProbes)
                .Append(" clonePivotResolved=").Append(clonePivotResolved)
                .Append(" cloneSystemResolved=").Append(cloneSystemResolved)
                .Append(" cloneOwnerResolved=").Append(cloneOwnerResolved)
                .Append(" cloneSpinMissing=").Append(cloneSpinMissing);

            Debug.Log(report.ToString());

            if (rc1Parts == 0)
            {
                Debug.LogWarning(Prefix + " no RC1 part in the catalog (" + otherParts
                    + " other ported part(s) are loaded) - RC1PortCatalog.Bootstrap() did not"
                    + " append them, or the catalog was rebuilt after it ran.");
                return;
            }
            if (aliasedTotal > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + aliasedTotal
                    + " renderer(s) are in both the construction and the physics list;"
                    + " ConfigureVisuals turns the part's own artwork off (visible while"
                    + " dragging, invisible once placed).");
            }
            // Negative orders are NOT a failure by themselves any more: the catalog copies MAIN's own
            // absolute orders, and MAIN keeps several layers below zero (a wheel's own background and
            // engine plates at -200 / -10 / -900 / -1100, the green wheel's second wheel layer and the
            // bumper at -100). The failure this check was written for is a part that ends up *entirely*
            // behind the level background, which is a part whose highest order is negative.
            // unexpectedNegativeOrders stays in the summary as information: it counts the negatives that
            // are not a "..._bg" / "..._e" plate, which is where a hand-edit would show up first.
            if (partsBehindBackground > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + partsBehindBackground + " RC1 part(s) have every"
                    + " layer at a negative sortingOrder (first: " + firstPartBehindBackground
                    + "), so the whole part draws behind the order-0 level background - the"
                    + " 'part is invisible once placed' report. " + negativeOrders
                    + " renderer(s) are negative in total, which is expected: MAIN itself keeps a"
                    + " wheel's background/engine plates below the tyre.");
            }
            if (rc1Parts > 0 && ctorEnabledTotal < ctorTotal)
            {
                Debug.LogWarning(Prefix + " FAIL: only " + ctorEnabledTotal + " of " + ctorTotal
                    + " construction renderer(s) are enabled - the build view shows the"
                    + " construction set, so those parts are invisible while placing them.");
            }
            if (partsWithNoPoints > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + partsWithNoPoints + " RC1 part(s) expose no"
                    + " connection point; RoverPortPart.GetConnectionPoints() finds nothing, so"
                    + " TryGetConnection can never match and CreateCustomJoints adds no joint -"
                    + " the part cannot be attached to anything (partsWithNoPoints=0 is healthy).");
            }
            if (cpMissing > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + cpMissing + " RC1 part(s) have no"
                    + " 'ConnectionPoints' node; the points under it are unreachable, because"
                    + " GetConnectionPoints() requires that ancestor.");
            }
            if (mountMismatch > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + mountMismatch + " RC1 part(s) expose a"
                    + " point count other than 2 x grid.mount_points (one point per mount per"
                    + " node tree); first: " + firstMountMismatch + ".");
            }
            if (collidersOutsideFootprint > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + collidersOutsideFootprint + " collider(s)"
                    + " sit outside their part's own cells (max offset " + colliderMaxOffset.ToString("F3")
                    + "); a collider tens of cells below its part spawns inside the ground and the"
                    + " solver throws the part out - that is the 'parts fly around' report.");
            }
            // --- bug 2: the wheels must roll ----------------------------------------------
            if (wheelsWithoutPivots > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + wheelsWithoutPivots + " driven RC1 wheel(s)"
                    + " carry no motion.wheel_pivots (first: " + firstNoPivotWheel + "), so"
                    + " RoverPortPart.AttachWheelMotion attaches nothing and RoverPortWheelSpin"
                    + " never turns the artwork - the 'RC1 wheels do not turn' report.");
            }
            if (pivotResolvedTotal < pivotTotal)
            {
                Debug.LogWarning(Prefix + " FAIL: only " + pivotResolvedTotal + " of " + pivotTotal
                    + " wheel pivot(s) resolve to a node (first broken part: " + firstBrokenPivot
                    + "); the runtime cannot turn what it cannot find.");
            }
            if (pivotTotal > 0 && pivotRendererTotal < pivotTotal)
            {
                Debug.LogWarning(Prefix + " FAIL: " + pivotRendererTotal + " renderer(s) hang under"
                    + " a wheel pivot but there are " + pivotTotal + " pivots - at least one pivot"
                    + " wraps no artwork, so that wheel turns nothing.");
            }
            // --- bug 3: a wheel must ignore its own vehicle -------------------------------
            if (wheelsWithoutCollisionRule > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + wheelsWithoutCollisionRule + " RC1 wheel(s) do"
                    + " not get RoverPortContraptionCollision, so they still collide with the"
                    + " other parts of their own vehicle (the source makes them ignore every part:"
                    + " AndroidTrackMotion.RefreshConnections / OffRoadWheel.PostInitialize).");
            }
            // --- bug 1: the exhaust smoke and the thruster flame --------------------------
            if (particleDriverTotal == 0)
            {
                Debug.LogWarning(Prefix + " FAIL: no RoverPortParticleDriver in the catalog; the"
                    + " exhaust smoke and the thruster flame cannot exist at all.");
            }
            else if (particleSystemResolved < particleDriverTotal)
            {
                Debug.LogWarning(Prefix + " FAIL: only " + particleSystemResolved + " of "
                    + particleDriverTotal + " particle driver(s) can see their ParticleSystem"
                    + " (first: " + firstBrokenParticle + "). A driver that cannot see its system"
                    + " drops every state change in SetEmitting's null guard and the plume never"
                    + " starts.");
            }
            // The owning part is NOT required on a template: RoverPortParticleDriver.Resolve calls
            // GetComponentInParent<RoverPortPart>(), and GetComponentInParent skips inactive parents -
            // the catalog builds every template with root.SetActive(false), so a template cannot see
            // its owner until it is placed and activated. The clone lines below are the decisive test,
            // and they are the path a placed part actually takes.
            if (cloneProbes > 0 && cloneSystemResolved < particleDriverTotal)
            {
                Debug.LogWarning(Prefix + " FAIL: on the cloned (placed) parts only "
                    + cloneSystemResolved + " of " + particleDriverTotal + " particle driver(s)"
                    + " re-resolved their ParticleSystem. ConstructionUI.SetPartAt places a part"
                    + " with Object.Instantiate, which copies serialized fields only, so a driver"
                    + " that does not re-resolve turns the plume off for good - exactly the"
                    + " reported 'exhaust smoke and thruster flame do not work'.");
            }
            if (cloneProbes > 0 && cloneOwnerResolved < particleDriverTotal)
            {
                Debug.LogWarning(Prefix + " FAIL: on the cloned (placed) parts only "
                    + cloneOwnerResolved + " of " + particleDriverTotal + " particle driver(s)"
                    + " re-resolved the part they drive. RoverPortParticleDriver.Resolve takes it"
                    + " from GetComponentInParent, so a driver that cannot find its part drops"
                    + " every SetEmitting state change and the plume never starts.");
            }
            if (cloneProbes > 0 && cloneSpinMissing > 0)
            {
                Debug.LogWarning(Prefix + " FAIL: " + cloneSpinMissing + " cloned wheel part(s)"
                    + " could not re-resolve a wheel pivot node; RoverPortWheelSpin.Resolve would"
                    + " then have nothing to turn.");
            }
        }

        /// <summary>
        /// Whether a catalog template is one of the RC1 parts, by the id the catalog builder gives them.
        /// </summary>
        /// <remarks>
        /// Deliberately not <c>RC1PortCatalog.IsRC1Index</c>: that test is open-ended
        /// (<c>index &gt;= 4000</c>), and the RCR2 extra catalog hands out "highest index + 1", so its
        /// parts can land at or above that base - <c>part_mecanum_wheel</c> sits at 4032, one past RC1's
        /// own 4000..4031. Judging such a part by RC1 rules reports RC1 failures for a part that is not
        /// RC1: it has a single node tree, so it exposes one connection point per mount rather than two,
        /// and it carries no RC1 wheel pivot because nothing RC1 turns it.
        /// </remarks>
        internal static bool IsRC1Part(RoverPortPart part)
        {
            return part != null && part.PortId != null
                && part.PortId.StartsWith("part_rc1_", StringComparison.Ordinal);
        }

        /// <summary>
        /// Whether a renderer is *meant* to draw below its own part's artwork.
        /// </summary>
        /// <remarks>
        /// The catalog builder follows MAIN's own absolute sorting orders, and MAIN puts a wheel's own
        /// background plate and engine plate below the tyre: the small wheels use bg -200 / e -10, the
        /// bigfoot wheels bg -900, and one part carries a -1100. A negative order on a node named
        /// "...bg" or "..._e" is therefore the source's convention - not the old "drawn behind the
        /// ground" bug, which put -200 on parts whose source z was positive. Anything else negative is
        /// still a failure.
        /// </remarks>
        private static bool IsIntentionalLowOrder(Renderer renderer)
        {
            string name = renderer.gameObject.name;
            return name.EndsWith("bg", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_e", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "e", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The renderer set a wheel pivot wraps.
        /// </summary>
        /// <remarks>
        /// The builder emits the source tree twice - <c>ME:*</c> for the construction artwork the build
        /// view shows and <c>PE:*</c> for the artwork that runs - and lists every pivot once per tree
        /// (Tools/RC1/BuildRC1Catalog.ps1:927). Only the physics list hangs under a <c>PE:</c> pivot and
        /// only the construction list under an <c>ME:</c> pivot, so counting one list against both
        /// reported half of the pivots as wrapping nothing.
        /// </remarks>
        private static List<Renderer> PivotRenderers(string nodeId, List<Renderer> construction,
            List<Renderer> physics)
        {
            return nodeId != null && nodeId.StartsWith("PE:", StringComparison.Ordinal) ? physics : construction;
        }

        /// <summary>Reads grid.mount_points out of the part's port data (0 when absent).</summary>
        private static int ReadMountPoints(RoverPortPart part)
        {
            if (s_portDataField == null)
            {
                return 0;
            }
            RoverPortPartData data = s_portDataField.GetValue(part) as RoverPortPartData;
            return data == null || data.grid == null ? 0 : data.grid.mount_points;
        }

        /// <summary>
        /// One line per catalog wheel pivot. This is the data RoverPortWheelSpin is built from, so
        /// a line like
        ///     pivot[0] node=PE:wp name=WheelPivot radius=0.450 mode=surface source=MotorWheel
        ///              resolved=True renderersUnder=4
        /// is the healthy shape: the node exists, the radius is the source's own rolling radius,
        /// the mode names which MAIN component's rule applies, and the pivot really does wrap the
        /// artwork (renderersUnder &gt; 0). Broken shapes seen before the fix: no pivot lines at
        /// all (pivots=0), or resolved=False.
        /// </summary>
        private static void AppendPivots(StringBuilder report, RoverPortWheelPivotData[] pivots,
            RoverPortPart part, List<Renderer> construction, List<Renderer> physics)
        {
            for (int i = 0; i < pivots.Length; i++)
            {
                RoverPortWheelPivotData pivot = pivots[i];
                if (pivot == null)
                {
                    continue;
                }
                Transform node = part.ResolvePortNode(pivot.node);
                int under = 0;
                if (node != null)
                {
                    foreach (Renderer renderer in PivotRenderers(pivot.node, construction, physics))
                    {
                        if (renderer == null)
                        {
                            continue;
                        }
                        for (Transform current = renderer.transform; current != null; current = current.parent)
                        {
                            if (current == node)
                            {
                                under++;
                                break;
                            }
                        }
                    }
                }
                report.Append(Prefix)
                    .Append("   pivot[").Append(i).Append(']')
                    .Append(" node=").Append(pivot.node)
                    .Append(" name=").Append(node == null ? "<missing>" : node.name)
                    .Append(" radius=").Append(pivot.radius.ToString("F3"))
                    .Append(" mode=").Append(pivot.mode)
                    .Append(" source=").Append(pivot.source)
                    .Append(" resolved=").Append(node != null)
                    .Append(" renderersUnder=").Append(under)
                    .Append('\n');
            }
        }

        /// <summary>
        /// One line per rebuilt Android particle system, on the template.
        ///     particle[0] driver=PS_Fire_Blue system=True owner=True playing=False gate=False
        /// system/owner True is the healthy shape. gate is the value
        /// RoverPortPart.ShouldEmitParticles returns right now, which is False outside a running
        /// contraption for both systems by design (see the two Android drivers' own gates).
        /// </summary>
        private static void AppendParticles(StringBuilder report, RoverPortParticleDriver[] drivers)
        {
            for (int i = 0; i < drivers.Length; i++)
            {
                RoverPortParticleDriver driver = drivers[i];
                if (driver == null)
                {
                    continue;
                }
                ParticleSystem system = driver.System;
                report.Append(Prefix)
                    .Append("   particle[").Append(i).Append(']')
                    .Append(" driver=").Append(driver.Driver)
                    .Append(" system=").Append(system != null)
                    .Append(" owner=").Append(driver.OwnerResolved)
                    .Append(" playing=").Append(system != null && system.isPlaying)
                    .Append(" gate=").Append(driver.GateValue)
                    .Append('\n');
            }
        }

        /// <summary>
        /// Probes a real clone of the template, which is what the build view places:
        /// ConstructionUI.SetPartAt does <c>Object.Instantiate(part.gameObject)</c>, and
        /// Instantiate copies serialized state only. Activating the clone runs Awake/OnEnable, so
        /// this is where a non-serialized reference shows up as null. Healthy line:
        ///     clone part=part_rc1_1022 pivots=1 pivotsResolved=1 spins=0 drivers=1 system=1
        ///           owner=1 consumption=100.0 maxSpeedRpm=0.0 energyScale=0.000 enabled=False
        ///           gate=False
        /// The bug-1 broken state was system=0 on every clone (m_system was never re-resolved);
        /// the bug-2 broken state was everyone's rated speed 0 (WheelDriveForce's falloff is 0
        /// while RatedSurfaceSpeed is 0, so an RC1 wheel pushed nothing and never rolled).
        /// </summary>
        private static void AppendCloneProbe(StringBuilder report, GameObject partObject,
            RoverPortPart template, RoverPortWheelPivotData[] pivots, RoverPortParticleDriver[] drivers,
            ref int cloneProbes, ref int clonePivotResolved, ref int cloneSystemResolved,
            ref int cloneOwnerResolved, ref int cloneSpinMissing)
        {
            if (pivots.Length == 0 && drivers.Length == 0)
            {
                return;
            }
            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(partObject);
                clone.name = partObject.name + "_diagClone";
                clone.SetActive(true);
                RoverPortPart part = clone.GetComponent<RoverPortPart>();
                if (part == null)
                {
                    return;
                }
                cloneProbes++;
                int resolved = 0;
                foreach (RoverPortWheelPivotData pivot in pivots)
                {
                    if (pivot != null && part.ResolvePortNode(pivot.node) != null)
                    {
                        resolved++;
                    }
                }
                clonePivotResolved += resolved;
                if (resolved < pivots.Length)
                {
                    cloneSpinMissing++;
                }
                RoverPortParticleDriver[] cloneDrivers = clone.GetComponentsInChildren<RoverPortParticleDriver>(true);
                int systems = 0;
                int owners = 0;
                foreach (RoverPortParticleDriver driver in cloneDrivers)
                {
                    if (driver == null)
                    {
                        continue;
                    }
                    if (driver.SystemResolved)
                    {
                        systems++;
                    }
                    if (driver.OwnerResolved)
                    {
                        owners++;
                    }
                }
                cloneSystemResolved += systems;
                cloneOwnerResolved += owners;
                RoverPortPoweredPart powered = clone.GetComponent<RoverPortPoweredPart>();
                report.Append(Prefix)
                    .Append("   clone part=").Append(template.PortId)
                    .Append(" pivots=").Append(pivots.Length)
                    .Append(" pivotsResolved=").Append(resolved)
                    .Append(" spins=").Append(clone.GetComponentsInChildren<RoverPortWheelSpin>(true).Length)
                    .Append(" drivers=").Append(cloneDrivers.Length)
                    .Append(" system=").Append(systems)
                    .Append(" owner=").Append(owners)
                    .Append(" kind=").Append(RC1PortDiagnostics.ReadString(RC1PortDiagnostics.s_motionKindField, powered))
                    .Append(" consumption=").Append(ReadFloat(s_powerConsumptionField, powered).ToString("F1"))
                    .Append(" maxSpeedRpm=").Append(RC1PortDiagnostics.ReadFloat(RC1PortDiagnostics.s_maximumSpeedField, powered).ToString("F1"))
                    .Append(" energyScale=").Append(RC1PortDiagnostics.ReadFloat(RC1PortDiagnostics.s_energyScaleField, powered).ToString("F3"))
                    .Append(" enabled=").Append(ReadBool(s_enabledField, powered))
                    .Append('\n');
            }
            finally
            {
                if (clone != null)
                {
                    UnityEngine.Object.Destroy(clone);
                }
            }
        }

        private static void AppendPoints(StringBuilder report, List<Vector3> points, Transform root)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 world = points[i];
                Vector3 local = root == null ? world : root.InverseTransformPoint(world);
                report.Append(Prefix)
                    .Append("   point[").Append(i).Append(']')
                    .Append(" world=").Append(Vector(world))
                    .Append(" local=").Append(Vector(local))
                    .Append('\n');
            }
        }

        private static int CountEnabled(List<Renderer> renderers)
        {
            int count = 0;
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.enabled)
                {
                    count++;
                }
            }
            return count;
        }

        private static void AppendSet(StringBuilder report, string kind, List<Renderer> renderers, Transform root)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    report.Append(Prefix).Append("   ").Append(kind).Append('[').Append(i)
                        .Append("] <destroyed>\n");
                    continue;
                }
                Transform node = renderer.transform;
                Vector3 position = node.position;
                Vector3 scale = node.lossyScale;
                Vector3 local = root == null ? position : root.InverseTransformPoint(position);
                report.Append(Prefix)
                    .Append("   ").Append(kind).Append('[').Append(i).Append(']')
                    .Append(" node=").Append(renderer.gameObject.name)
                    .Append(" path=").Append(PathOf(node, root))
                    .Append(" layerID=").Append(renderer.sortingLayerID)
                    .Append(" layer=").Append(renderer.sortingLayerName)
                    .Append(" order=").Append(renderer.sortingOrder)
                    .Append(" enabled=").Append(renderer.enabled)
                    .Append(" active=").Append(node.gameObject.activeInHierarchy)
                    .Append(" pos=").Append(Vector(position))
                    .Append(" scale=").Append(Vector(scale))
                    .Append(" relPos=").Append(Vector(local))
                    .Append('\n');
            }
        }

        private static List<Renderer> GetRenderers(FieldInfo field, RoverPortPart part)
        {
            if (field == null)
            {
                return new List<Renderer>();
            }
            List<Renderer> value = field.GetValue(part) as List<Renderer>;
            return value ?? new List<Renderer>();
        }

        private static string PathOf(Transform node, Transform root)
        {
            if (node == null)
            {
                return "<null>";
            }
            List<string> names = new List<string>();
            for (Transform current = node; current != null && current != root; current = current.parent)
            {
                names.Add(current.name);
            }
            names.Reverse();
            string prefix = root == null ? string.Empty : root.name + "/";
            return prefix + string.Join("/", names.ToArray());
        }

        private static string Vector(Vector3 value)
        {
            return "(" + value.x.ToString("F3") + "," + value.y.ToString("F3") + "," + value.z.ToString("F3") + ")";
        }
    }

    /// <summary>
    /// TEMPORARY DIAGNOSTIC - SAFE TO DELETE (this type lives in RC1PortDiagnostics.cs).
    ///
    /// The scene-load dump above reports the *template* state, which cannot show whether a gate
    /// opens while somebody drives. This watcher does: every quarter second it looks at the RC1
    /// parts of the running contraption and logs one line whenever any value changes, so a plain
    /// in-game test answers all three reports from the player log:
    ///
    ///     [RC1Diag] run part=part_rc1_1009 running=True parts=13 powered=True enabled=True
    ///               consumption=100.0 kind=wheel rpm=900.0 speed=15.000 energyScale=1.000
    ///               thrust=0.0 pivots=2 angle=-142.5 spin=15.000 ignored=12/12 colliders=2
    ///               drivers=0 gate=0 emitting=0
    ///
    /// Read it like this:
    ///   * bug 2 - `rpm=900.0 speed=15.000` while a wheel is engaged (broken: 0.0 / 0.000, i.e.
    ///     WheelDriveForce's falloff is 0 and the wheel pushes nothing) and `angle` **changing**
    ///     while the vehicle moves (broken: angle stays 0.0 - the artwork never turns).
    ///   * bug 3 - `ignored` must reach `parts - 1` (12/12 here) as soon as the vehicle runs: the
    ///     wheel then ignores every other part on the same vehicle, so `colliders` of the wheel can
    ///     no longer sit inside the frame. Broken: ignored=0/12.
    ///   * bug 1 - on the exhaust part `gate`/`emitting` go 1 while any powered part is engaged
    ///     (`powered=True`), and on a thruster `gate=1 emitting=1 thrust=1500.0` while it is switched
    ///     on and drawing (`enabled=True energyScale&gt;0`). Broken: emitting stays 0 even with
    ///     gate=1, which is the "the plume never starts" state the clone bug produced.
    ///   * the thruster itself must also show `thrust=1500.0`: a `thruster` part pushes with a flat
    ///     1500 N along its own `transform.right` while it is on (see RoverPortPoweredPart
    ///     .DriveThruster). Broken: thrust stays 0.0.
    ///   * the run-time motion of an imported part (see the "sprung axle / connection spring /
    ///     ski split / exhaust scale / spoiler" section of Tools/RC1/RC1_CATALOG_NOTES.md):
    ///     - `travel=0.1234 travelMax=0.2602 hz=12.0` on the two bigfoot wheels, with `travel`
    ///       moving as the vehicle bounces (broken: `travelMax=0.0000`, the axle welded to its
    ///       mount, which is the pre-v5 one-rigid-body port).
    ///     - `spring=150.0` on the bigfoot wheels and the track, `spring=0.0` everywhere else -
    ///       the off-road connection spring that lets the wheel give vertically and not sideways.
    ///     - `downforce=25.0` (rising with speed, capped at 100) on the spoiler, `downforce=-` on
    ///       every other part (broken: `downforce=0.0` - a spoiler that pushes nothing).
    ///     - `ski=split` on the snowmobile ski once the contraption runs, `ski=whole` before that
    ///       and `ski=-` on every other part (broken: it stays `whole` for ever).
    /// </summary>
    public sealed class RC1GateWatcher : MonoBehaviour
    {
        private const string Prefix = "[RC1Diag]";
        private const float Interval = 0.25f;

        private readonly List<RoverPortPart> m_parts = new List<RoverPortPart>();
        private readonly Dictionary<int, string> m_seen = new Dictionary<int, string>();
        private float m_next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject host = new GameObject("RC1GateWatcher");
            DontDestroyOnLoad(host);
            host.AddComponent<RC1GateWatcher>();
        }

        private void Update()
        {
            if (Time.unscaledTime < m_next)
            {
                return;
            }
            m_next = Time.unscaledTime + Interval;
            m_parts.Clear();
            m_parts.AddRange(FindObjectsOfType<RoverPortPart>());
            foreach (RoverPortPart part in m_parts)
            {
                if (part == null || !RC1PortDiagnostics.IsRC1Part(part))
                {
                    continue;
                }
                Contraption contraption = part.contraption;
                if (contraption == null || !contraption.IsRunning)
                {
                    continue;
                }
                string line = Describe(part, contraption);
                int key = part.GetInstanceID();
                string previous;
                if (m_seen.TryGetValue(key, out previous) && previous == line)
                {
                    continue;
                }
                m_seen[key] = line;
                Debug.Log(line);
            }
        }

        private static string Describe(RoverPortPart part, Contraption contraption)
        {
            RoverPortPoweredPart powered = part as RoverPortPoweredPart;
            RoverPortWheelSpin[] spins = part.GetComponentsInChildren<RoverPortWheelSpin>(true);
            RoverPortContraptionCollision collision = part.GetComponent<RoverPortContraptionCollision>();
            RoverPortParticleDriver[] drivers = part.GetComponentsInChildren<RoverPortParticleDriver>(true);
            float rpm = RC1PortDiagnostics.ReadFloat(RC1PortDiagnostics.s_maximumSpeedField, powered);
            string kind = RC1PortDiagnostics.ReadString(RC1PortDiagnostics.s_motionKindField, powered);
            float energyScale = RC1PortDiagnostics.ReadFloat(RC1PortDiagnostics.s_energyScaleField, powered);
            // What the drive step applies right now: a thruster pushes only while it is switched on
            // and drawing (see RoverPortPoweredPart.DriveThruster).
            bool thrusting = string.Equals(kind, "thruster", System.StringComparison.Ordinal);
            float thrust = thrusting && part.IsEnabled() && energyScale > 0f
                ? RoverPortPoweredPart.ThrusterThrust
                : 0f;
            int gates = 0;
            int emitting = 0;
            foreach (RoverPortParticleDriver driver in drivers)
            {
                if (driver == null)
                {
                    continue;
                }
                if (driver.GateValue)
                {
                    gates++;
                }
                ParticleSystem system = driver.System;
                if (system != null && system.isPlaying)
                {
                    emitting++;
                }
            }
            StringBuilder line = new StringBuilder();
            line.Append(Prefix)
                .Append(" run part=").Append(part.PortId)
                .Append(" running=True")
                .Append(" parts=").Append(contraption.Parts.Count)
                .Append(" powered=").Append(contraption.SomePoweredPartsEnabled())
                .Append(" enabled=").Append(part.IsEnabled())
                .Append(" consumption=").Append(part.m_powerConsumption.ToString("F1"))
                .Append(" kind=").Append(kind)
                .Append(" rpm=").Append(rpm.ToString("F1"))
                .Append(" speed=").Append(powered == null ? "0.000" : powered.RatedGroundSpeed(rpm).ToString("F3"))
                .Append(" energyScale=").Append(energyScale.ToString("F3"))
                .Append(" thrust=").Append(thrust.ToString("F1"))
                .Append(" pivots=").Append(spins.Length);
            if (spins.Length > 0 && spins[0] != null)
            {
                line.Append(" node=").Append(spins[0].Node)
                    .Append(" angle=").Append(spins[0].Angle.ToString("F1"))
                    .Append(" spin=").Append(spins[0].SpinSpeed.ToString("F3"));
                // The sprung axle of MAIN's AndroidBigfootMotion: travelMax is the source's own
                // serialized travel and travel is where the axle is this frame. Broken (the
                // pre-v5 one-rigid-body port): travelMax=0.000 and travel can only ever be 0.
                if (spins[0].Travel > 0f)
                {
                    line.Append(" travel=").Append(spins[0].TravelOffset.ToString("F4"))
                        .Append(" travelMax=").Append(spins[0].Travel.ToString("F4"))
                        .Append(" hz=").Append(spins[0].SpringFrequency.ToString("F1"));
                }
            }
            RoverPortSpoiler spoiler = part.GetComponent<RoverPortSpoiler>();
            RoverPortSkiMotion ski = part.GetComponent<RoverPortSkiMotion>();
            line.Append(" spring=").Append(part.SourceWheelSpringStiffness.ToString("F1"))
                .Append(" downforce=").Append(spoiler == null ? "-" : spoiler.LastDownforce.ToString("F1"))
                .Append(" ski=").Append(ski == null ? "-" : (ski.IsSplit ? "split" : "whole"))
                .Append(" ignored=").Append(collision == null ? -1 : collision.IgnoredPartCount)
                .Append('/').Append(contraption.Parts.Count - 1)
                .Append(" colliders=")
                .Append(collision == null ? part.GetComponentsInChildren<Collider>(true).Length
                                          : collision.PhysicalColliders.Length)
                .Append(" drivers=").Append(drivers.Length)
                .Append(" gate=").Append(gates)
                .Append(" emitting=").Append(emitting);
            return line.ToString();
        }
    }
}
