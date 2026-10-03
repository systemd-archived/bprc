using IngameDebugConsole;
using UnityEngine;

/// <summary>
/// Holds the console's floating popup window at <see cref="INDebugConsole.PopupEnabled"/>.
/// </summary>
/// <remarks>
/// The bundled manager asserts its own preference for that window in its <c>Start</c>
/// (<c>PopupEnabled = popupVisibility != PopupVisibility.Never</c>), and the staged prefab leaves that
/// preference at the asset's <c>Always</c> - it is what keeps the popup's opacity, raycast state and
/// position correct while the log window opens and closes. That <c>Start</c> runs after
/// <see cref="INDebugConsole.Ensure"/> has prepared the console, though, so without this the floating
/// button would turn itself back on one frame after boot, whatever 通用设置's 显示debug悬浮窗 switch says.
///
/// This enforces the one bit that belongs to this project and writes nothing at all while the two
/// already agree, which is every frame but the exceptions. It lives on the console's own root, so it is
/// created and destroyed with it and never outlives the instance it is watching.
/// </remarks>
public sealed class INDebugConsolePopupKeeper : MonoBehaviour
{
	private void LateUpdate()
	{
		DebugLogManager manager = DebugLogManager.Instance;
		if (manager != null && manager.PopupEnabled != INDebugConsole.PopupEnabled)
		{
			manager.PopupEnabled = INDebugConsole.PopupEnabled;
		}
	}
}
