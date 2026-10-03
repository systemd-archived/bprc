namespace Box3D
{
    /// <summary>Global, world-independent Box3D entry points.</summary>
    public static class Box3DApi
    {
        /// <summary>Version of the loaded native library. First call proves the DLL resolves.</summary>
        public static Box3DVersion GetVersion()
        {
            return UnsafeBindings.b3GetVersion();
        }

        /// <summary>True if the native library was built with BOX3D_DOUBLE_PRECISION. This wrapper
        /// requires a single-precision build; managed struct layouts assume it.</summary>
        public static bool IsDoublePrecision => UnsafeBindings.b3IsDoublePrecision();
    }
}
