using System;
using System.Text;

namespace BadPiggies.Multiplayer.Online.Qr
{
    /// <summary>
    /// Error correction level of a QR Code symbol, from the least to the most redundant.
    /// </summary>
    public enum QrEcc
    {
        L = 0,
        M = 1,
        Q = 2,
        H = 3
    }

    /// <summary>
    /// Dependency free QR Code encoder (byte mode, ISO/IEC 18004).
    /// <para>
    /// Supports versions 1..10 and all four error correction levels. The class deliberately
    /// references no UnityEngine type so it can be compiled and exercised standalone.
    /// </para>
    /// <para>
    /// Matrix convention: <c>bool[row, col]</c>, row 0 is the top row, col 0 is the left column,
    /// <c>true</c> means a dark module. The returned matrix is the bare symbol and contains no
    /// quiet zone.
    /// </para>
    /// </summary>
    public static class QrEncoder
    {
        #region Constants

        /// <summary>Lowest QR version this encoder can produce.</summary>
        public const int MinSupportedVersion = 1;

        /// <summary>Highest QR version this encoder can produce.</summary>
        public const int MaxSupportedVersion = 10;

        private const int PenaltyN1 = 3;
        private const int PenaltyN2 = 3;
        private const int PenaltyN3 = 40;
        private const int PenaltyN4 = 10;

        /// <summary>Total codewords (data + error correction) indexed by version. Index 0 unused.</summary>
        private static readonly int[] TotalCodewordsByVersion =
        {
            0, 26, 44, 70, 100, 134, 172, 196, 242, 292, 346
        };

        /// <summary>
        /// Alignment pattern centre coordinates indexed by version. Index 0 and 1 are unused
        /// (version 1 has no alignment pattern).
        /// </summary>
        private static readonly int[][] AlignmentPatternCenters =
        {
            new int[] { },
            new int[] { },
            new int[] { 6, 18 },
            new int[] { 6, 22 },
            new int[] { 6, 26 },
            new int[] { 6, 30 },
            new int[] { 6, 34 },
            new int[] { 6, 22, 38 },
            new int[] { 6, 24, 42 },
            new int[] { 6, 26, 46 },
            new int[] { 6, 28, 50 }
        };

        /// <summary>
        /// Error correction block layout, indexed by <c>(version - 1) * 4 + (int)ecc</c>.
        /// Each entry is <c>{ eccCodewordsPerBlock, group1Blocks, group1DataCodewords, group2Blocks, group2DataCodewords }</c>.
        /// </summary>
        private static readonly int[][] BlockTable =
        {
            // ---- version 1 ----
            new int[] { 7, 1, 19, 0, 0 },   // L
            new int[] { 10, 1, 16, 0, 0 },  // M
            new int[] { 13, 1, 13, 0, 0 },  // Q
            new int[] { 17, 1, 9, 0, 0 },   // H
            // ---- version 2 ----
            new int[] { 10, 1, 34, 0, 0 },  // L
            new int[] { 16, 1, 28, 0, 0 },  // M
            new int[] { 22, 1, 22, 0, 0 },  // Q
            new int[] { 28, 1, 16, 0, 0 },  // H
            // ---- version 3 ----
            new int[] { 15, 1, 55, 0, 0 },  // L
            new int[] { 26, 1, 44, 0, 0 },  // M
            new int[] { 18, 2, 17, 0, 0 },  // Q
            new int[] { 22, 2, 13, 0, 0 },  // H
            // ---- version 4 ----
            new int[] { 20, 1, 80, 0, 0 },  // L
            new int[] { 18, 2, 32, 0, 0 },  // M
            new int[] { 26, 2, 24, 0, 0 },  // Q
            new int[] { 16, 4, 9, 0, 0 },   // H
            // ---- version 5 ----
            new int[] { 26, 1, 108, 0, 0 }, // L
            new int[] { 24, 2, 43, 0, 0 },  // M
            new int[] { 18, 2, 15, 2, 16 }, // Q
            new int[] { 22, 2, 11, 2, 12 }, // H
            // ---- version 6 ----
            new int[] { 18, 2, 68, 0, 0 },  // L
            new int[] { 16, 4, 27, 0, 0 },  // M
            new int[] { 24, 4, 19, 0, 0 },  // Q
            new int[] { 28, 4, 15, 0, 0 },  // H
            // ---- version 7 ----
            new int[] { 20, 2, 78, 0, 0 },  // L
            new int[] { 18, 4, 31, 0, 0 },  // M
            new int[] { 18, 2, 14, 4, 15 }, // Q
            new int[] { 26, 4, 13, 1, 14 }, // H
            // ---- version 8 ----
            new int[] { 24, 2, 97, 0, 0 },  // L
            new int[] { 22, 2, 38, 2, 39 }, // M
            new int[] { 22, 4, 18, 2, 19 }, // Q
            new int[] { 26, 4, 14, 2, 15 }, // H
            // ---- version 9 ----
            new int[] { 30, 2, 116, 0, 0 }, // L
            new int[] { 22, 3, 36, 2, 37 }, // M
            new int[] { 20, 4, 16, 4, 17 }, // Q
            new int[] { 24, 4, 12, 4, 13 }, // H
            // ---- version 10 ----
            new int[] { 18, 2, 68, 2, 69 }, // L
            new int[] { 26, 4, 43, 1, 44 }, // M
            new int[] { 24, 6, 19, 2, 20 }, // Q
            new int[] { 28, 6, 15, 2, 16 }  // H
        };

