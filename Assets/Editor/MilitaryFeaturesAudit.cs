using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 军事版功能移植的自检：开关本身、设置数据、分类谓词，以及每个移植点在源码里确实被开关拦住。
/// </summary>
/// <remarks>
/// 这个脚本本身引用了 <see cref="INMilitaryFeatures.Enabled"/> 和 <c>BasePart.IsNuclearBomb()</c>
/// 之类的成员，所以它能通过编译这件事，就已经证明这些接口存在；下面再核对数据与插桩点。
/// </remarks>
public static class MilitaryFeaturesAudit
{
	private const string ReportPath = "Builds/QA/military-features.txt";

	private const string SitesPath = "Builds/QA/military-features-sites.csv";

	private static readonly string[] GatedFiles =
	{
		"Assets/Scripts/Assembly-CSharp/Pig.cs",
		"Assets/Scripts/Assembly-CSharp/Rocket.cs",
		"Assets/Scripts/Assembly-CSharp/FuelBox.cs",
		"Assets/Scripts/Assembly-CSharp/Spring.cs",
		"Assets/Scripts/Assembly-CSharp/Frame.cs",
		"Assets/Scripts/Assembly-CSharp/BasePart.cs",
		"Assets/Scripts/Assembly-CSharp/TNT.cs",
	};

	[MenuItem("Tools/Innovation/Audit military features")]
	public static void Run()
	{
		List<string> failures = new List<string>();
		List<string> rows = new List<string> { "file,gateSites" };

		// 1. 开关本身：枚举成员编号、属性可读、默认关闭。
		Require(Enum.IsDefined(typeof(INFeature), "MilitaryFeatures"), "INFeature.MilitaryFeatures 缺失", failures);
		int featureValue = (int)(INFeature)Enum.Parse(typeof(INFeature), "MilitaryFeatures");
		Require(featureValue == 208, "MilitaryFeatures 的编号应为 208，实际 " + featureValue, failures);
		PropertyInfo enabledProperty = typeof(INMilitaryFeatures).GetProperty("Enabled", BindingFlags.Public | BindingFlags.Static);
		Require(enabledProperty != null && enabledProperty.PropertyType == typeof(bool), "INMilitaryFeatures.Enabled 不是静态 bool 属性", failures);
		PropertyInfo settingsProperty = typeof(PartSettings).GetProperty("MilitaryFeatures", BindingFlags.Public | BindingFlags.Instance);
		Require(settingsProperty != null && settingsProperty.PropertyType == typeof(bool), "PartSettings.MilitaryFeatures 不是 bool 属性", failures);
		Require(!new PartSettings().MilitaryFeatures, "PartSettings 默认应当关闭军事版功能", failures);

		// 2. 设置数据：声明、各版本默认值、别名、本地化标签都要有这一项。
		RequireJson("Assets/TextAsset/INDeclarationSettingsExp.json", "items", "MilitaryFeatures", failures);
		RequireJson("Assets/TextAsset/INDeclarationSettings.json", "items", "MilitaryFeatures", failures);
		foreach (string version in new[] { "A", "O", "B" })
		{
			RequireJson("Assets/TextAsset/INSettings" + version + "Exp.json", "items", "MilitaryFeatures", failures);
			RequireJson("Assets/TextAsset/INSettings" + version + ".json", "items", "MilitaryFeatures", failures);
		}
		RequireJsonKey("Assets/TextAsset/INAliasSettings.json", "MilitaryFeatures", failures);
		RequireJson("Assets/TextAsset/INLocalizationData.json", "data", "PartSettings_MilitaryFeatures", "id", failures);

		// 3. 分类谓词与编号常量。
		foreach (string predicate in new[] { "IsHeavyBomb", "IsNuclearBomb", "IsAcidBomb", "IsAlienBomb", "IsAPRocket", "IsNeoFrame" })
		{
			MethodInfo method = typeof(PartExtensions).GetMethod(predicate, BindingFlags.Public | BindingFlags.Static);
			Require(method != null && method.ReturnType == typeof(bool), "PartExtensions." + predicate + " 缺失", failures);
		}
		foreach (string constant in new[] { "HeavyBomb", "NuclearBomb", "AcidBomb", "AlienBomb", "APRocket" })
		{
			PropertyInfo property = typeof(BasePart).GetProperty(constant, BindingFlags.Public | BindingFlags.Static);
			Require(property != null, "BasePart." + constant + " 缺失", failures);
		}

		// 4. 每个移植点都要真的被开关拦住：统计源码里的引用次数。
		foreach (string file in GatedFiles)
		{
			string text = File.ReadAllText(file);
			int sites = CountOccurrences(text, "INMilitaryFeatures.");
			rows.Add(file.Substring(file.LastIndexOf('/') + 1) + "," + sites);
			Require(sites > 0, Path.GetFileName(file) + " 里没有任何 INMilitaryFeatures 引用", failures);
		}
		Require(CountOccurrences(File.ReadAllText(GatedFiles[0]), "INMilitaryFeatures.") >= 3, "Pig.cs 的军事版移动插桩点少于 3 处", failures);
		Require(CountOccurrences(File.ReadAllText(GatedFiles[1]), "INMilitaryFeatures.") >= 5, "Rocket.cs 的 AP 火箭/核弹守卫插桩点少于 5 处", failures);

		Directory.CreateDirectory("Builds/QA");
		File.WriteAllLines(SitesPath, rows);
		List<string> report = new List<string>
		{
			"MilitaryFeatures = " + featureValue,
			"PartSettings.MilitaryFeatures default = " + new PartSettings().MilitaryFeatures,
			"gated files = " + GatedFiles.Length,
			"failures = " + failures.Count,
		};
		report.AddRange(failures);
		File.WriteAllLines(ReportPath, report);
		if (failures.Count > 0)
		{
			throw new Exception(string.Join("\n", failures));
		}
		Debug.Log("MILITARY_FEATURES_AUDIT_PASS: feature flag, settings data, predicates and " + GatedFiles.Length + " gated files checked.");
	}

	private static int CountOccurrences(string text, string needle)
	{
		int count = 0;
		int index = text.IndexOf(needle, StringComparison.Ordinal);
		while (index >= 0)
		{
			count++;
			index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
		}
		return count;
	}

	private static void RequireJson(string path, string arrayName, string name, List<string> failures)
	{
		RequireJson(path, arrayName, name, "name", failures);
	}

	private static void RequireJson(string path, string arrayName, string name, string key, List<string> failures)
	{
		try
		{
			string text = File.ReadAllText(path);
			Require(text.Contains("\"" + name + "\""), Path.GetFileName(path) + " 里没有 " + name, failures);
			Require(text.Contains("\"" + arrayName + "\""), Path.GetFileName(path) + " 里没有 " + arrayName + " 数组", failures);
			Require(text.Contains("\"" + key + "\""), Path.GetFileName(path) + " 里没有 " + key + " 字段", failures);
		}
		catch (Exception exception)
		{
			failures.Add(Path.GetFileName(path) + ": " + exception.Message);
		}
	}

	private static void RequireJsonKey(string path, string key, List<string> failures)
	{
		try
		{
			Require(File.ReadAllText(path).Contains("\"" + key + "\""), Path.GetFileName(path) + " 里没有 " + key, failures);
		}
		catch (Exception exception)
		{
			failures.Add(Path.GetFileName(path) + ": " + exception.Message);
		}
	}

	private static void Require(bool condition, string message, List<string> failures)
	{
		if (!condition)
		{
			failures.Add(message);
		}
	}
}
