using UnityEngine;

/// <summary>
/// The languages the interface can be read in.
/// </summary>
/// <remarks>
/// <para>
/// These are the two the project has text for: this project's keyed table carries "en-US" and "zh-Hans" for
/// every id, and the game's own string table carries "en-EN" and "zh-CN". The numbers are what the settings
/// file stores, so they are part of that file's format and must not be renumbered.
/// </para>
/// <para>
/// Following the system is the default and stays a choice rather than being folded into the other two,
/// because the game's own string table carries seven locales: a player whose system is French gets French
/// level text whatever the two columns this project's table has, and narrowing that to English just because
/// the settings were saved would be a regression.
/// </para>
/// </remarks>
public enum InterfaceLanguage
{
	FollowSystem = 0,
	English = 1,
	Chinese = 2
}

/// <summary>
/// The conversions between <see cref="InterfaceLanguage"/> and the several places a language is spelled:
/// the engine's <see cref="SystemLanguage"/>, the game's own locale ids, and this project's.
/// </summary>
/// <remarks>
/// There are two string tables in the game and they do not agree on how a locale is named - the game's
/// own <see cref="Localizer"/> calls English "en-EN", while <see cref="INLocalization"/> calls it "en-US".
/// Both are derived here so that a language switch cannot set one and leave the other behind.
/// </remarks>
public static class InterfaceLanguages
{
	/// <summary>The language the system asks for, narrowed to the two this interface can be read in.</summary>
	public static InterfaceLanguage FromSystem()
	{
		return ToInterface(Application.systemLanguage);
	}

	/// <summary>The interface language an engine language corresponds to.</summary>
	public static InterfaceLanguage ToInterface(SystemLanguage language)
	{
		if (language == SystemLanguage.Chinese || language == SystemLanguage.ChineseSimplified ||
			language == SystemLanguage.ChineseTraditional)
		{
			return InterfaceLanguage.Chinese;
		}
		return InterfaceLanguage.English;
	}

	/// <summary>The engine language a choice means, which is what the rest of the game reads from.</summary>
	public static SystemLanguage ToSystemLanguage(InterfaceLanguage language)
	{
		if (language == InterfaceLanguage.FollowSystem)
		{
			return ToSystemLanguage(FromSystem());
		}
		return language == InterfaceLanguage.Chinese ? SystemLanguage.Chinese : SystemLanguage.English;
	}

	/// <summary>The locale id the game's own string table resolves through.</summary>
	public static string ToLocaleId(InterfaceLanguage language)
	{
		return ToSystemLanguage(language) == SystemLanguage.Chinese ? "zh-CN" : "en-EN";
	}

	/// <summary>
	/// What a language picker shows for a choice.
	/// </summary>
	/// <remarks>
	/// Each language is named in its own language, the way a language list is normally read, so that the
	/// choice is legible whichever of the two is currently on - and so the picker itself never needs
	/// translating. The choice that hands the language back to the system has no language of its own, so it
	/// is named in the one that is on.
	/// </remarks>
	public static string Title(InterfaceLanguage language)
	{
		if (language == InterfaceLanguage.English) { return "English"; }
		if (language == InterfaceLanguage.Chinese) { return "中文"; }
		INLocalization localization = INLocalization.Instance;
		return localization != null ? localization.GetText("InterfaceLanguage_FollowSystem") : "System";
	}
}
