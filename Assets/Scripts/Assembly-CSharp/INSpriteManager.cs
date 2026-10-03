using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class INSpriteManager : MonoBehaviour
{
	[SerializeField]
	private List<TextAsset> m_textAssets;

	private bool m_initialized;

	private Dictionary<string, Dictionary<string, INSpriteData>> m_atlasData;

	public static INSpriteManager Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
		// INInitializer instantiates this manager from a prefab, so it starts life under that prefab's
		// root. DontDestroyOnLoad only accepts a root object, which is why the call was refused in play
		// mode: the manager was then destroyed on the next scene load and every later
		// INSpriteManager.Instance lookup — INSerializedSprite, UIPartButtonList, UIPartSliderButton —
		// went with it. Detaching first is what makes the intent stick.
		transform.SetParent(null, worldPositionStays: true);
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
	}

	public void Initialize()
	{
		m_initialized = true;
		m_atlasData = new Dictionary<string, Dictionary<string, INSpriteData>>(m_textAssets.Count);
		foreach (TextAsset textAsset in m_textAssets)
		{
			LoadAtlasData(textAsset.text);
		}
	}

	public Dictionary<string, INSpriteData> GetAtlasData(string textureName)
	{
		if (!m_initialized)
		{
			Initialize();
		}
		return m_atlasData[textureName];
	}

	private void LoadAtlasData(string text)
	{
		Dictionary<string, INSpriteData> dictionary = new Dictionary<string, INSpriteData>();
		IFormatProvider invariantInfo = NumberFormatInfo.InvariantInfo;
		string[] array = text.Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
		string[] array2 = array[0].Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		string key = array2[0];
		Vector2Int textureSize = new Vector2Int(int.Parse(array2[1], invariantInfo), int.Parse(array2[2], invariantInfo));
		for (int i = 1; i < array.Length; i++)
		{
			array2 = array[i].Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			string key2 = array2[0];
			Rect rect = new Rect(float.Parse(array2[1], invariantInfo), float.Parse(array2[2], invariantInfo), float.Parse(array2[3], invariantInfo), float.Parse(array2[4], invariantInfo));
			Vector2 scale = new Vector2(float.Parse(array2[5], invariantInfo), float.Parse(array2[6], invariantInfo));
			int screenHeight = int.Parse(array2[7], invariantInfo);
			dictionary[key2] = new INSpriteData(rect, scale, screenHeight, textureSize);
		}
		m_atlasData.Add(key, dictionary);
	}
}
