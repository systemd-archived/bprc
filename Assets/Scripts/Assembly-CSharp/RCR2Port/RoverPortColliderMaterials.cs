using System.Collections.Generic;
using UnityEngine;

namespace RCR2Port
{
    /// <summary>
    /// Builds the PhysicMaterial of one ported collider.
    ///
    /// The catalog is the only place a ported part's grip can come from, because MAIN keeps
    /// friction on the collider and not on the BasePart, so there is no part field to read it
    /// from. Two kinds of catalog express it:
    ///
    /// <list type="bullet">
    /// <item><description>
    /// The source is a Unity prefab, so every collider points at a real PhysicMaterial asset.
    /// That is RC1, and the ground truth is MAIN's imported Android prefabs, which agree
    /// exactly with this project's own native <c>Part_*.prefab</c> files:
    /// <c>Contraption_PhysMat</c> (dynamic 0.7, static 0.5, Average) on every frame, engine,
    /// thruster, exhaust, bumper and spoiler collider *and* on a wheel's SupportCollider;
    /// <c>Contraption_WheelFriction_PhysMat</c> (dynamic 0.025, static 0, Multiply) on the
    /// tyre - the same material <c>Part_MotorWheel_*</c>, <c>Part_NormalWheel_*</c>,
    /// <c>Part_SmallWheel_*</c> and <c>Part_StickyWheel_*</c> use; the two
    /// <c>AndroidSki*PhysicsMaterial</c> assets on the snowmobile ski; and no material at all
    /// (Unity's Default Material, 0.6 / 0.6 / Average) on the bigfoot wheels and the track.
    /// <c>Tools/RC1/BuildRC1Catalog.ps1</c> copies those numbers into
    /// <see cref="RoverPortColliderData.material"/>.
    /// </description></item>
    /// <item><description>
    /// The source is not a prefab and stores friction as a single number on the <em>part</em>
    /// instead. Those catalogs leave <c>material</c> null and keep the part-level value, which
    /// <see cref="Resolve"/> returns as the fallback. Nothing about their behaviour changes
    /// here.
    /// </description></item>
    /// </list>
    ///
    /// Before this, every RC1 collider shared one invented part-level material whose input
    /// (<c>RoverPortPartData.friction</c>) the RC1 generator always wrote as 0, because MAIN
    /// keeps friction on the collider and not on the part. The result was that all 20
    /// structural RC1 parts ran with <c>dynamicFriction = staticFriction = 0</c> and slid over
    /// the ground like ice, while all 12 wheels were floored at
    /// <c>RoverPortPoweredPart.WheelMinimumFriction</c> = 0.7 - 28x the grip BPLE and MAIN both
    /// give a tyre.
    /// </summary>
    internal static class RoverPortColliderMaterials
    {
        // Unity's PhysicMaterialCombine: Average = 0, Minimum = 1, Multiply = 2, Maximum = 3.
        private const int CombineLow = 0;
        private const int CombineHigh = 3;

        /// <summary>
        /// PhysicMaterial for one collider: the catalog's own record when it carries one,
        /// otherwise <paramref name="fallback"/> (the part-level material of a catalog whose
        /// source is not a prefab).
        ///
        /// Materials are cached per part so a part whose colliders all share one source
        /// material builds one object rather than one per collider.
        /// </summary>
        internal static PhysicMaterial Resolve(RoverPortColliderData collider, RoverPortPartData data, PhysicMaterial fallback, Dictionary<string, PhysicMaterial> cache)
        {
            RoverPortPhysicsMaterialData record = collider == null ? null : collider.material;
            if (record == null)
            {
                return fallback;
            }
            string key = string.IsNullOrEmpty(record.name) ? "<source default>" : record.name;
            if (cache != null)
            {
                PhysicMaterial cached;
                if (cache.TryGetValue(key, out cached) && cached != null)
                {
                    return cached;
                }
            }
            string owner = data == null || string.IsNullOrEmpty(data.id) ? "RoverPort" : data.id;
            PhysicMaterial material = new PhysicMaterial(owner + "_" + key);
            material.dynamicFriction = Mathf.Clamp01(record.dynamic_friction);
            material.staticFriction = Mathf.Clamp01(record.static_friction);
            material.bounciness = Mathf.Clamp01(record.bounciness);
            material.frictionCombine = (PhysicMaterialCombine)Mathf.Clamp(record.friction_combine, CombineLow, CombineHigh);
            material.bounceCombine = (PhysicMaterialCombine)Mathf.Clamp(record.bounce_combine, CombineLow, CombineHigh);
            if (cache != null)
            {
                cache[key] = material;
            }
            return material;
        }

        /// <summary>
        /// BoxCollider depth for one collider: the source value when the catalog carries one,
        /// otherwise the historical 1 that catalogs without a 3D source have always produced.
        /// </summary>
        internal static float Depth(RoverPortColliderData collider)
        {
            return collider == null || collider.depth <= 0f ? 1f : collider.depth;
        }
    }
}
