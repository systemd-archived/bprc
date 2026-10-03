using UnityEngine;

namespace BadPiggies.Multiplayer
{
public class MultiplayerMenuButton : MonoBehaviour
{
	public void OpenMultiplayer()
	{
		MultiplayerBootstrap.OpenLobby();
	}
}
}
