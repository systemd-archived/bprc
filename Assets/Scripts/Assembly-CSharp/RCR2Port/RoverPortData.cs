using System;

namespace RCR2Port
{
    [Serializable]
    public sealed class RoverPortCatalogData
    {
        public int version;
        public string source;
        public RoverPortPartData[] parts;
        public RoverPortTextureData[] textures;
    }

    [Serializable]
    public sealed class RoverPortTextureData
    {
        public string guid;
        public string canonical_file;
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class RoverPortPartData
    {
        public string id;
        public string asset_name;
        public string display_name;
        public string description;
        public int category_code;
        public string category;
        public int biome;
        public int rarity;
        public float mass;
        public float friction;
        public float durability;
        public float durability_bonus;
        public float obstacle_damage;
        public float sink_factor_down;
        public bool use_sink_physics;
        public bool separate_joint_bodies;
        public float consumption;
        public int timer;
        public int first_amount;
        public int first_level;
        public int visibility;
        public string model_root;
        public string physics_root;
        public float[] center;
        public float[] rotation_center;
        public RoverPortGridData grid;
        public RoverPortNodeData[] nodes;
        public RoverPortRendererData[] renderers;
        public RoverPortRendererData[] construction_renderers;
        public RoverPortColliderData[] colliders;
        public RoverPortBodyData[] bodies;
        public RoverPortJointData[] joints;
        public RoverPortMotionData motion;
        public RoverPortSpriteData icon;
        public RoverPortBandData band;
        /// <summary>
        /// The imported Android spoiler's aerodynamic numbers (MAIN's own <c>Spoiler</c>
        /// component, serialized on the prefab). Null on every part that is not a spoiler, and on
        /// every other catalog.
        /// </summary>
        public RoverPortSpoilerData spoiler;
        /// <summary>
        /// Sprites that follow a rigidbody node at runtime (the original
        /// TransformFollower). A wheel whose artwork is carried by a small
        /// moving part - the mecanum rollers - needs them to keep the picture
        /// on the physics.
        /// </summary>
        public RoverPortFollowerData[] followers;
        /// <summary>
        /// Sprites rotated between two nodes (the original
        /// SpriteRotatorBy2Points), which is how the mecanum's rolling arms
        /// visually follow their rollers.
        /// </summary>
        public RoverPortStickRotatorData[] stick_rotators;
        /// <summary>
        /// The imported Android part class this record came from
        /// (Tools/RC1/BuildRC1Catalog.ps1). It is the parity contract: MAIN names the
        /// class on the prefab, so the port must resolve its BasePart.PartType from
        /// here rather than guessing from <see cref="category"/>. Empty for every
        /// other catalog, which keeps the old category/motion resolution intact.
        /// </summary>
        public string android_class;
        /// <summary>MAIN's own base component the class was grafted onto (MotorWheel, OffRoadWheel, Engine, JetEngine, ...). Audit only.</summary>
        public string base_component;
        /// <summary>
        /// Physics contract MAIN writes on the imported part. The values are per part and are
        /// copied out of each prefab (they are not uniform: 21 parts ship FixedJoint and 11
        /// HingeJoint, and the connection strength is 2, 3 or 4 depending on the part), which is
        /// why the catalog carries all six fields rather than a single derived default.
        /// </summary>
        public float break_velocity;
        public float interactive_radius;
        public int auto_align;
        public bool eight_way;
        public int joint_type;
        public int joint_connection_type;
        public int joint_connection_direction;
        public int joint_connection_strength;
        public bool joint_preprocessing;
        public float z_offset;
        /// <summary>Shader and tint MAIN uses for this part's sprites and its icon (Sprites/Default for the Android import).</summary>
        public RoverPortMaterialData material;
        public RoverPortMaterialData icon_material;
        /// <summary>The rebuilt Android particle systems carried by this part (thruster flame, exhaust smoke).</summary>
        public RoverPortParticleData[] particles;
    }

    /// <summary>Material contract: the shader MAIN uses plus the tint applied to every renderer.</summary>
    [Serializable]
    public sealed class RoverPortMaterialData
    {
        public string shader;
        public float[] color;
    }

