using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Rebuilds the Android particle systems the RC1 import carries (MAIN's
    /// AndroidPartImporter.BuildThrusterFlame and .BuildExhaustSmoke write them onto the
    /// prefab, and Tools/RC1/BuildRC1Catalog.ps1 reads them back out of it).
    ///
    /// Nothing here is invented: duration, loop, simulation space, emission rate,
    /// lifetime, speed, size, the seven frame UV sheet, the size/rotation over lifetime
    /// curves and the sorting order all come from the prefab, so a rebuilt system matches
    /// the one MAIN instantiates. The two Android drivers are represented by
    /// <see cref="RoverPortParticleDriver"/>, which starts the system stopped and lets the
    /// vehicle switch it on.
    /// </summary>
    public static class RoverPortParticles
    {
        public static void Build(Dictionary<string, Transform> nodes, RoverPortPartData data, Func<string, Texture2D> textureResolver)
        {
            if (data == null || data.particles == null)
            {
                return;
            }
            for (int i = 0; i < data.particles.Length; i++)
            {
                RoverPortParticleData particle = data.particles[i];
                if (particle != null)
                {
                    BuildOne(nodes, particle, textureResolver);
                }
            }
        }

        private static void BuildOne(Dictionary<string, Transform> nodes, RoverPortParticleData data, Func<string, Texture2D> textureResolver)
        {
            GameObject host = new GameObject(string.IsNullOrEmpty(data.name) ? "Particles" : data.name);
            Transform parent = null;
            if (nodes != null && !string.IsNullOrEmpty(data.node))
            {
                nodes.TryGetValue(data.node, out parent);
            }
            if (parent != null)
            {
                host.transform.SetParent(parent, false);
            }
            host.transform.localPosition = ToVector3(data.local_position, Vector3.zero);
            host.transform.localRotation = ToQuaternion(data.local_rotation);

            ParticleSystem system = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = Mathf.Max(0.05f, data.duration);
            main.loop = data.loop;
            main.prewarm = data.prewarm;
            main.playOnAwake = data.play_on_awake;
            main.simulationSpace = data.simulation_space == 0
                ? ParticleSystemSimulationSpace.Local
                : ParticleSystemSimulationSpace.World;
            main.scalingMode = (ParticleSystemScalingMode)Mathf.Clamp(data.scaling_mode, 0, 2);
            // Unity's ParticleSystemCullingMode is Automatic / PauseAndCatchup / Pause / AlwaysSimulate,
            // so the range is 0..3. Clamping to 2 silently turned the exhaust's AlwaysSimulate (MAIN
            // serialises 3) into Pause, which stops the smoke feeding whenever the emitter leaves the
            // camera frustum.
            main.cullingMode = (ParticleSystemCullingMode)Mathf.Clamp(data.culling_mode, 0, 3);
            main.maxParticles = Mathf.Max(1, data.max_particles);
            main.gravityModifier = data.gravity_modifier;
            main.startLifetime = ToCurve(data.start_lifetime, 1f);
            main.startSpeed = ToCurve(data.start_speed, 0f);
            main.startSize = ToCurve(data.start_size, 1f);
            main.startRotation = ToCurve(data.start_rotation, 0f);
            if (data.start_color != null && data.start_color.Length >= 4)
            {
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(data.start_color[0], data.start_color[1], data.start_color[2], data.start_color[3]));
            }
            // MAIN's owning component can scale the whole plume. Exhaust does, every frame
            // (`ApplySmokeSize` -> startSizeMultiplier = smokeSizeFactor = 1.8), leaving the
            // serialized startSize at the Android-derived 0.8130 while the visible puffs are 1.8x
            // that. The catalog exports the factor; without it the port smoked at a little over
            // half the size MAIN shows.
            if (data.size_multiplier > 0f && !Mathf.Approximately(data.size_multiplier, 1f))
            {
                main.startSizeMultiplier = data.size_multiplier;
            }

            ParticleSystem.ShapeModule shape = system.shape;
            if (data.shape != null)
            {
                shape.enabled = data.shape.enabled;
                shape.shapeType = (ParticleSystemShapeType)Mathf.Clamp(data.shape.type, 0, 20);
                shape.angle = data.shape.angle;
                shape.radius = data.shape.radius;
                shape.arc = data.shape.arc;
                shape.radiusThickness = data.shape.radius_thickness;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            if (data.emission != null)
            {
                emission.enabled = data.emission.enabled;
                emission.rateOverTime = ToCurve(data.emission.rate_over_time, 0f);
                emission.rateOverDistance = ToCurve(data.emission.rate_over_distance, 0f);
            }

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            if (data.size_over_lifetime != null)
            {
                sizeOverLifetime.enabled = data.size_over_lifetime.enabled;
                sizeOverLifetime.size = ToCurve(data.size_over_lifetime.size, 1f);
            }

            ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = system.rotationOverLifetime;
            if (data.rotation_over_lifetime != null)
            {
                rotationOverLifetime.enabled = data.rotation_over_lifetime.enabled;
                // MAIN authors this in radians, which is what the module expects.
                rotationOverLifetime.z = ToCurve(data.rotation_over_lifetime.z, 0f);
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            if (data.color_over_lifetime != null)
            {
                colorOverLifetime.enabled = data.color_over_lifetime.enabled;
            }

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            if (data.velocity_over_lifetime != null)
            {
                velocity.enabled = data.velocity_over_lifetime.enabled;
                velocity.space = data.velocity_over_lifetime.space == 0
                    ? ParticleSystemSimulationSpace.Local
                    : ParticleSystemSimulationSpace.World;
                velocity.x = ToCurve(data.velocity_over_lifetime.x, 0f);
                velocity.y = ToCurve(data.velocity_over_lifetime.y, 0f);
                velocity.z = ToCurve(data.velocity_over_lifetime.z, 0f);
                velocity.speedModifier = ToCurve(data.velocity_over_lifetime.speed_modifier, 1f);
            }

            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            if (data.texture_sheet != null)
            {
                sheet.enabled = data.texture_sheet.enabled;
                // data.mode and data.time_mode are 0 on every cached system, which is
                // Unity's own default (Grid tiles, timed over the particle's lifetime),
                // so neither is assigned here and neither can drift.
                sheet.fps = data.texture_sheet.fps;
                sheet.numTilesX = Mathf.Max(1, data.texture_sheet.tiles_x);
                sheet.numTilesY = Mathf.Max(1, data.texture_sheet.tiles_y);
                sheet.animation = (ParticleSystemAnimationType)Mathf.Clamp(data.texture_sheet.animation, 0, 2);
                sheet.frameOverTime = ToCurve(data.texture_sheet.frame_over_time, 1f);
                sheet.startFrame = ToCurve(data.texture_sheet.start_frame, 0f);
                if (data.texture_sheet.cycles > 0f) sheet.cycleCount = Mathf.RoundToInt(data.texture_sheet.cycles);
            }

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = data.sorting_order;
                if (data.renderer != null)
                {
                    renderer.renderMode = (ParticleSystemRenderMode)Mathf.Clamp(data.renderer.render_mode, 0, 5);
                    renderer.sortMode = (ParticleSystemSortMode)Mathf.Clamp(data.renderer.sort_mode, 0, 4);
                    renderer.minParticleSize = data.renderer.min_particle_size;
                    renderer.maxParticleSize = data.renderer.max_particle_size;
                    renderer.lengthScale = data.renderer.length_scale;
                    renderer.normalDirection = data.renderer.normal_direction;
                    renderer.alignment = (ParticleSystemRenderSpace)Mathf.Clamp(data.renderer.alignment, 0, 3);
                    renderer.pivot = ToVector3(data.renderer.pivot, Vector3.zero);
                    renderer.flip = ToVector3(data.renderer.flip, Vector3.zero);
                }
                Material material = BuildMaterial(data, textureResolver);
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            // MAIN's systems are scenery on the prefab; the vehicle starts them.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            RoverPortParticleDriver driver = host.AddComponent<RoverPortParticleDriver>();
            driver.Configure(system, data);
        }

        private static Material BuildMaterial(RoverPortParticleData data, Func<string, Texture2D> textureResolver)
        {
            string shaderName = string.IsNullOrEmpty(data.shader) ? "Sprites/Default" : data.shader;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                Debug.LogError("[RCR2Port] Missing particle shader for " + data.name);
                return null;
            }
            Material material = new Material(shader);
            material.name = "RCR2_particle_" + data.name;
            Texture2D texture = textureResolver == null ? null : textureResolver(data.texture_guid);
            if (texture != null)
            {
                material.mainTexture = texture;
            }
            return material;
        }

        /// <summary>
        /// A MinMaxCurve carries either two constants or a curve. MAIN's thruster flame
        /// uses a curve (1 -> 0.5528 over the particle's life) and its rotation uses two
        /// constants, so both shapes have to survive the round trip.
        /// </summary>
        private static ParticleSystem.MinMaxCurve ToCurve(RoverPortMinMaxData data, float fallback)
        {
            if (data == null)
            {
                return new ParticleSystem.MinMaxCurve(fallback);
            }
            if (data.state == 3)
            {
                return new ParticleSystem.MinMaxCurve(data.min, data.max);
            }
            var keys = data.curve_max != null && data.curve_max.Length > 0 ? data.curve_max : data.curve_min;
            if ((data.state == 1 || data.state == 2) && keys != null && keys.Length > 0)
            {
                AnimationCurve curve = new AnimationCurve();
                for (int i = 0; i < keys.Length; i++)
                {
                    RoverPortCurveKey key = keys[i];
                    if (key.has_tangents)
                        curve.AddKey(new Keyframe(key.time, key.value, key.in_tangent, key.out_tangent));
                    else curve.AddKey(key.time, key.value);
                }
                if (curve.length > 0)
                {
                    return new ParticleSystem.MinMaxCurve(data.scalar == 0f ? 1f : data.scalar, curve);
                }
            }
            return new ParticleSystem.MinMaxCurve(data.scalar);
        }

        private static Vector3 ToVector3(float[] value, Vector3 fallback)
        {
            if (value == null || value.Length < 2)
            {
                return fallback;
            }
            return new Vector3(value[0], value[1], value.Length > 2 ? value[2] : 0f);
        }

        private static Quaternion ToQuaternion(float[] value)
        {
            if (value == null || value.Length < 4)
            {
                return Quaternion.identity;
            }
            return new Quaternion(value[0], value[1], value[2], value[3]);
        }
    }

    /// <summary>
    /// Stands in for the Android drivers MAIN attaches to the two rebuilt systems
    /// (AndroidThrusterFlame on PS_Fire_Blue, Exhaust on FxSmoke). It owns the system,
    /// starts it stopped, and then polls the part it belongs to once a frame, exactly
    /// like the two Android drivers did: AndroidThrusterFlame.Update() polled the
    /// thruster's own firing signal and Exhaust.Update() polled
    /// <c>contraption.IsRunning &amp;&amp; contraption.SomePoweredPartsEnabled()</c>. The
    /// state itself lives on <see cref="RoverPortPart.ShouldEmitParticles"/>, because
    /// only the owning part knows what "engaged" means for its driver.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class RoverPortParticleDriver : MonoBehaviour
    {
        [SerializeField] private string m_driver;
        private ParticleSystem m_system;
        private RoverPortPart m_owner;
        private bool m_emitting;

        /// <summary>The MAIN component this system belongs to ("PS_Fire_Blue" / "FxSmoke").</summary>
        public string Driver { get { return m_driver; } }

        /// <summary>The system this driver owns, resolved on demand (see <see cref="Resolve"/>).</summary>
        public ParticleSystem System
        {
            get
            {
                Resolve();
                return m_system;
            }
        }

        /// <summary>Whether this driver can see its system (diagnostics; false only if broken).</summary>
        public bool SystemResolved
        {
            get { return System != null; }
        }

        /// <summary>Whether this driver can see the part it belongs to (diagnostics).</summary>
        public bool OwnerResolved
        {
            get
            {
                Resolve();
                return m_owner != null;
            }
        }

        /// <summary>What the owning part answers right now (diagnostics; see ShouldEmitParticles).</summary>
        public bool GateValue
        {
            get
            {
                Resolve();
                return m_owner != null && m_owner.ShouldEmitParticles(m_driver);
            }
        }

        public void Configure(ParticleSystem system, RoverPortParticleData data)
        {
            m_system = system;
            m_driver = data == null ? string.Empty : data.driver;
            // MAIN serialises the smoke with playOnAwake = true and gates it inside its own
            // Exhaust.cs instead (see that file's header: "the exhaust must not smoke while
            // it sits in the build screen / parts list"). The port keeps the exported value in
            // the catalog but does the same at runtime: the driver is the only thing allowed
            // to start a rebuilt Android system, so a part cannot start smoking the moment it
            // is activated in the build view.
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            SetEmitting(false);
        }

        /// <summary>
        /// Re-resolves the system and the owner after Unity clones the part.
        ///
        /// <c>ConstructionUI.SetPartAt</c> places a part with
        /// <c>Object.Instantiate(part.gameObject)</c>, and Instantiate copies serialized state
        /// only. <c>m_system</c> and <c>m_owner</c> are private references with no
        /// <c>[SerializeField]</c>, so both are null in every placed part: <c>m_owner</c> was
        /// already re-resolved here, but <c>m_system</c> was not, and
        /// <c>SetEmitting</c>'s <c>m_system == null</c> guard then dropped every state change on
        /// the floor. The clone's system kept the state it was cloned with - built stopped by
        /// <see cref="RoverPortParticles.BuildOne"/> - so neither the exhaust smoke nor the
        /// thruster flame could ever start. This is the same lazy re-resolution MAIN's own
        /// <c>Exhaust.cs</c> uses (<c>smoke = GetComponentInChildren&lt;ParticleSystem&gt;(true)</c>
        /// inside the polling method): the driver sits on the system's own GameObject, so it can
        /// simply take it back.
        /// </summary>
        private void Resolve()
        {
            if (m_system == null)
            {
                m_system = GetComponent<ParticleSystem>();
                if (m_system == null)
                {
                    m_system = GetComponentInChildren<ParticleSystem>(true);
                }
            }
            if (m_owner == null)
            {
                m_owner = GetComponentInParent<RoverPortPart>();
            }
        }

        private void Update()
        {
            Resolve();
            SetEmitting(m_owner != null && m_owner.ShouldEmitParticles(m_driver));
        }

        private void FixedUpdate()
        {
            Resolve();
            if (m_driver == "PS_Fire_Blue" && m_system != null && GateValue)
            {
                SetEmitting(true);
                m_system.Emit(1);
            }
        }

        private void OnEnable()
        {
            // The template is built stopped and a clone copies that, so re-sync the
            // cached flag with it: a stale "emitting" would make Update's early-out skip
            // the stop and leave the system running for good.
            m_emitting = false;
            Resolve();
            if (m_system != null && m_system.isPlaying)
            {
                m_system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnDisable()
        {
            SetEmitting(false);
        }

        public void SetEmitting(bool emitting)
        {
            Resolve();
            if (m_system == null || emitting == m_emitting)
            {
                return;
            }
            m_emitting = emitting;
            if (emitting)
            {
                m_system.Play(true);
                return;
            }
            // Stop feeding the plume but let the particles already in flight fade out
            // over their own lifetime, like both Android drivers did. The clearing stop
            // is in BuildOne, so a part that comes back still starts from empty.
            m_system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
