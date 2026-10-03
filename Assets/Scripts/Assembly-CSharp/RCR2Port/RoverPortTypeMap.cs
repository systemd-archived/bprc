namespace RCR2Port
{
    // List families only. Runtime behaviour uses the source prefab adapter.
    public static class RoverPortTypeMap
    {
        public static BasePart.PartType Resolve(RoverPortPartData data)
        {
            switch (data.id)
            {
                case "part_cave_lamp": return BasePart.PartType.PointLight;
                case "part_cave_damper":
                case "part_suspension_air": return BasePart.PartType.Spring;
                case "part_cave_frame_jointconnector": return BasePart.PartType.MechanicalPart;
                case "part_acid_wheel_double":
                case "part_cave_wheel_sticky": return BasePart.PartType.StickyWheel;
                case "part_ice_chainsaw": return BasePart.PartType.MechanicalPart;
            }
            // The imported Android parts (RC1, index base 4000) carry the class MAIN names
            // on the prefab, and that name is the parity contract: an AndroidFrame is not a
            // MetalFrame, an AndroidBigfootWheel is not a MotorWheel, and each family carries
            // its own collider layout, suspension values and joint contract. Every other
            // catalog leaves android_class empty and keeps the category/motion resolution.
            if (!string.IsNullOrEmpty(data.android_class))
            {
                BasePart.PartType mapped = ResolveAndroidClass(data.android_class);
                if (mapped != BasePart.PartType.Unknown)
                {
                    return mapped;
                }
            }
            switch (data.category)
            {
                case "package": return BasePart.PartType.RCR2Cargo;
                // The RC3 missile launcher (part_rc3_missile) is its own one-shot
                // weapon family so the trigger button and "last used variant"
                // bookkeeping never mix with native Rocket variants.
                case "weapon": return BasePart.PartType.RC3Missile;
                case "frame": return BasePart.PartType.MetalFrame;
                // The cockpit is the frame of the cabin: it carries a part inside it and holds its
                // frame connections at 70% of a metal frame's - see RoverPortCockpitPart.
                case "cockpit": return BasePart.PartType.MetalFrame;
                // A wheel the source builds without a motor rolls with the vehicle instead of driving it
                // (see Tools/RCR2/PatchPassiveWheels.py), so it belongs to the plain wheel family.
                case "wheel": return data.motion != null && data.motion.passive
                    ? BasePart.PartType.NormalWheel
                    : BasePart.PartType.MotorWheel;
                case "battery": return BasePart.PartType.ElectricalPart;
                case "turbine": return data.motion.kind == "propeller" ? BasePart.PartType.Propeller : BasePart.PartType.JetEngine;
                case "gyroscope": return BasePart.PartType.MechanicalPart;
                case "engine": return data.motion.kind == "engine" ? BasePart.PartType.JetEngine : BasePart.PartType.MechanicalPart;
                default: return BasePart.PartType.MechanicalPart;
            }
        }

        /// <summary>
        /// The thirteen classes of MAIN's Android import (see
        /// Tools/RC1/BuildRC1Catalog.ps1 and MAIN's AndroidPartImporter.AndroidPartClass
        /// table). The names are the ones MAIN writes on the prefab, so the two projects
        /// can be compared by name instead of by enum number: BPRC already owns 51 and 52,
        /// so the imported classes are appended above them in both PartType and
        /// SortedPartType and must never reuse MAIN's numbering.
        /// </summary>
        public static BasePart.PartType ResolveAndroidClass(string androidClass)
        {
            switch (androidClass)
            {
                case "Exhaust": return BasePart.PartType.Exhaust;
                case "AndroidFrame": return BasePart.PartType.AndroidFrame;
                case "AndroidTriFrame": return BasePart.PartType.AndroidTriFrame;
                case "Accumulator": return BasePart.PartType.Accumulator;
                case "Gyro": return BasePart.PartType.Gyro;
                case "AndroidThruster": return BasePart.PartType.AndroidThruster;
                case "Bumper": return BasePart.PartType.Bumper;
                case "AndroidMotorWheel": return BasePart.PartType.AndroidMotorWheel;
                case "AndroidGenerator": return BasePart.PartType.AndroidGenerator;
                case "AndroidBigfootWheel": return BasePart.PartType.AndroidBigfootWheel;
                case "AndroidTrack": return BasePart.PartType.AndroidTrack;
                case "AndroidSki": return BasePart.PartType.AndroidSki;
                case "Spoiler": return BasePart.PartType.Spoiler;
                default: return BasePart.PartType.Unknown;
            }
        }
    }
}