    /// <summary>Bind one sprite node to a rigidbody node.</summary>
    [Serializable]
    public sealed class RoverPortFollowerData
    {
        public string node;
        public string target;
        public bool follow_position;
        public bool follow_rotation;
    }

    /// <summary>Rotate one sprite node so it points from point_a at point_b.</summary>
    [Serializable]
    public sealed class RoverPortStickRotatorData
    {
        public string node;
        public string point_a;
        public string point_b;
    }

    /// <summary>
    /// Rubber band of a tracked part. RCR2 generates the band at runtime (no prefab references the
    /// link sprite), so it has to be described separately from the renderer sets: the belt loop is
    /// the part's chain of hinge linked bodies (motion.wheel_bodies) and the sprite is one belt link.
    /// </summary>
    [Serializable]
    public sealed class RoverPortBandData
    {
        public string node;
        public string[] nodes;
        public float[] radii;
        public RoverPortSpriteData sprite;
        public float link_length;
        public float thickness;
        public int sorting_order;
    }

    /// <summary>
    /// Aerodynamics of an imported Android spoiler, copied verbatim out of MAIN's own
    /// <c>Spoiler</c> component on the prefab. MAIN adds a downward force every fixed step
    /// (<c>Spoiler.FixedUpdate</c>), clamped to <see cref="max_force"/>, and the four numbers are
    /// serialized per part rather than hard-coded in the class - so they belong in the catalog,
    /// not in the runtime.
    /// </summary>
    [Serializable]
    public sealed class RoverPortSpoilerData
    {
        /// <summary><c>Spoiler.liftCoefficient</c>. MAIN divides it by its own <c>AndroidLiftCoefficient</c> (30) to scale the force.</summary>
        public float lift_coefficient;
        /// <summary>Coefficient of the <c>velocity.sqrMagnitude</c> term.</summary>
        public float downforce_constant;
        /// <summary>The constant part of the downforce, in newtons.</summary>
        public float base_force;
        /// <summary>Upper bound MAIN clamps the force vector to, in newtons.</summary>
        public float max_force;
    }

    /// <summary>
    /// Build-grid alignment recovered from the RCR2 source by Tools/RCR2/GridAlignment.py.
    /// RCR2 authors a part's artwork on a 1x1 cell lattice anchored at <c>rotation_center</c>;
    /// <c>shift</c> is the half-cell offset the port adds so the artwork sits on BPLE cells,
    /// and <c>cells</c> is the resulting footprint relative to the part root.
    /// </summary>
    [Serializable]
    public sealed class RoverPortGridData
    {
        /// <summary>
        /// True when <see cref="cells"/> is the source's own build layout and must
        /// be used as this part's footprint.
        ///
        /// The flag exists because "this part has no layout" cannot be expressed
        /// as JSON <c>null</c>: Unity's <c>JsonUtility</c> materialises a null
        /// object field as a *default instance*, so an absent layout reads back
        /// as a zeroed 1x1 footprint at the origin rather than as "unknown".
        /// Without it the parts that must measure their own artwork (the eighteen
        /// RC3 drivers) would silently collapse to a single cell.
        /// </summary>
        public bool authoritative;

        public float[] phase;
        public float[] shift;
        public RoverPortGridCellsData cells;
        public float confidence;
        public float misfit;
        public int atypical_mounts;
        public int mount_points;
        /// <summary>
        /// Connection points the source declares for this part's cells, in
        /// part-local units. The extractor also emits them as
        /// <c>ConnectionPoints/Point-*</c> nodes (which is what the runtime
        /// actually anchors joints on); this list is what the audits check the
        /// node tree against.
        /// </summary>
        public RoverPortMountPointData[] mounts;
    }

    /// <summary>One declared connection point of a part's build grid.</summary>
    [Serializable]
    public sealed class RoverPortMountPointData
    {
        public string name;
        public float x;
        public float y;
        /// <summary>Cell edge this point sits on, in BPLE direction terms.</summary>
        public string side;
    }

