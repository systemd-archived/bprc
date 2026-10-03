using UnityEngine;

/// <summary>
/// 军事版 24.7.12 里主工程没有的零件行为，统一由设置界面“部件设置 → 军事版功能”这一个开关控制。
/// 开关关闭时（默认）每个读取点都走主工程原有逻辑，行为与移植前完全一致。
/// </summary>
/// <remarks>
/// 移植清单与逐条理由见 <c>docs/MilitaryPartsDiff.md</c>：只搬行为、不搬架构，
/// 所以这里不引用军事版的电气/物理 API，全部接到主工程自己的设施上。
/// </remarks>
public static class INMilitaryFeatures
{
	/// <summary>猪的移动（军事版 Pig.cs 的 Update 段）：起跳力、水平力、水平速度上限、回正力矩。</summary>
	public const float PigJumpForce = 200f;

	public const float PigMoveForce = 10f;

	public const float PigMaxMoveSpeed = 5f;

	public const float PigUprightTorque = 0.5f;

	/// <summary>军事版的下潜力，只在水中生效（军事版没有 S 键，这里补齐 WASD 的“下”）。</summary>
	public const float PigDiveForce = 60f;

	/// <summary>
	/// 设置界面的“军事版功能”开关。设置尚未初始化时按关闭处理，避免在任何加载顺序下意外开启。
	/// </summary>
	public static bool Enabled
	{
		get
		{
			try
			{
				return INSettings.GetBool(INFeature.MilitaryFeatures);
			}
			catch
			{
				return false;
			}
		}
	}

	/// <summary>军事版用方向键，这里同时接受 WASD 与方向键。</summary>
	public static bool JumpPressed => Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);

	public static bool LeftPressed => Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);

	public static bool RightPressed => Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

	public static bool DownPressed => Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

	/// <summary>
	/// 猪是否在水里。军事版靠 <c>CompareTag("WaterSystem")</c> 的触发器判断，而主工程的水位由
	/// <see cref="WaterSystem"/> 从 <see cref="PartSettings.WaterLevel"/> 取，也没有那个标签，
	/// 所以这里直接按同一个水位判断，结果与主工程自己的浮力系统一致。
	/// </summary>
	public static bool IsUnderwater(Transform source)
	{
		if (source == null || INUserSettings.Instance == null)
		{
			return false;
		}
		PartSettings settings = INUserSettings.Instance.PartSettings;
		if (settings == null || !settings.EnableWaterSystem)
		{
			return false;
		}
		return source.position.y < settings.WaterLevel;
	}
}
