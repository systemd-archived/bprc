using System.Runtime.InteropServices;
using Mirror;

namespace BadPiggies.Multiplayer
{
[StructLayout(LayoutKind.Sequential, Size = 1)]
public struct MultiplayerSessionClosingMessage : NetworkMessage
{
}
}
