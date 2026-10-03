using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using UnityEngine;

public class Localizer : Singleton<Localizer>
{
	public class LocaleParameters
	{
		public string translation = string.Empty;

		public float characterSizeFactor = 1f;

		public float lineSpacingFactor = 1f;
	}

	public string[] allowedLocales;

	private Dictionary<string, LocaleParameters> activeTranslations = new Dictionary<string, LocaleParameters>();

	private Dictionary<string, LocaleParameters> englishTranslations;

	private Font languageSpecificFont;

	private Font englishFont;

	private new static Localizer instance;

	private const string defaultFontName = "default";

	private bool localizationDataInitalized;

	private string currentLocale = string.Empty;

	public Font LanguageFont => languageSpecificFont;

	public Font EnglishFont => englishFont;

	public string CurrentLocale => currentLocale;

	public static XDocument LoadLocalizationFile()
	{
		return XDocument.Parse(Resources.Load<TextAsset>("Localization/localization_data").text);
	}

	private void Awake()
	{
		SetAsPersistant();
		currentLocale = DetectLocale();
		PopulateTranslations(currentLocale);
		localizationDataInitalized = true;

		// The settings can already have chosen a language: they load before this prefab exists, so a switch
		// aimed at this table would have found nothing to switch. Following the system is what the detection
		// above has just done, and a choice that agrees with the system needs nothing either.
		if (INUserSettings.Instance != null)
		{
			InterfaceLanguage chosen = INUserSettings.Instance.GeneralSettings.Language;
			if (chosen != InterfaceLanguage.FollowSystem && chosen != InterfaceLanguages.FromSystem())
			{
				SetLocale(InterfaceLanguages.ToLocaleId(chosen));
			}
		}
	}

