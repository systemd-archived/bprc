using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BadPiggies.Multiplayer
{
public class MultiplayerMenuInjector : MonoBehaviour
{
	private const string CloneSourceName = "StartButton";

	private const string LayoutParentName = "StartLayout";

	private const string MultiplayerButtonName = "MultiplayerButton";

	private const string ButtonMaterialResourcePath = "Multiplayer/MultiplayerButton";

	private const string ArtChildName = "MultiplayerButtonArt";

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void Start()
	{
		TryInject();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (scene.name == "MainMenu")
		{
			TryInject();
		}
	}

	private void TryInject()
	{
		if (GameObject.Find("MultiplayerButton") != null)
		{
			return;
		}
		GameObject gameObject = FindInMenu("StartButton");
		if (gameObject == null)
		{
			return;
		}
		GameObject gameObject2 = Object.Instantiate(gameObject, gameObject.transform.parent);
		gameObject2.name = "MultiplayerButton";
		Vector3 localPosition = gameObject.transform.localPosition;
		gameObject2.transform.localPosition = localPosition;
		gameObject2.transform.localRotation = gameObject.transform.localRotation;
		gameObject2.transform.localScale = gameObject.transform.localScale;
		PlayerLevelRequirement component = gameObject2.GetComponent<PlayerLevelRequirement>();
		if (component != null)
		{
			Object.Destroy(component);
		}
		MultiplayerMenuButton targetComponent = gameObject2.AddComponent<MultiplayerMenuButton>();
		Button component2 = gameObject2.GetComponent<Button>();
		if (component2 != null)
		{
			component2.MethodToCall.SetMethod(targetComponent, "OpenMultiplayer");
		}
		else
		{
			AnimatedButton component3 = gameObject2.GetComponent<AnimatedButton>();
			if (component3 != null)
			{
				component3.MethodToCall.SetMethod(targetComponent, "OpenMultiplayer");
			}
			else
			{
				Debug.LogWarning("[Multiplayer] Cloned menu button exposes no Button/AnimatedButton; the Multiplayer button will not respond to clicks.");
			}
		}
		if (!ApplyMultiplayerArt(gameObject2))
		{
			ApplyLabel(gameObject2, "MULTIPLAYER");
		}
		MultiplayerMenuLayout.Arrange(FindCakeRaceButton(), gameObject, gameObject2);
		Debug.Log("[Multiplayer] Multiplayer button injected into the main menu.");
	}

	private static bool ApplyMultiplayerArt(GameObject clone)
	{
		Material material = Resources.Load<Material>("Multiplayer/MultiplayerButton");
		if (material == null)
		{
			Debug.LogWarning("[Multiplayer] No button material at Resources/Multiplayer/MultiplayerButton; the Multiplayer button will keep the cloned Play artwork.");
			return false;
		}
		MeshFilter component = clone.GetComponent<MeshFilter>();
		MeshRenderer component2 = clone.GetComponent<MeshRenderer>();
		if (component == null || component.sharedMesh == null || component2 == null)
		{
			Debug.LogWarning("[Multiplayer] The cloned menu button has no generated mesh yet; keeping the cloned Play artwork.");
			return false;
		}
		Bounds bounds = component.sharedMesh.bounds;
		float y = bounds.extents.y;
		float num = 1f;
		Texture mainTexture = material.mainTexture;
		if (mainTexture != null && mainTexture.height > 0)
		{
			num = (float)mainTexture.width / (float)mainTexture.height;
		}
		float num2 = y * num;
		Transform transform = clone.transform.Find("MultiplayerButtonArt");
		if (transform != null)
		{
			Object.Destroy(transform.gameObject);
		}
		int num3 = 0;
		Renderer[] componentsInChildren = clone.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (!(componentsInChildren[i] == null))
			{
				componentsInChildren[i].enabled = false;
				num3++;
			}
		}
		GameObject gameObject = new GameObject("MultiplayerButtonArt");
		gameObject.layer = clone.layer;
		gameObject.transform.SetParent(clone.transform, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0f);
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one;
		Mesh mesh = new Mesh
		{
			name = "MultiplayerButtonQuad"
		};
		mesh.vertices = new Vector3[4]
		{
			new Vector3(0f - num2, 0f - y, 0f),
			new Vector3(0f - num2, y, 0f),
			new Vector3(num2, y, 0f),
			new Vector3(num2, 0f - y, 0f)
		};
		mesh.triangles = new int[6] { 0, 1, 2, 2, 3, 0 };
		mesh.uv = new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(0f, 1f),
			new Vector2(1f, 1f),
			new Vector2(1f, 0f)
		};
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = material;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		ApplyLabel(clone, string.Empty);
		Debug.Log($"[Multiplayer] Multiplayer button art applied ({num2 * 2f:0.00} x " + $"{y * 2f:0.00} menu units, {num3} cloned renderer(s) hidden).");
		return true;
	}

	private static void ApplyLabel(GameObject clone, string label)
	{
		TextMesh[] componentsInChildren = clone.GetComponentsInChildren<TextMesh>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			TextMeshLocale component = componentsInChildren[i].GetComponent<TextMeshLocale>();
			if (component != null)
			{
				Object.Destroy(component);
			}
			componentsInChildren[i].text = label;
		}
	}

	/// <summary>
	/// The Cake Race button normally sits next to Play under StartLayout, but the unlock sequence
	/// detaches it from that parent, so fall back to a plain search by name.
	/// </summary>
	private static GameObject FindCakeRaceButton()
	{
		GameObject gameObject = FindInMenu(MultiplayerMenuLayout.CakeRaceButtonName);
		if (gameObject != null)
		{
			return gameObject;
		}
		return GameObject.Find(MultiplayerMenuLayout.CakeRaceButtonName);
	}

	private static GameObject FindInMenu(string objectName)
	{
		Scene activeScene = SceneManager.GetActiveScene();
		if (!activeScene.IsValid())
		{
			return null;
		}
		GameObject[] rootGameObjects = activeScene.GetRootGameObjects();
		for (int i = 0; i < rootGameObjects.Length; i++)
		{
			Transform[] componentsInChildren = rootGameObjects[i].GetComponentsInChildren<Transform>(includeInactive: true);
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				if (componentsInChildren[j].name == objectName && IsUnder(componentsInChildren[j], "StartLayout"))
				{
					return componentsInChildren[j].gameObject;
				}
			}
		}
		return null;
	}

	private static bool IsUnder(Transform node, string ancestorName)
	{
		Transform parent = node.parent;
		while (parent != null)
		{
			if (parent.name == ancestorName)
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}
}
}