        /// <summary>Two bit error correction level indicator used inside the format information. L=01, M=00, Q=11, H=10.</summary>
        private static readonly int[] EccFormatBits = { 1, 0, 3, 2 };

        /// <summary>Cache of Reed-Solomon generator polynomials, indexed by the error correction codeword count.</summary>
        private static readonly byte[][] RsDivisorCache = new byte[31][];

        #endregion

        #region Public API

        /// <summary>
        /// Encodes <paramref name="text"/> as a QR Code at error correction level M, choosing the
        /// smallest version in 1..10 and the best data mask automatically.
        /// </summary>
        /// <returns>The bare symbol matrix, or <c>null</c> on failure with <paramref name="error"/> set.</returns>
        public static bool[,] Encode(string text, out string error)
        {
            int version;
            int mask;
            return Encode(text, QrEcc.M, MinSupportedVersion, MaxSupportedVersion, -1, out version, out mask, out error);
        }

        /// <summary>
        /// Encodes <paramref name="text"/> as a QR Code at error correction level M, choosing the
        /// smallest version in 1..10 and the best data mask automatically.
        /// </summary>
        /// <returns>The bare symbol matrix, or <c>null</c> on failure with <paramref name="error"/> set.</returns>
        public static bool[,] Encode(string text, out int version, out int mask, out string error)
        {
            return Encode(text, QrEcc.M, MinSupportedVersion, MaxSupportedVersion, -1, out version, out mask, out error);
        }