	private string DetectLocale()
	{
		string text = "en-EN";
		if (Application.systemLanguage == SystemLanguage.Japanese)
		{
			text = "ja-JP";
		}
		SystemLanguage systemLanguage = Application.systemLanguage;
		switch (systemLanguage)
		{
		case SystemLanguage.Polish:
			text = "pl-PL";
			break;
		case SystemLanguage.Portuguese:
			text = "pt-PT";
			break;
		default:
			switch (systemLanguage)
			{
			case SystemLanguage.Arabic:
				text = "ar-AR";
				break;
			default:
				_ = 10;
				text = "en-EN";
				break;
			case SystemLanguage.Chinese:
			case SystemLanguage.ChineseSimplified:
			case SystemLanguage.ChineseTraditional:
				text = "zh-CN";
				break;
			case SystemLanguage.Japanese:
				text = "ja-JP";
				break;
			case SystemLanguage.Italian:
				text = "it-IT";
				break;
			case SystemLanguage.German:
				text = "de-DE";
				break;
			case SystemLanguage.French:
				text = "fr-FR";
				break;
			}
			break;
		case SystemLanguage.Russian:
			text = "ru-RU";
			break;
		case SystemLanguage.Spanish:
			text = "es-ES";
			break;
		}
		if (allowedLocales != null && allowedLocales.Length != 0)
		{
			string[] array = allowedLocales;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] == text)
				{
					return text;
				}
			}
			return "en-EN";
		}
		return text;
	}

	public Font GetFont(string localeName)
	{
		if (localeName == "en-EN")
		{
			return Singleton<Localizer>.Instance.EnglishFont;
		}
		return Singleton<Localizer>.Instance.LanguageFont;
	}

	public LocaleParameters Resolve(string textId, string localeName = null)
	{
		Dictionary<string, LocaleParameters> dictionary = ((!(localeName == "en-EN")) ? activeTranslations : englishTranslations);
		if (!dictionary.ContainsKey(textId) || dictionary[textId].translation == string.Empty)
		{
			return new LocaleParameters
			{
				translation = textId
			};
		}
		return dictionary[textId];
	}

	private bool LoadFont(string fontName)
	{
		if (fontName != "default")
		{
			languageSpecificFont = (Font)Resources.Load("Localization/Fonts/" + fontName, typeof(Font));
			return languageSpecificFont;
		}
		return true;
	}

	private void PopulateTranslations(string localeId)
	{
		bool flag = englishTranslations == null;
		if (flag)
		{
			englishTranslations = new Dictionary<string, LocaleParameters>();
		}
		XDocument xDocument = LoadLocalizationFile();
		XElement xElement = null;
		foreach (XElement item in xDocument.Element("texts").Elements("languages").Descendants())
		{
			if (item.Name == localeId)
			{
				xElement = item;
				break;
			}
		}
		if (xElement != null)
		{
			XElement xElement2 = xElement.Element("font");
			if (xElement2 != null)
			{
				LoadFont(xElement2.Value);
			}
		}
		if (englishFont == null)
		{
			XElement xElement3 = null;
			foreach (XElement item2 in xDocument.Element("texts").Elements("languages").Descendants())
			{
				if (item2.Name == "en-EN")
				{
					xElement3 = item2;
					break;
				}
			}
			if (xElement3 != null)
			{
				XElement xElement4 = xElement3.Element("font");
				if (xElement4 != null)
				{
					englishFont = (Font)Resources.Load("Localization/Fonts/" + xElement4.Value, typeof(Font));
				}
			}
		}
		foreach (XElement item3 in xDocument.Element("texts").Elements("text"))
		{
			string value = item3.Element("text_id").Value;
			XElement xElement5 = item3.Element(localeId);
			if (xElement5 == null)
			{
				xElement5 = item3.Element("en-EN");
			}
			if (!activeTranslations.ContainsKey(value))
			{
				activeTranslations.Add(value, FormulateTranslation(xElement5));
				if (flag)
				{
					xElement5 = item3.Element("en-EN");
					englishTranslations.Add(value, FormulateTranslation(xElement5));
				}
			}
		}
	}

	private LocaleParameters FormulateTranslation(XElement xn)
	{
		LocaleParameters localeParameters = new LocaleParameters();
		localeParameters.translation = xn.Value;
		XAttribute xAttribute = xn.Attribute("character_size");
		XAttribute xAttribute2 = xn.Attribute("line_spacing");
		if (xAttribute != null)
		{
			localeParameters.characterSizeFactor = float.Parse(xAttribute.Value, CultureInfo.InvariantCulture);
		}
		if (xAttribute2 != null)
		{
			localeParameters.lineSpacingFactor = float.Parse(xAttribute2.Value, CultureInfo.InvariantCulture);
		}
		return localeParameters;
	}

	private void OnApplicationPause(bool paused)
	{
		if (!paused && DetectLocale() != currentLocale)
		{
			RefreshLocalization();
		}
	}

	/// <summary>
	/// Switches the language this table resolves through, and tells everything showing its text to refresh.
	/// </summary>
	/// <remarks>
	/// The locale ids are the ones <see cref="DetectLocale"/> produces - "en-EN" for English, "zh-CN" for
	/// Chinese - and are not the same ids this project's keyed table uses. The per-locale font is dropped
	/// so the new one is loaded, and <see cref="LocalizationReloaded"/> is what the original game's labels
	/// and this project's panels listen for.
	/// </remarks>
	public void SetLocale(string localeId)
	{
		if (string.IsNullOrEmpty(localeId) || localeId == currentLocale)
		{
			return;
		}
		activeTranslations.Clear();
		languageSpecificFont = null;
		currentLocale = localeId;
		PopulateTranslations(currentLocale);
		EventManager.Send(new LocalizationReloaded(currentLocale));
	}

	/// <summary>
	/// Switches this table back to the language the system asks for, and tells everything to refresh.
	/// </summary>
	/// <remarks>
	/// This is the "follow the system" half of the language setting, and the same thing the game already
	/// does when it comes back from the background in another language. It reaches the locales this
	/// project's own table has no column for - French, German, Japanese and the rest.
	/// </remarks>
	public void RefreshLocalization()
	{
		SetLocale(DetectLocale());
	}
}
