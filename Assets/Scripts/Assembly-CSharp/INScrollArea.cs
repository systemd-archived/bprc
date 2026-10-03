using System;
using UnityEngine;
using UnityEngine.EventSystems;

// 给任意 UI 区域添加“拖动 + 滚轮”滚动，以“步（行）”为单位回调。
// OnStep(+n) = 向下（显示更后面的内容）；OnStep(-n) = 向上。
public class INScrollArea : MonoBehaviour, IDragHandler, IScrollHandler
{
	public Action<int> OnStep;

	// 拖动多少像素算一步
	public float PixelsPerStep = 34f;

	// 滚轮一格算几步
	public float WheelStepsPerNotch = 1f;

	private float m_accum;

	public void OnScroll(PointerEventData eventData)
	{
		if (OnStep == null)
		{
			return;
		}
		float dy = eventData != null ? eventData.scrollDelta.y : 0f; // 上滚为正
		m_accum += dy * WheelStepsPerNotch;
		int steps = (int)m_accum;
		if (steps != 0)
		{
			m_accum -= steps;
			OnStep(-steps); // 上滚 = 回看更前面的内容
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (OnStep == null || eventData == null || PixelsPerStep <= 0f)
		{
			return;
		}
		m_accum += eventData.delta.y / PixelsPerStep; // 向上拖动 = 正 = 向后
		int steps = (int)m_accum;
		if (steps != 0)
		{
			m_accum -= steps;
			OnStep(steps);
		}
	}
}
