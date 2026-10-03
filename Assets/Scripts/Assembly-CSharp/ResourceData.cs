using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceData", menuName = "ScriptableObjects/ResourceData", order = 1)]
public class ResourceData : ScriptableObject
{
	public List<GameObject> Prefabs;

	public List<Font> Fonts;

	public List<Texture> Textures;

	public List<Shader> Shaders;

	public List<Material> Materials;

	public List<TextAsset> TextAssets;

	public List<ScriptableObject> ScriptableObjects;
}