        /// <summary>
        /// Encodes <paramref name="text"/> as a QR Code.
        /// </summary>
        /// <param name="text">Payload. Encoded as UTF-8 bytes in byte mode.</param>
        /// <param name="ecc">Error correction level.</param>
        /// <param name="minVersion">Lowest version to consider (clamped to 1..10).</param>
        /// <param name="maxVersion">Highest version to consider (clamped to 1..10).</param>
        /// <param name="forcedMask">
        /// <c>-1</c> to pick the best mask with the standard penalty rules, otherwise 0..7 to force a mask.
        /// </param>
        /// <param name="version">Receives the version actually used, or 0 on failure.</param>
        /// <param name="mask">Receives the mask actually used, or -1 on failure.</param>
        /// <param name="error">Receives <c>null</c> on success, otherwise a human readable message.</param>
        /// <returns>The bare symbol matrix (no quiet zone), or <c>null</c> on failure.</returns>
        public static bool[,] Encode(string text, QrEcc ecc, int minVersion, int maxVersion, int forcedMask, out int version, out int mask, out string error)
        {
            version = 0;
            mask = -1;
            error = null;

            if (text == null)
            {
                error = "QR 编码失败：输入文本为 null。";
                return null;
            }
            if (text.Length == 0)
            {
                error = "QR 编码失败：输入文本为空字符串。";
                return null;
            }

            int eccIndex = (int)ecc;
            if (eccIndex < 0 || eccIndex > 3)
            {
                error = "QR 编码失败：无效的纠错等级 " + eccIndex + "。";
                return null;
            }

            if (minVersion < MinSupportedVersion)
            {
                minVersion = MinSupportedVersion;
            }
            if (maxVersion > MaxSupportedVersion)
            {
                maxVersion = MaxSupportedVersion;
            }
            if (minVersion > maxVersion)
            {
                error = "QR 编码失败：版本范围无效 (minVersion=" + minVersion + ", maxVersion=" + maxVersion + ")。";
                return null;
            }

            if (forcedMask < -1 || forcedMask > 7)
            {
                error = "QR 编码失败：forcedMask 必须为 -1 或 0..7，实际为 " + forcedMask + "。";
                return null;
            }

            byte[] payload = Encoding.UTF8.GetBytes(text);

            // ---- pick the smallest version that fits ----
            int chosenVersion = -1;
            int dataCodewords = 0;
            for (int v = minVersion; v <= maxVersion; v++)
            {
                int dc = GetDataCodewordCount(v, eccIndex);
                if (FitsInVersion(payload.Length, v, dc))
                {
                    chosenVersion = v;
                    dataCodewords = dc;
                    break;
                }
            }

            if (chosenVersion < 0)
            {
                int maxDc = GetDataCodewordCount(maxVersion, eccIndex);
                int capacity = GetByteCapacity(maxVersion, maxDc);
                error = "QR 编码失败：数据过长，" + payload.Length + " 字节超出最大版本 " + maxVersion +
                        " / 纠错等级 " + ecc + " 的容量（该配置最多 " + capacity + " 字节）。";
                return null;
            }

            int[] codewords = BuildCodewords(payload, chosenVersion, dataCodewords, eccIndex);
            int chosenMask;
            bool[,] modules = BuildSymbol(codewords, chosenVersion, eccIndex, forcedMask, out chosenMask);

            version = chosenVersion;
            mask = chosenMask;
            return modules;
        }

        #endregion

        #region Capacity helpers

        /// <summary>Number of data codewords available for a version / error correction level pair.</summary>
        private static int GetDataCodewordCount(int version, int eccIndex)
        {
            int[] spec = BlockTable[(version - 1) * 4 + eccIndex];
            return spec[1] * spec[2] + spec[3] * spec[4];
        }

        /// <summary>Number of character count indicator bits used by byte mode for a version.</summary>
        private static int GetCharCountBits(int version)
        {
            return version <= 9 ? 8 : 16;
        }

        /// <summary>Largest number of payload bytes that fits in a version / data codeword budget.</summary>
        private static int GetByteCapacity(int version, int dataCodewords)
        {
            int availableBits = dataCodewords * 8 - 4 - GetCharCountBits(version);
            return availableBits / 8;
        }

        private static bool FitsInVersion(int byteCount, int version, int dataCodewords)
        {
            long neededBits = 4L + GetCharCountBits(version) + (long)byteCount * 8L;
            return neededBits <= (long)dataCodewords * 8L;
        }

        #endregion

        #region Codeword construction

