using UnityEngine;
using UnityEngine.UI;

namespace BadPiggies.Multiplayer.Online.Ui
{
	/// <summary>
	/// Captures the current MainMenu camera at a reduced resolution and presents it through the
	/// lobby's blur material. The capture is refreshed when the lobby opens, so the scene remains
	/// visible behind the translucent command panels without letting the UI capture itself.
	/// </summary>
	[RequireComponent(typeof(RawImage))]
	internal sealed class OnlineLobbyBackdrop : MonoBehaviour
	{
		private RawImage m_image;
		private Camera m_captureCamera;
		private RenderTexture m_target;
		private Material m_material;
		private bool m_ready;

		public void Initialize()
		{
			m_image = GetComponent<RawImage>();
			m_image.raycastTarget = false;
			m_image.color = new Color(0.098f, 0.137f, 0.176f, 1f);
            m_image.enabled = true;
			m_material = CreateMaterial();
			if (m_material != null)
			{
				m_image.material = null;
			}
			m_ready = true;
		}

		public void Refresh()
		{
			if (!m_ready)
			{
				Initialize();
			}
			if (!Application.isPlaying)
			{
				return;
			}

			Camera source = Camera.main;
			if (source == null)
			{
				return;
			}
			EnsureCaptureCamera(source);
			int width = Mathf.Max(320, Screen.width / 2);
			int height = Mathf.Max(180, Screen.height / 2);
			if (m_target == null || m_target.width != width || m_target.height != height)
			{
				ReleaseTarget();
				m_target = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
				{
					name = "Online Lobby MainMenu Blur",
					filterMode = FilterMode.Bilinear,
					wrapMode = TextureWrapMode.Clamp,
					useMipMap = false,
					antiAliasing = 1
				};
				m_target.Create();
			}

			m_captureCamera.targetTexture = m_target;
			m_captureCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
			m_captureCamera.fieldOfView = source.fieldOfView;
			m_captureCamera.orthographic = source.orthographic;
			m_captureCamera.orthographicSize = source.orthographicSize;
			m_captureCamera.Render();
			m_image.material = m_material;
            m_image.color = Color.white;
            m_image.texture = m_target;
            m_image.enabled = true;
		}

		private void EnsureCaptureCamera(Camera source)
		{
			if (m_captureCamera != null)
			{
				m_captureCamera.cullingMask = source.cullingMask;
				m_captureCamera.clearFlags = source.clearFlags;
				m_captureCamera.backgroundColor = source.backgroundColor;
				return;
			}
			GameObject host = new GameObject("Online Lobby Backdrop Capture Camera");
			host.transform.SetParent(transform, false);
			m_captureCamera = host.AddComponent<Camera>();
			m_captureCamera.CopyFrom(source);
			m_captureCamera.enabled = false;
			m_captureCamera.depth = -1000f;
			m_captureCamera.allowHDR = false;
			m_captureCamera.allowMSAA = false;
		}

		private static Material CreateMaterial()
		{
			Shader shader = Shader.Find("BPRC/Online Lobby/Backdrop Blur");
			return shader == null ? null : new Material(shader) { name = "Online Lobby Backdrop Blur Material" };
		}

		private void OnDestroy()
		{
			ReleaseTarget();
			if (m_captureCamera != null)
			{
				DestroyObject(m_captureCamera.gameObject);
				m_captureCamera = null;
			}
			if (m_material != null)
			{
				DestroyObject(m_material);
				m_material = null;
			}
		}

		private void ReleaseTarget()
		{
			if (m_target == null)
			{
				return;
			}
			m_target.Release();
			DestroyObject(m_target);
			m_target = null;
		}

		private static void DestroyObject(Object target)
		{
			if (target == null)
			{
				return;
			}
			if (Application.isPlaying)
			{
				Object.Destroy(target);
			}
			else
			{
				Object.DestroyImmediate(target);
			}
		}
	}
}