    [Serializable]
    public sealed class RoverPortGridCellsData
    {
        public int xmin;
        public int xmax;
        public int ymin;
        public int ymax;
    }

    [Serializable]
    public sealed class RoverPortNodeData
    {
        public string id;
        public int source_id;
        public string parent;
        public string name;
        public bool active;
        public float[] position;
        public float[] rotation;
        public float[] scale;
    }

    [Serializable]
    public sealed class RoverPortSpriteData
    {
        public string guid;
        public string name;
        public string texture_guid;
        public string texture_file;
        public int texture_width;
        public int texture_height;
        public int x;
        public int y;
        public int width;
        public int height;
        public float pivot_x;
        public float pivot_y;
        public float ppu;
        public RoverPortVector2Data[] vertices;
        public RoverPortVector2Data[] uvs;
        public int[] triangles;
    }

    [Serializable]
    public sealed class RoverPortRendererData
    {
        public string node;
        public RoverPortSpriteData sprite;
        public float[] color;
        public bool flip_x;
        public bool flip_y;
        public bool enabled;
        public int sorting_order;
        public int sorting_layer_id;
    }

    [Serializable]
    public sealed class RoverPortColliderData
    {
        public string node;
        public string type;
        public bool enabled;
        public bool trigger;
        public float density;
        public float[] offset;
        public float[] size;
        /// <summary>
        /// BoxCollider depth. Catalogs whose source is a Unity prefab carry the real 3D size:
        /// MAIN's imported Android parts use BPLE's own dimensions - 1.5 on the thruster
        /// (Part_JetEngine is 1.8 x 0.7 x 1.5), 0.4 / 0.38 on the bigfoot wheel, 0.4 on the
        /// track, 0.4 / 0.22 on the ski, and 1 on a plain frame cell. Catalogs whose source is
        /// not a prefab leave this 0, which keeps the depth they have always been built with
        /// (1). 0 therefore means "not specified"; see
        /// <see cref="RoverPortColliderMaterials.Depth"/>.
        /// </summary>
        public float depth;
        public float radius;
        public int direction;
        public RoverPortVector2Data[] points;
        /// <summary>
        /// The PhysicMaterial the source collider actually carries, for catalogs whose source
        /// is a Unity prefab (RC1). Null for a catalog that stores friction as one number on
        /// the part instead - see <see cref="RoverPortColliderMaterials"/>.
        /// </summary>
        public RoverPortPhysicsMaterialData material;
    }

    /// <summary>
    /// A source PhysicMaterial's values, copied into the catalog by Tools/RC1/BuildRC1Catalog.ps1.
    ///
    /// The numbers travel instead of the asset guid on purpose: MAIN's imported Android prefabs
    /// point at BPLE's own Contraption_PhysMat / Contraption_WheelFriction_PhysMat *and* at two
    /// materials the importer generated only in MAIN
    /// (Assets/AndroidParts/Generated/AndroidSki*PhysicsMaterial), which this project does not
    /// have. A guid the runtime cannot resolve would silently fall back to Unity's default
    /// material, which is exactly the bug this replaces.
    /// </summary>
    [Serializable]
    public sealed class RoverPortPhysicsMaterialData
    {
        /// <summary>Source asset name, for the audit and the runtime object name. Empty means the source collider had no material ({fileID: 0}) and therefore used Unity's built-in Default Material.</summary>
        public string name;
        public float dynamic_friction;
        public float static_friction;
        public float bounciness;
        /// <summary>Unity's PhysicMaterialCombine: Average = 0, Minimum = 1, Multiply = 2, Maximum = 3.</summary>
        public int friction_combine;
        public int bounce_combine;
    }

    [Serializable]
    public sealed class RoverPortVector2Data
    {
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class RoverPortBodyData
    {
        public string node;
        public int source_id;
        public float mass;
        public float linear_drag;
        public float angular_drag;
        public float gravity_scale;
        public int body_type;
        public int constraints;
        public bool simulated;
    }

