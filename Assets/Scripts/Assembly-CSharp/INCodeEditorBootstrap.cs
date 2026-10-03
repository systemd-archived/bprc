using UnityEngine;

// 【已停用】此前用“运行时注入”方式给应用界面加“代码编辑器”页面与侧栏入口。
// 现改为与应用界面其它页面（设置界面/指令系统/星海设置等）**同级的注册方式**：
//   - 页面 prefab：Assets/Material/GameObject/CodeEditorInterface.prefab（根物体挂了 INCodeEditorPage）
//   - 注册位置：INApplicationInterface.prefab 的 INAppInterface.m_elements 与 AppInterfaceSidebar.m_sidebarElements
// 因此本类不再自动运行，保留仅作参考（如需回退到运行时注入，取消下方方法注释即可）。
public class INCodeEditorBootstrap : MonoBehaviour
{
	// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	// private static void Bootstrap()
	// {
	//     GameObject go = new GameObject("INCodeEditorBootstrap");
	//     Object.DontDestroyOnLoad(go);
	//     go.AddComponent<INCodeEditorBootstrap>();
	// }
}
