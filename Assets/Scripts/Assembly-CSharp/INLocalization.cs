using System.Collections.Generic;
using Innovation;
using UnityEngine;

public class INLocalization
{
	public class LocalizationData
	{
		public List<LocalizationDataElement> Data { get; set; }

		public LocalizationData(List<LocalizationDataElement> data)
		{
			Data = data;
		}
	}

	public class LocalizationDataElement
	{
		public string ID { get; set; }

		public Dictionary<string, string> Text { get; set; }

		public LocalizationDataElement(string id, Dictionary<string, string> text)
		{
			ID = id;
			Text = text;
		}
	}

	private Dictionary<string, string> m_localization;

	private Dictionary<string, string> m_defaultLocalization;

	public static INLocalization Instance { get; private set; }

	public static void Create()
	{
		INLocalization iNLocalization = new INLocalization();
		iNLocalization.Initialize();
		Instance = iNLocalization;
	}

	/// <summary>
	/// Switches the language the interface is read in, everywhere the text for it comes from.
	/// </summary>
	/// <remarks>
	/// Two tables hold interface text and both have to move together: this project's keyed table, which
	/// every <see cref="UITextLocale"/> resolves through, and the game's own string table in
	/// <see cref="Localizer"/>, which the original game's labels use. The engine language is what tells
	/// both of them, and the cross-cutting logic that asks it directly - the option help's language, the
	/// quick menu's own layout - reads it too.
	/// <para>
	/// Following the system hands the game's own table back to its own detection, which reaches the five
	/// locales this project's table has no column for; the two named languages aim it at one of those.
	/// </para>
	/// <para>
	/// The game's own table announces the change with <see cref="LocalizationReloaded"/>, which is also
	/// what tells the panels to re-read their labels, so it is sent even when there is no
	/// <see cref="Localizer"/> in the scene to send it.
	/// </para>
	/// </remarks>
	public static void SetLanguage(InterfaceLanguage language)
	{
		SystemLanguage wanted = InterfaceLanguages.ToSystemLanguage(language);
		if (INUnity.Language != wanted || Instance == null)
		{
			INUnity.SetLanguage(wanted);
			Create();
		}

		Localizer localizer = Singleton<Localizer>.Instance;
		if (language == InterfaceLanguage.FollowSystem)
		{
			if (localizer != null)
			{
				localizer.RefreshLocalization();
				return;
			}
			EventManager.Send(new LocalizationReloaded(
				InterfaceLanguages.ToLocaleId(InterfaceLanguages.FromSystem())));
			return;
		}

		string locale = InterfaceLanguages.ToLocaleId(language);
		if (localizer != null)
		{
			// Repopulating the game's table sends the event that tells everything showing its text.
			localizer.SetLocale(locale);
			return;
		}
		EventManager.Send(new LocalizationReloaded(locale));
	}

	private void Initialize()
	{
		LocalizationData localizationData = Json.Deserialize<LocalizationData>(INUnity.LoadTextAsset("INLocalizationData").text);
		string key = ((INUnity.Language == SystemLanguage.Chinese) ? "zh-Hans" : "en-US");
		int count = localizationData.Data.Count;
		m_localization = new Dictionary<string, string>(count);
		m_defaultLocalization = new Dictionary<string, string>(count);
		foreach (LocalizationDataElement datum in localizationData.Data)
		{
			string value = datum.Text[key];
			string value2 = datum.Text["en-US"];
			m_localization.Add(datum.ID, value);
			m_defaultLocalization.Add(datum.ID, value2);
		}
	}

	public string GetText(string id)
	{
		return Lookup(m_localization, id);
	}

	public string GetDefaultText(string id)
	{
		return Lookup(m_defaultLocalization, id);
	}

	private readonly HashSet<string> m_missingReported = new HashSet<string>();

	/// <summary>
	/// A missing id used to escape as a <see cref="KeyNotFoundException"/> from the dictionary indexer,
	/// and because the settings interface resolves one label per generated item, a single gap aborted
	/// the whole panel - everything after the missing key was never created. That is how a new setting
	/// with no locale entry turned into a half-built settings screen. Report each gap once and fall back
	/// to the id, so the panel stays intact and the missing entry is visible rather than fatal.
	/// </summary>
	private string Lookup(Dictionary<string, string> table, string id)
	{
		string text;
		if (table != null && table.TryGetValue(id, out text))
		{
			return text;
		}
		if (m_missingReported.Add(id))
		{
			UnityEngine.Debug.LogWarning("[INLocalization] no " +
				(object.ReferenceEquals(table, m_defaultLocalization) ? "en-US" : "localized") +
				" text for '" + id + "'; showing the id instead.");
		}
		return id;
	}
}