    [Serializable]
    public sealed class RoverPortJointData
    {
        public string node;
        public string type;
        public string connected_body;
        public bool enabled;
        public float[] anchor;
        public float[] connected_anchor;
        public bool auto_configure_connected_anchor;
        public bool enable_collision;
        public float angle;
        public bool use_limits;
        public float lower_limit;
        public float upper_limit;
        public float frequency;
        public float damping_ratio;
        public float distance;
        public bool auto_configure_distance;
        public bool max_distance_only;
        public float motor_speed;
        public float max_motor_force;
        public float max_motor_torque;
        public bool use_motor;
    }

    /// <summary>
    /// One piece of wheel artwork the *source* rotates, with the rolling radius and the
    /// rotation rule its own component uses. MAIN never rotates a wheel with physics alone:
    /// <c>MotorWheel</c>/<c>OffRoadWheel</c> spin a <c>WheelPivot</c> transform by the surface
    /// speed / circumference, <c>AndroidBigfootMotion</c> spins the wheel node it serialises by
    /// v.x / wheelRadius, and <c>AndroidTrackMotion</c> spins every roller it serialises by the
    /// belt speed / roller radius. A ported wheel that only writes <c>Rigidbody.angularVelocity</c>
    /// therefore cannot turn: its artwork is a plain child of the part. The catalog records which
    /// node each of those components rotates so the runtime can turn the same node the same way.
    /// </summary>
    [Serializable]
    public sealed class RoverPortWheelPivotData
    {
        /// <summary>Port node id of the transform the source component writes its rotation to.</summary>
        public string node;
        /// <summary>Rolling radius the source divides the speed by (MAIN's own serialized value).</summary>
        public float radius;
        /// <summary>
        /// Which of MAIN's three rotation rules to apply, named after the component that owns it:
        /// <c>surface</c> = <c>MotorWheel</c>/<c>OffRoadWheel</c>
        /// (<c>localRotation = AngleAxis(-partZ + angle, forward)</c>, angle from surface speed),
        /// <c>velocity_x</c> = <c>AndroidBigfootMotion</c>
        /// (<c>localRotation = Euler(0, 0, angle)</c>, angle from world v.x, plus the sprung
        /// <c>localPosition.y</c> that gives the bigfoot wheel its travel),
        /// <c>belt</c> = <c>AndroidTrackMotion</c>
        /// (<c>localRotation = initialLocalRotation * AngleAxis(angle, forward)</c>).
        /// </summary>
        public string mode;
        /// <summary>Component in MAIN that owns this rotation, for provenance.</summary>
        public string source;
        /// <summary>
        /// <c>AndroidBigfootMotion.travel</c>: half the sprung travel of the wheel the component
        /// carries, in metres (MAIN serialises 0.2601626 on both bigfoot wheels). Zero on every
        /// other pivot, i.e. on the pivots a plain <c>MotorWheel</c>/<c>OffRoadWheel</c> or
        /// <c>AndroidTrackMotion</c> owns, which do not move their node at all.
        /// </summary>
        public float travel;
        /// <summary>
        /// <c>AndroidBigfootMotion.springFrequency</c> in Hz (MAIN serialises 12): the component
        /// integrates <c>springVelocity += (target - offset) * frequency^2 * dt</c>.
        /// </summary>
        public float spring_frequency;
        /// <summary>
        /// <c>AndroidBigfootMotion.damping</c> (MAIN serialises 0.82): a per-60Hz-frame factor,
        /// applied as <c>pow(damping, dt * 60)</c>, not a damping ratio.
        /// </summary>
        public float damping;
    }

