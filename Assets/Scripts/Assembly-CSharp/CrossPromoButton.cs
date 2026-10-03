using System;
using UnityEngine;

public class CrossPromoButton : MonoBehaviour
{
	public enum CrossPromoType
	{
		Main = 0,
		Episode = 1
	}

	[SerializeField]
	private CrossPromoType crossPromoType;

	[SerializeField]
	private UnmanagedSprite sprite;

	[SerializeField]
	private bool sendImpressions = true;

	private bool reportedImpression;

	// The Debug.LogWarning calls the original build left in this component were removed: the episode
	// selector instantiates two prefabs that carry it every time the level list is built, so each visit
	// logged "CrossPromoButton::Awake" (and would have logged the rest) without saying anything a player
	// or a log reader can act on. Nothing reads those strings - see RCR2_PORT.md.
	private void Awake()
	{
		if (crossPromoType == CrossPromoType.Main && AdvertisementHandler.CrossPromoMainRenderable != null)
		{
			if (AdvertisementHandler.GetCrossPromoMainTexture() != null)
			{
				OnRenderableReady(isReady: true);
				return;
			}
			AdvertisementHandler.RenderableHandler crossPromoMainRenderable = AdvertisementHandler.CrossPromoMainRenderable;
			crossPromoMainRenderable.onRenderableReady = (Action<bool>)Delegate.Combine(crossPromoMainRenderable.onRenderableReady, new Action<bool>(OnRenderableReady));
			base.gameObject.SetActive(value: false);
		}
		else if (crossPromoType == CrossPromoType.Episode && AdvertisementHandler.CrossPromoEpisodeRenderable != null)
		{
			if (AdvertisementHandler.GetCrossPromoEpisodeTexture() != null)
			{
				OnRenderableReady(isReady: true);
				return;
			}
			AdvertisementHandler.RenderableHandler crossPromoEpisodeRenderable = AdvertisementHandler.CrossPromoEpisodeRenderable;
			crossPromoEpisodeRenderable.onRenderableReady = (Action<bool>)Delegate.Combine(crossPromoEpisodeRenderable.onRenderableReady, new Action<bool>(OnRenderableReady));
			base.gameObject.SetActive(value: false);
		}
		else
		{
			base.gameObject.SetActive(value: false);
		}
	}

	private void OnDestroy()
	{
		if (crossPromoType == CrossPromoType.Main && AdvertisementHandler.CrossPromoMainRenderable != null)
		{
			AdvertisementHandler.RenderableHandler crossPromoMainRenderable = AdvertisementHandler.CrossPromoMainRenderable;
			crossPromoMainRenderable.onRenderableReady = (Action<bool>)Delegate.Remove(crossPromoMainRenderable.onRenderableReady, new Action<bool>(OnRenderableReady));
		}
		else if (crossPromoType == CrossPromoType.Episode && AdvertisementHandler.CrossPromoEpisodeRenderable != null)
		{
			AdvertisementHandler.RenderableHandler crossPromoEpisodeRenderable = AdvertisementHandler.CrossPromoEpisodeRenderable;
			crossPromoEpisodeRenderable.onRenderableReady = (Action<bool>)Delegate.Remove(crossPromoEpisodeRenderable.onRenderableReady, new Action<bool>(OnRenderableReady));
		}
	}

	public void OnPressed()
	{
	}

	private void OnRenderableReady(bool isReady)
	{
		if (Application.isPlaying && isReady)
		{
			Renderer component = sprite.GetComponent<Renderer>();
			if (crossPromoType == CrossPromoType.Main && AdvertisementHandler.CrossPromoMainRenderable != null)
			{
				component.material.mainTexture = AdvertisementHandler.GetCrossPromoMainTexture();
			}
			else if (crossPromoType == CrossPromoType.Episode && AdvertisementHandler.CrossPromoEpisodeRenderable != null)
			{
				component.material.mainTexture = AdvertisementHandler.GetCrossPromoEpisodeTexture();
			}
			if (component.material != null && component.material.mainTexture != null)
			{
				float num = component.material.mainTexture.width;
				float num2 = component.material.mainTexture.height;
				component.transform.localScale = Vector3.up * component.transform.localScale.y + Vector3.right * component.transform.localScale.y * (num / num2);
				OnRenderableShow();
			}
		}
	}

	public void OnRenderableShow()
	{
		if (!reportedImpression && sendImpressions)
		{
			base.gameObject.SetActive(value: true);
			reportedImpression = true;
		}
	}

	public void OnRenderableHide()
	{
	}
}