        /// <summary>
        /// Builds the final interleaved codeword sequence (data blocks followed by interleaved
        /// error correction blocks) for the given payload and version.
        /// </summary>
        private static int[] BuildCodewords(byte[] payload, int version, int dataCodewords, int eccIndex)
        {
            int[] spec = BlockTable[(version - 1) * 4 + eccIndex];
            int eccPerBlock = spec[0];
            int group1Blocks = spec[1];
            int group1Data = spec[2];
            int group2Blocks = spec[3];
            int group2Data = spec[4];
            int totalBlocks = group1Blocks + group2Blocks;

            // ---- data bit stream: mode 0100, char count, payload, terminator, padding ----
            byte[] dataBytes = new byte[dataCodewords];
            int capacityBits = dataCodewords * 8;
            int bitPos = 0;

            bitPos = AppendBits(dataBytes, bitPos, 0x4, 4);
            bitPos = AppendBits(dataBytes, bitPos, payload.Length, GetCharCountBits(version));
            for (int i = 0; i < payload.Length; i++)
            {
                bitPos = AppendBits(dataBytes, bitPos, payload[i], 8);
            }

            int terminator = Math.Min(4, capacityBits - bitPos);
            bitPos += terminator; // terminator bits are zero, the buffer already is

            // pad to the next codeword boundary (again, zero bits)
            bitPos = ((bitPos + 7) / 8) * 8;

            // alternating pad codewords 0xEC / 0x11
            byte padByte = 0xEC;
            while (bitPos < capacityBits)
            {
                bitPos = AppendBits(dataBytes, bitPos, padByte, 8);
                padByte = (byte)(padByte ^ 0xEC ^ 0x11);
            }

            // ---- split into blocks and compute error correction ----
            byte[][] dataBlocks = new byte[totalBlocks][];
            byte[][] eccBlocks = new byte[totalBlocks][];
            byte[] divisor = GetRsDivisor(eccPerBlock);

            int offset = 0;
            for (int b = 0; b < totalBlocks; b++)
            {
                int blockLength = b < group1Blocks ? group1Data : group2Data;
                byte[] block = new byte[blockLength];
                Array.Copy(dataBytes, offset, block, 0, blockLength);
                offset += blockLength;
                dataBlocks[b] = block;
                eccBlocks[b] = ComputeRsRemainder(block, divisor);
            }

            // ---- interleave ----
            int totalCodewords = TotalCodewordsByVersion[version];
            int[] result = new int[totalCodewords];
            int index = 0;

            int longestDataBlock = Math.Max(group1Data, group2Data);
            for (int i = 0; i < longestDataBlock; i++)
            {
                for (int b = 0; b < totalBlocks; b++)
                {
                    if (i < dataBlocks[b].Length)
                    {
                        result[index++] = dataBlocks[b][i];
                    }
                }
            }
            for (int i = 0; i < eccPerBlock; i++)
            {
                for (int b = 0; b < totalBlocks; b++)
                {
                    result[index++] = eccBlocks[b][i];
                }
            }

            if (index != totalCodewords)
            {
                throw new InvalidOperationException("QR 内部错误：交织后码字数量 " + index + " 与版本 " + version + " 期望的 " + totalCodewords + " 不符。");
            }

            return result;
        }

        /// <summary>Writes <paramref name="length"/> bits of <paramref name="value"/> (MSB first) and returns the new bit position.</summary>
        private static int AppendBits(byte[] destination, int bitPosition, int value, int length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                if (((value >> i) & 1) != 0)
                {
                    destination[bitPosition >> 3] |= (byte)(1 << (7 - (bitPosition & 7)));
                }
                bitPosition++;
            }
            return bitPosition;
        }

        #endregion

        #region Reed-Solomon over GF(256) / 0x11D

