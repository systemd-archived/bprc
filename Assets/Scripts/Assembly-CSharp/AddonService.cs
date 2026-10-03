using System.Collections.Generic;
using System.IO;
using Innovation;
using UnityEngine;

internal class AddonService : IAddonService, IResourceResolver
{
	public string GetAddonDataPath()
	{
		return INAddonManager.DataPath;
	}

	public string LoadFileAsString(string path)
	{
		return INAddonManager.LoadFileAsString(path);
	}

	public byte[] LoadFileAsBytes(string path)
	{
		return INAddonManager.LoadFileAsBytes(path);
	}

	public void RunScript(string code, bool printCode)
	{
		INAddonManager.RunScript(code, printCode);
	}

	public void RunScriptFromFile(string path, bool printCode)
	{
		INAddonManager.RunScriptFromFile(path, printCode);
	}

	public Texture2D LoadTexture(byte[] data, string name)
	{
		return INAddonManager.LoadTexture(data, name);
	}

	public Texture2D LoadTextureFromFile(string path, string name)
	{
		return INAddonManager.LoadTextureFromFile(path, name);
	}

	public AudioClip LoadAudio(byte[] data, string name)
	{
		return INAddonManager.LoadAudioWithNLayer(data, name);
	}

	public AudioClip LoadAudioFromFile(string path, string name)
	{
		return INAddonManager.LoadAudioFromFileWithNLayer(path, name);
	}

	public IContraptionData LoadContraptionData(string text)
	{
		INContraptionDataManager.TryLoadDataFromCsv(text, out var result);
		return result;
	}

	public IContraptionData LoadContraptionDataFromFile(string path)
	{
		INContraptionDataManager.TryLoadDataFromCsv(File.ReadAllText(path), out var result);
		return result;
	}

	public void SetTexturePack(Texture2D texture)
	{
		INAddonManager.Instance.SetTexturePack(texture);
	}

	public void SetAudioPack(AudioClip audioClip, string name)
	{
		INAddonManager.Instance.SetAudioPack(audioClip, name);
	}

	public AddonPackage FindPackage(string id)
	{
		return INAddonManager.Instance.PackageManager.FindPackage(id);
	}

	public AddonPackage FindCurrentPackage()
	{
		return INAddonManager.Instance.PackageManager.CurrentPackage;
	}

	public AddonComponent FindAddonComponent(string name)
	{
		return INAddonManager.Instance.FindAddonComponent(name);
	}

	public IReadOnlyList<AddonComponent> FindAddonComponents(string name)
	{
		return INAddonManager.Instance.FindAddonComponents(name);
	}

	public AddonBackground CreateBackground(Texture2D texture = null, Color? color = null, LocationMode locationMode = LocationMode.CameraAndScreen)
	{
		return INAddonManager.Instance.CreateBackground(texture, color, locationMode);
	}

	public AddonVideoPlayer CreateVideoPlayer(string path, LocationMode locationMode = LocationMode.CameraAndScreen)
	{
		return INAddonManager.Instance.CreateVideoPlayer(path, locationMode);
	}

	public GameObject CreateGameObject(GameObjectTemplate template, IResourceResolver resolver)
	{
		return TemplateFactory.ApplyTemplate(template, resolver ?? this);
	}

	public IBasePart CreateCustomPart(CustomPartTemplate template, IResourceResolver resolver)
	{
		BasePart basePart;
		if (template.UnderlyingPartType == PartTypeCode.Unknown)
		{
			basePart = Object.Instantiate(INUnity.LoadScriptableObject<PartListData>("PartListData").CustomPartPrefab).AddOrGetComponent<BasePart>();
		}
		else
		{
			BasePart.PartType partType = ((SortedPartType)template.UnderlyingPartType).ToPartType();
			int underlyingPartIndex = template.UnderlyingPartIndex;
			basePart = Object.Instantiate(Singleton<INPartFactoryManager>.Instance.FindCustomPart(partType, underlyingPartIndex));
		}
		TemplateFactory.ApplyTemplate(basePart.gameObject, template, resolver ?? this);
		basePart.ApplyTemplate(template, resolver ?? this);
		// --- addon custom part fixes (see notes) ------------------------------------------
		// 1) Rename like the game's own custom parts. PartListing only shows an icon whose
		//    name looks like "Part_<Type>_<int>_SET" (it parses name.Split('_')[2] as int).
		string partName = "Part_" + basePart.Type.ToString() + "_" + (basePart.Index + 1) + "_SET";
		basePart.gameObject.name = partName;
		// 2) The icon prefab carries a Sprite whose m_id points at a generic atlas sprite; its
		//    Awake() (also on every PartListing clone) would re-mesh the icon and overwrite the
		//    UVs. Clear it so the unit quad + the template's own texture stays.
		Sprite iconSprite = basePart.m_constructionIconSprite;
		if (iconSprite != null)
		{
			iconSprite.m_id = string.Empty;
			// 3) PartListing caches materials by NAME (ToDark/ToGray -> GetDarkMaterial/
			//    GetNormalMaterial). Every icon material instantiated from the icon prefab is
			//    called "Default-Material (Instance)", so all addon icons were collapsed onto one
			//    cached material and looked identical. Give each its own name.
			MeshRenderer iconRenderer = iconSprite.GetComponent<MeshRenderer>();
			if (iconRenderer != null && iconRenderer.sharedMaterial != null)
			{
				iconRenderer.sharedMaterial.name = "AddonIcon_" + partName;
			}
		}
		// 4) PartListing greys locked custom parts (ToGray) and disables their collider/button.
		//    Mark the part unlocked so it keeps its own material and stays selectable.
		GameProgress.SetBool(partName, true);
		// ----------------------------------------------------------------------------------
		Singleton<INPartFactoryManager>.Instance.SetParent(basePart);
		Singleton<INPartFactoryManager>.Instance.AddExtraPart(basePart);
		return basePart;
	}

	Texture2D IResourceResolver.ResolveTexture(string path)
	{
		return LoadTextureFromFile(path, Path.GetFileName(path));
	}

	AudioClip IResourceResolver.ResolveAudio(string path)
	{
		return LoadAudioFromFile(path, Path.GetFileName(path));
	}

	Shader IResourceResolver.ResolveShader(string path)
	{
		return Shader.Find(path);
	}
}
