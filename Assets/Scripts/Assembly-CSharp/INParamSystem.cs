// 长按“设置菜单”（自定义数值）总开关。关闭后完全按原版逻辑：不读取/不应用自定义数值。
public static class INParamSystem
{
	public static bool Enabled
	{
		get { return INUserSettings.Instance?.StarSeaSettings?.EnableParamPanel ?? true; }
	}
}