        private static byte[] GetRsDivisor(int degree)
        {
            if (degree < 1 || degree >= RsDivisorCache.Length)
            {
                throw new InvalidOperationException("QR 内部错误：无效的 RS 生成多项式次数 " + degree + "。");
            }

            byte[] cached = RsDivisorCache[degree];
            if (cached != null)
            {
                return cached;
            }

            // Product of (x - r^0)(x - r^1)...(x - r^(degree-1)), highest term dropped (always 1).
            byte[] result = new byte[degree];
            result[degree - 1] = 1;

            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < result.Length; j++)
                {
                    result[j] = (byte)GfMultiply(result[j] & 0xFF, root);
                    if (j + 1 < result.Length)
                    {
                        result[j] ^= result[j + 1];
                    }
                }
                root = GfMultiply(root, 0x02);
            }

            RsDivisorCache[degree] = result;
            return result;
        }

        private static byte[] ComputeRsRemainder(byte[] data, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            for (int d = 0; d < data.Length; d++)
            {
                int factor = (data[d] ^ result[0]) & 0xFF;

                for (int i = 0; i < result.Length - 1; i++)
                {
                    result[i] = result[i + 1];
                }
                result[result.Length - 1] = 0;

                for (int i = 0; i < result.Length; i++)
                {
                    result[i] ^= (byte)GfMultiply(divisor[i] & 0xFF, factor);
                }
            }
            return result;
        }

        /// <summary>Multiplication in GF(2^8) with the primitive polynomial x^8 + x^4 + x^3 + x^2 + 1 (0x11D).</summary>
        private static int GfMultiply(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ (((z >> 7) & 1) * 0x11D);
                z ^= (((y >> i) & 1) * x);
            }
            return z & 0xFF;
        }

        #endregion

        #region Symbol construction

        private static bool[,] BuildSymbol(int[] codewords, int version, int eccIndex, int forcedMask, out int chosenMask)
        {
            int size = 17 + 4 * version;
            bool[,] modules = new bool[size, size];
            bool[,] isFunction = new bool[size, size];

            DrawFunctionPatterns(modules, isFunction, version);

            // Reserve the format information area (with a provisional mask 0 value) so that the
            // codeword placement below skips it. It is rewritten for every mask candidate.
            DrawFormatBits(modules, isFunction, 0, eccIndex);

            DrawCodewords(modules, isFunction, codewords);

            if (forcedMask >= 0)
            {
                ApplyMask(modules, isFunction, forcedMask);
                DrawFormatBits(modules, isFunction, forcedMask, eccIndex);
                chosenMask = forcedMask;
                return modules;
            }

            bool[,] best = null;
            int bestPenalty = int.MaxValue;
            chosenMask = 0;

            for (int candidate = 0; candidate < 8; candidate++)
            {
                bool[,] trial = (bool[,])modules.Clone();
                ApplyMask(trial, isFunction, candidate);
                DrawFormatBits(trial, isFunction, candidate, eccIndex);

                int penalty = ComputePenalty(trial);
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    best = trial;
                    chosenMask = candidate;
                }
            }

            return best;
        }

        private static void SetFunction(bool[,] modules, bool[,] isFunction, int row, int col, bool dark)
        {
            modules[row, col] = dark;
            isFunction[row, col] = true;
        }

        /// <summary>Draws finder patterns, separators, timing patterns, alignment patterns and version information.</summary>
        private static void DrawFunctionPatterns(bool[,] modules, bool[,] isFunction, int version)
        {
            int size = modules.GetLength(0);

            // Timing patterns (finders and alignment patterns overwrite the relevant parts).
            for (int i = 0; i < size; i++)
            {
                bool dark = (i % 2) == 0;
                SetFunction(modules, isFunction, i, 6, dark);
                SetFunction(modules, isFunction, 6, i, dark);
            }

            // Finder patterns plus their separators, drawn from the centre of each 7x7 block.
            DrawFinderPattern(modules, isFunction, 3, 3);
            DrawFinderPattern(modules, isFunction, 3, size - 4);
            DrawFinderPattern(modules, isFunction, size - 4, 3);

            // Alignment patterns. Version 1 has none; the three finder corners are excluded.
            int[] centers = AlignmentPatternCenters[version];
            int count = centers.Length;
            for (int ri = 0; ri < count; ri++)
            {
                for (int ci = 0; ci < count; ci++)
                {
                    bool overlapsFinder =
                        (ri == 0 && ci == 0) ||
                        (ri == 0 && ci == count - 1) ||
                        (ri == count - 1 && ci == 0);
                    if (!overlapsFinder)
                    {
                        DrawAlignmentPattern(modules, isFunction, centers[ri], centers[ci]);
                    }
                }
            }

            // Version information (BCH 18,6 with generator 0x1F25), versions 7 and above only.
            if (version >= 7)
            {
                int remainder = version;
                for (int i = 0; i < 12; i++)
                {
                    remainder = (remainder << 1) ^ (((remainder >> 11) & 1) * 0x1F25);
                }
                int bits = (version << 12) | remainder;

                for (int i = 0; i < 18; i++)
                {
                    bool bit = GetBit(bits, i);
                    int a = size - 11 + (i % 3);
                    int b = i / 3;
                    SetFunction(modules, isFunction, b, a, bit);
                    SetFunction(modules, isFunction, a, b, bit);
                }
            }
        }

        /// <summary>Draws a 7x7 finder pattern and its 1 module separator, centred on (row, col).</summary>
        private static void DrawFinderPattern(bool[,] modules, bool[,] isFunction, int row, int col)
        {
            int size = modules.GetLength(0);
            for (int dr = -4; dr <= 4; dr++)
            {
                for (int dc = -4; dc <= 4; dc++)
                {
                    int r = row + dr;
                    int c = col + dc;
                    if (r < 0 || r >= size || c < 0 || c >= size)
                    {
                        continue;
                    }
                    int distance = Math.Max(Math.Abs(dr), Math.Abs(dc));
                    // distance 4 = separator (light), 3 = outer ring (dark), 2 = inner ring (light),
                    // 1 and 0 = 3x3 centre (dark).
                    SetFunction(modules, isFunction, r, c, distance != 2 && distance != 4);
                }
            }
        }

        /// <summary>Draws a 5x5 alignment pattern centred on (row, col).</summary>
        private static void DrawAlignmentPattern(bool[,] modules, bool[,] isFunction, int row, int col)
        {
            for (int dr = -2; dr <= 2; dr++)
            {
                for (int dc = -2; dc <= 2; dc++)
                {
                    SetFunction(modules, isFunction, row + dr, col + dc, Math.Max(Math.Abs(dr), Math.Abs(dc)) != 1);
                }
            }
        }

        /// <summary>Draws the two copies of the format information (BCH 15,5 with mask 0x5412) and the dark module.</summary>
        private static void DrawFormatBits(bool[,] modules, bool[,] isFunction, int mask, int eccIndex)
        {
            int size = modules.GetLength(0);

            int data = (EccFormatBits[eccIndex] << 3) | mask;
            int remainder = data;
            for (int i = 0; i < 10; i++)
            {
                remainder = (remainder << 1) ^ (((remainder >> 9) & 1) * 0x537);
            }
            int bits = ((data << 10) | remainder) ^ 0x5412;

            // First copy, around the top left finder pattern.
            for (int i = 0; i <= 5; i++)
            {
                SetFunction(modules, isFunction, i, 8, GetBit(bits, i));
            }
            SetFunction(modules, isFunction, 7, 8, GetBit(bits, 6));
            SetFunction(modules, isFunction, 8, 8, GetBit(bits, 7));
            SetFunction(modules, isFunction, 8, 7, GetBit(bits, 8));
            for (int i = 9; i < 15; i++)
            {
                SetFunction(modules, isFunction, 8, 14 - i, GetBit(bits, i));
            }

            // Second copy, split between the top right and bottom left finder patterns.
            for (int i = 0; i < 8; i++)
            {
                SetFunction(modules, isFunction, 8, size - 1 - i, GetBit(bits, i));
            }
            for (int i = 8; i < 15; i++)
            {
                SetFunction(modules, isFunction, size - 15 + i, 8, GetBit(bits, i));
            }

            // The module just above the bottom left format information is always dark.
            SetFunction(modules, isFunction, size - 8, 8, true);
        }

        /// <summary>Places the codeword bits in the standard two column upward/downward zigzag, skipping function modules.</summary>
        private static void DrawCodewords(bool[,] modules, bool[,] isFunction, int[] codewords)
        {
            int size = modules.GetLength(0);
            int totalBits = codewords.Length * 8;
            int bitIndex = 0;

            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                {
                    right = 5; // skip the vertical timing pattern column
                }

                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int col = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int row = upward ? size - 1 - vert : vert;

                        if (!isFunction[row, col] && bitIndex < totalBits)
                        {
                            modules[row, col] = GetBit(codewords[bitIndex >> 3], 7 - (bitIndex & 7));
                            bitIndex++;
                        }
                        // Any modules left over are remainder bits and stay light, which is correct.
                    }
                }
            }
        }

        /// <summary>XORs one of the eight data masks onto every non function module.</summary>
        private static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask)
        {
            int size = modules.GetLength(0);
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (isFunction[row, col])
                    {
                        continue;
                    }

                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = ((row + col) % 2) == 0; break;
                        case 1: invert = (row % 2) == 0; break;
                        case 2: invert = (col % 3) == 0; break;
                        case 3: invert = ((row + col) % 3) == 0; break;
                        case 4: invert = (((row / 2) + (col / 3)) % 2) == 0; break;
                        case 5: invert = (((row * col) % 2) + ((row * col) % 3)) == 0; break;
                        case 6: invert = ((((row * col) % 2) + ((row * col) % 3)) % 2) == 0; break;
                        default: invert = ((((row + col) % 2) + ((row * col) % 3)) % 2) == 0; break;
                    }

                    if (invert)
                    {
                        modules[row, col] = !modules[row, col];
                    }
                }
            }
        }

        private static bool GetBit(int value, int index)
        {
            return ((value >> index) & 1) != 0;
        }

        #endregion

        #region Mask penalty evaluation

        /// <summary>
        /// Computes the standard four rule penalty score (N1 = 3, N2 = 3, N3 = 40, N4 = 10) used
        /// to pick the data mask. Lower is better.
        /// </summary>
        private static int ComputePenalty(bool[,] modules)
        {
            int size = modules.GetLength(0);
            int result = 0;

            // Rule 1 and rule 3, scanning rows.
            for (int y = 0; y < size; y++)
            {
                bool runColor = false;
                int runLength = 0;
                int[] runHistory = new int[7];

                for (int x = 0; x < size; x++)
                {
                    if (modules[y, x] == runColor)
                    {
                        runLength++;
                        if (runLength == 5)
                        {
                            result += PenaltyN1;
                        }
                        else if (runLength > 5)
                        {
                            result += 1;
                        }
                    }
                    else
                    {
                        FinderPenaltyAddHistory(runLength, runHistory, size);
                        if (!runColor)
                        {
                            result += FinderPenaltyCountPatterns(runHistory) * PenaltyN3;
                        }
                        runColor = modules[y, x];
                        runLength = 1;
                    }
                }

                result += FinderPenaltyTerminateAndCount(runColor, runLength, runHistory, size) * PenaltyN3;
            }

            // Rule 1 and rule 3, scanning columns.
            for (int x = 0; x < size; x++)
            {
                bool runColor = false;
                int runLength = 0;
                int[] runHistory = new int[7];

                for (int y = 0; y < size; y++)
                {
                    if (modules[y, x] == runColor)
                    {
                        runLength++;
                        if (runLength == 5)
                        {
                            result += PenaltyN1;
                        }
                        else if (runLength > 5)
                        {
                            result += 1;
                        }
                    }
                    else
                    {
                        FinderPenaltyAddHistory(runLength, runHistory, size);
                        if (!runColor)
                        {
                            result += FinderPenaltyCountPatterns(runHistory) * PenaltyN3;
                        }
                        runColor = modules[y, x];
                        runLength = 1;
                    }
                }

                result += FinderPenaltyTerminateAndCount(runColor, runLength, runHistory, size) * PenaltyN3;
            }

            // Rule 2: 2x2 blocks of the same colour.
            for (int y = 0; y < size - 1; y++)
            {
                for (int x = 0; x < size - 1; x++)
                {
                    bool color = modules[y, x];
                    if (color == modules[y, x + 1] && color == modules[y + 1, x] && color == modules[y + 1, x + 1])
                    {
                        result += PenaltyN2;
                    }
                }
            }

            // Rule 4: deviation of the dark module proportion from 50%.
            int dark = 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (modules[y, x])
                    {
                        dark++;
                    }
                }
            }
            int total = size * size;
            // Smallest k >= 0 such that (45 - 5k)% <= dark/total <= (55 + 5k)%.
            int k = (int)((Math.Abs(dark * 20L - total * 10L) + total - 1) / total) - 1;
            if (k < 0)
            {
                k = 0;
            }
            result += k * PenaltyN4;

            return result;
        }

        private static void FinderPenaltyAddHistory(int currentRunLength, int[] runHistory, int size)
        {
            if (runHistory[0] == 0)
            {
                currentRunLength += size; // add the light border to the initial run
            }
            for (int i = runHistory.Length - 1; i >= 1; i--)
            {
                runHistory[i] = runHistory[i - 1];
            }
            runHistory[0] = currentRunLength;
        }

        private static int FinderPenaltyCountPatterns(int[] runHistory)
        {
            int n = runHistory[1];
            bool core = n > 0 && runHistory[2] == n && runHistory[3] == n * 3 && runHistory[4] == n && runHistory[5] == n;
            return (core && runHistory[0] >= n * 4 && runHistory[6] >= n ? 1 : 0)
                 + (core && runHistory[6] >= n * 4 && runHistory[0] >= n ? 1 : 0);
        }

        private static int FinderPenaltyTerminateAndCount(bool currentRunColor, int currentRunLength, int[] runHistory, int size)
        {
            if (currentRunColor)
            {
                FinderPenaltyAddHistory(currentRunLength, runHistory, size);
                currentRunLength = 0;
            }
            currentRunLength += size; // add the light border to the final run
            FinderPenaltyAddHistory(currentRunLength, runHistory, size);
            return FinderPenaltyCountPatterns(runHistory);
        }

        #endregion
    }
}
