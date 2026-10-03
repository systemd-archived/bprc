using Mirror;

namespace BadPiggies.Multiplayer
{
public struct MultiplayerLoadArenaMessage : NetworkMessage
{
	public string sandboxIdentifier;

	public int round;
}
}