    [Serializable]
    public sealed class RoverPortMotionData
    {
        public float suspension_travel;
        /// <summary>APK axle artwork that follows suspension translation without tyre rotation.</summary>
        public string[] suspension_followers;
        public bool jet_control;
        public string kind;
        /// <summary>
        /// A wheel the source builds without a motor (its wheel component stores MaxWheelMotorTorque 0,
        /// see Tools/RCR2/PatchPassiveWheels.py): it is pushed along by the ground instead of driving the
        /// vehicle, like a plain wheel. Defaults to false, so a catalog without the field keeps driving.
        /// </summary>
        public bool passive;
        public float power;
        public float consumption;
        public float force;
        public float max_speed;
        public float reverse_max_speed;
        public float acceleration;
        public float deceleration;
        public float input_multiplier;
        public float torque;
        public float friction;
        public float capacity;
        public string[] wheel_bodies;
        /// <summary>
        /// Bodies that carry this wheel's ground contact even though they are
        /// not driven themselves. The mecanum wheel drives its hub, but the hub
        /// sits inside the eight spring mounted rollers, so its own collider
        /// never reaches the ground: the rollers are the tyre contact patches.
        /// </summary>
        public string[] contact_bodies;
        /// <summary>
        /// Off-road wheel suspension stiffness MAIN serialises (150 for the bigfoot
        /// wheels, 110 for the snowmobile track, 0 when the class has no spring).
        /// </summary>
        public float spring_stiffness;
        /// <summary>
        /// The source WheelJoint2D suspension used by ordinary Android motor/generator
        /// wheels. These values are separate from <see cref="spring_stiffness"/>:
        /// the latter belongs to OffRoadWheel.CustomConnectToPart, while these two
        /// values describe the wheel's soft connection to its bracket.
        /// </summary>
        public float connection_frequency;
        public float connection_damping_ratio;
        public float connection_angle;
        public RoverPortTrackRollerData[] track_rollers;
        /// <summary>MAIN's m_externalVisualMotion / m_externalPhysicsMotion on the off-road wheel.</summary>
        public bool external_visual_motion;
        public bool external_physics_motion;
        /// <summary>
        /// The wheel artwork the source's own component rotates (see
        /// <see cref="RoverPortWheelPivotData"/>). Empty for every part that is not a wheel.
        /// </summary>
        public RoverPortWheelPivotData[] wheel_pivots;
    }

    [Serializable]
    public sealed class RoverPortTrackRollerData
    {
        public string node;
        public float radius;
        public float mass;
        public float linear_drag;
        public float angular_drag;
        public float frequency;
        public float damping_ratio;
        public float[] mount;
        public bool powered;
    }

    /// <summary>
    /// One rebuilt Android particle system, exactly as MAIN's importer leaves it on the
    /// prefab (AndroidPartImporter.BuildThrusterFlame / .BuildExhaustSmoke). The values
    /// are read straight out of the prefab's ParticleSystem blocks; nothing is retyped
    /// by hand, so the port can rebuild the same system.
    /// </summary>
    [Serializable]
    public sealed class RoverPortParticleData
    {
        public string name;
        /// <summary>Node the system is parented to (the port's node id).</summary>
        public string node;
        public float[] local_position;
        public float[] local_rotation;
        public string texture_file;
        public string texture_guid;
        public string shader;
        public int sorting_order;
        public int sorting_layer_id;
        public float duration;
        public bool loop;
        public bool prewarm;
        public bool play_on_awake;
        /// <summary>0 Local, 1 World, 2 Custom - MAIN's moveWithTransform.</summary>
        public int simulation_space;
        public int scaling_mode;
        public int culling_mode;
        public int max_particles;
        public float gravity_modifier;
        public RoverPortMinMaxData start_lifetime;
        public RoverPortMinMaxData start_speed;
        public RoverPortMinMaxData start_size;
        public RoverPortMinMaxData start_rotation;
        public float[] start_color;
        public RoverPortParticleShapeData shape;
        public RoverPortParticleEmissionData emission;
        public RoverPortParticleSizeOverLifetimeData size_over_lifetime;
        public RoverPortParticleRotationOverLifetimeData rotation_over_lifetime;
        public RoverPortParticleColorOverLifetimeData color_over_lifetime;
        public RoverPortParticleVelocityData velocity_over_lifetime;
        public RoverPortParticleSheetData texture_sheet;
        public RoverPortParticleRendererData renderer;
        /// <summary>
        /// Which MAIN driver owns the system ("PS_Fire_Blue" on the thrusters,
        /// "FxSmoke" on the exhaust). The port maps this to its own component.
        /// </summary>
        public string driver;
        public bool enabled;
        /// <summary>
        /// The owning MAIN component's visual size multiplier for this system, or 1 when it has
        /// none. MAIN's <c>Exhaust</c> serialises <c>smokeSizeFactor = 1.8</c> and pushes it onto
        /// <c>ParticleSystem.MainModule.startSizeMultiplier</c> every frame
        /// (<c>Exhaust.ApplySmokeSize</c>), so the serialized <c>start_size</c> stays the Android
        /// value (0.8130) while the visible smoke is that much larger. Without it the port emitted
        /// the smoke at a little over half the size MAIN shows.
        /// </summary>
        public float size_multiplier;
    }

