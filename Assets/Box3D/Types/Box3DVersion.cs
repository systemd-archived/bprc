using System.Runtime.InteropServices;

namespace Box3D
{
    /// <summary>Mirrors native b3Version (base.h). SemVer version of the loaded Box3D library.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Box3DVersion
    {
        public int Major;
        public int Minor;
        public int Revision;

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Revision}";
        }
    }
}
