using UnityEngine;

namespace BadPiggies.Multiplayer.Online.Qr
{
    /// <summary>
    /// Renders <see cref="QrEncoder"/> matrices into <see cref="Texture2D"/> objects for the
    /// invite UI. This is the only type in the QR folder that touches UnityEngine.
    /// </summary>
    public static class QrTexture
    {
        /// <summary>Default dark module colour, a near black that matches the game UI.</summary>
        public static readonly Color32 DefaultDark = new Color32(20, 24, 33, 255);

        /// <summary>Default light module colour.</summary>
        public static readonly Color32 DefaultLight = new Color32(255, 255, 255, 255);

        private const int MaxTextureDimension = 16384;

        /// <summary>
        /// Encodes <paramref name="text"/> at error correction level M and renders it into a new
        /// RGBA32 <see cref="Texture2D"/>.
        /// </summary>
        /// <remarks>
        /// The parameter list matches the documented order
        /// <c>(text, pixelsPerModule, quietZone, dark, light, out error)</c>. C# forbids optional
        /// parameters before a required <c>out</c> parameter (CS1737), so the defaults are supplied
        /// by the overloads below instead of by parameter initialisers.
        /// </remarks>
        /// <returns>The texture, or <c>null</c> on failure with <paramref name="error"/> set.</returns>
        public static Texture2D Create(string text, int pixelsPerModule, int quietZone, Color32? dark, Color32? light, out string error)
        {
            error = null;
            bool[,] modules = QrEncoder.Encode(text, out error);
            if (modules == null)
            {
                return null;
            }

            Color32 darkColor = dark.HasValue ? dark.Value : DefaultDark;
            Color32 lightColor = light.HasValue ? light.Value : DefaultLight;
            return CreateFromMatrix(modules, pixelsPerModule, quietZone, darkColor, lightColor);
        }

        /// <summary>Renders <paramref name="text"/> with the default 8 px module size and 4 module quiet zone.</summary>
        public static Texture2D Create(string text, out string error)
        {
            return Create(text, 8, 4, null, null, out error);
        }

        /// <summary>Renders <paramref name="text"/> with a custom module size and the default 4 module quiet zone.</summary>
        public static Texture2D Create(string text, int pixelsPerModule, out string error)
        {
            return Create(text, pixelsPerModule, 4, null, null, out error);
        }

        /// <summary>Renders <paramref name="text"/> with a custom module size and quiet zone.</summary>
        public static Texture2D Create(string text, int pixelsPerModule, int quietZone, out string error)
        {
            return Create(text, pixelsPerModule, quietZone, null, null, out error);
        }

        /// <summary>Renders <paramref name="text"/> with custom colours and the default module size and quiet zone.</summary>
        public static Texture2D Create(string text, Color32? dark, Color32? light, out string error)
        {
            return Create(text, 8, 4, dark, light, out error);
        }

        /// <summary>
        /// Renders an already encoded module matrix. <paramref name="modules"/> uses the
        /// <c>bool[row, col]</c> convention documented on <see cref="QrEncoder"/>: row 0 is the top
        /// row, <c>true</c> is a dark module, and the matrix contains no quiet zone.
        /// </summary>
        /// <returns>The texture, or <c>null</c> if the arguments are not renderable.</returns>
        public static Texture2D CreateFromMatrix(bool[,] modules, int pixelsPerModule, int quietZone, Color32 dark, Color32 light)
        {
            if (modules == null || modules.GetLength(0) == 0 || modules.GetLength(1) == 0)
            {
                return null;
            }
            if (pixelsPerModule < 1)
            {
                return null;
            }
            if (quietZone < 0)
            {
                return null;
            }

            int moduleRows = modules.GetLength(0);
            int moduleCols = modules.GetLength(1);

            long width = ((long)moduleCols + 2L * quietZone) * pixelsPerModule;
            long height = ((long)moduleRows + 2L * quietZone) * pixelsPerModule;
            if (width < 1 || height < 1 || width > MaxTextureDimension || height > MaxTextureDimension)
            {
                return null;
            }

            int pixelWidth = (int)width;
            int pixelHeight = (int)height;
            Color32[] pixels = new Color32[pixelWidth * pixelHeight];

            // Texture row 0 is the bottom row, the matrix row 0 is the top row, so the vertical
            // axis is flipped while filling.
            for (int py = 0; py < pixelHeight; py++)
            {
                int moduleRow = (pixelHeight - 1 - py) / pixelsPerModule - quietZone;
                bool rowInSymbol = moduleRow >= 0 && moduleRow < moduleRows;
                int rowBase = py * pixelWidth;

                for (int px = 0; px < pixelWidth; px++)
                {
                    int moduleCol = px / pixelsPerModule - quietZone;
                    bool inSymbol = rowInSymbol && moduleCol >= 0 && moduleCol < moduleCols;
                    bool isDark = inSymbol && modules[moduleRow, moduleCol];
                    pixels[rowBase + px] = isDark ? dark : light;
                }
            }

            Texture2D texture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.RGBA32, false);
            texture.name = "QrCode";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