    /// <summary>
    /// A serialized MinMaxCurve. <c>min</c>/<c>max</c> are the resolved pair the
    /// runtime applies (state 0 Constant, 1 Curve, 2 RandomBetweenTwoCurves,
    /// 3 TwoConstants); the raw curve keys are kept for auditing against MAIN.
    /// </summary>
    [Serializable]
    public sealed class RoverPortMinMaxData
    {
        public int state;
        public float scalar;
        public float min_scalar;
        public float min;
        public float max;
        public RoverPortCurveKey[] curve_min;
        public RoverPortCurveKey[] curve_max;
    }

    /// <summary>
    /// One keyframe of a particle MinMaxCurve.
    ///
    /// The catalog used to carry these as <c>[time, value]</c> pairs, i.e. <c>float[][]</c>.
    /// Unity's JsonUtility cannot deserialize nested arrays: it leaves such a field null and
    /// reports nothing, so every curve-mode module (the exhaust's <c>frameOverTime</c> and
    /// plume curves included) silently collapsed to <c>scalar</c> - the smoke then sampled the
    /// sheet's empty last tile and never drew a pixel. An array of a [Serializable] struct is
    /// what JsonUtility does support, so the catalog now writes
    /// <c>{"time": t, "value": v}</c> objects.
    /// </summary>
    [Serializable]
    public struct RoverPortCurveKey
    {
        public float time;
        public float value;
        public bool has_tangents;
        public float in_tangent;
        public float out_tangent;
    }

    [Serializable]
    public sealed class RoverPortParticleShapeData
    {
        public bool enabled;
        public int type;
        public float angle;
        public float radius;
        public float arc;
        public float radius_thickness;
    }

    [Serializable]
    public sealed class RoverPortParticleEmissionData
    {
        public bool enabled;
        public RoverPortMinMaxData rate_over_time;
        public RoverPortMinMaxData rate_over_distance;
    }

    [Serializable]
    public sealed class RoverPortParticleSizeOverLifetimeData
    {
        public bool enabled;
        public RoverPortMinMaxData size;
    }

    [Serializable]
    public sealed class RoverPortParticleRotationOverLifetimeData
    {
        public bool enabled;
        public RoverPortMinMaxData z;
    }

    [Serializable]
    public sealed class RoverPortParticleColorOverLifetimeData
    {
        public bool enabled;
    }

    [Serializable]
    public sealed class RoverPortParticleVelocityData
    {
        public bool enabled;
        public int space;
        public RoverPortMinMaxData x;
        public RoverPortMinMaxData y;
        public RoverPortMinMaxData z;
        public RoverPortMinMaxData speed_modifier;
    }

    [Serializable]
    public sealed class RoverPortParticleSheetData
    {
        public bool enabled;
        public int mode;
        public int time_mode;
        public float fps;
        public int tiles_x;
        public int tiles_y;
        public int animation;
        public float cycles;
        public int row_index;
        public RoverPortMinMaxData frame_over_time;
        public RoverPortMinMaxData start_frame;
    }

    [Serializable]
    public sealed class RoverPortParticleRendererData
    {
        public int render_mode;
        public int sort_mode;
        public float min_particle_size;
        public float max_particle_size;
        public float length_scale;
        public float normal_direction;
        public int alignment;
        public float[] pivot;
        public float[] flip;
    }
}
