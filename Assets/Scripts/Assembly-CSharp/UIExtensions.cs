using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public static class UIExtensions
{
	private static List<RaycastResult> s_raycastResults = new List<RaycastResult>();
	private static EventSystem s_pointerSystem;
	private static PointerEventData s_pointerEvent;

	public static bool IsPointerOverUIObject(this EventSystem eventSystem, Vector2 position)
	{
		if (eventSystem == null) return false;
		if (s_pointerEvent == null || s_pointerSystem != eventSystem)
		{
			s_pointerSystem = eventSystem;
			s_pointerEvent = new PointerEventData(eventSystem);
		}
		s_pointerEvent.Reset();
		s_pointerEvent.position = position;
		eventSystem.RaycastAll(s_pointerEvent, s_raycastResults);
		bool result = s_raycastResults.Count > 0;
		s_raycastResults.Clear();
		return result;
	}

	public static UniTask PlayFadeInAnimation(this CanvasRenderer canvasRenderer, float duration, bool ignoreTimeScale)
	{
		return canvasRenderer.PlayAlphaAnimation(0f, 1f, duration, ignoreTimeScale);
	}

	public static UniTask PlayFadeInAnimation(this CanvasGroup canvasGroup, float duration, bool ignoreTimeScale)
	{
		return canvasGroup.PlayAlphaAnimation(0f, 1f, duration, ignoreTimeScale);
	}

	public static UniTask PlayFadeOutAnimation(this CanvasRenderer canvasRenderer, float duration, bool ignoreTimeScale)
	{
		return canvasRenderer.PlayAlphaAnimation(1f, 0f, duration, ignoreTimeScale);
	}

	public static UniTask PlayFadeOutAnimation(this CanvasGroup canvasGroup, float duration, bool ignoreTimeScale)
	{
		return canvasGroup.PlayAlphaAnimation(1f, 0f, duration, ignoreTimeScale);
	}

	public static UniTask PlayAlphaAnimation(this CanvasRenderer canvasRenderer, float alpha0, float alpha1, float duration, bool ignoreTimeScale)
	{
		Action<float> setter = delegate(float alpha2)
		{
			canvasRenderer.SetAlpha(alpha2);
		};
		return PlayLinearAnimation(alpha0, alpha1, duration, ignoreTimeScale, setter);
	}

	public static UniTask PlayAlphaAnimation(this CanvasGroup canvasGroup, float alpha0, float alpha1, float duration, bool ignoreTimeScale)
	{
		Action<float> setter = delegate(float alpha2)
		{
			canvasGroup.alpha = alpha2;
		};
		return PlayLinearAnimation(alpha0, alpha1, duration, ignoreTimeScale, setter);
	}

	public static async UniTask PlayLinearAnimation(float from, float to, float duration, bool ignoreTimeScale, Action<float> setter)
	{
		float deltaTime = (ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime);
		for (float time = 0f; time < duration; time += deltaTime)
		{
			float num = time / duration;
			float obj = from * (1f - num) + to * num;
			setter(obj);
			await UniTask.NextFrame();
		}
		setter(to);
	}
}
