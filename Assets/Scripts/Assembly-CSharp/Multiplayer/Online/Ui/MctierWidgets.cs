using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BadPiggies.Multiplayer.Online.Ui
{
	/// <summary>
	/// Builds the lobby out of plain uGUI + TextMeshPro at runtime, styled by
	/// <see cref="MctierTheme"/>. There are no prefab dependencies, so the UI cannot break because
	/// an asset moved, and it can be constructed in edit mode by the editor audit.
	///
	/// Everything the lobby UI shows is created through here; the child names used by callers
	/// (<c>Dot</c>, <c>Name</c>, <c>Detail</c>, <c>Action</c>, <c>Label</c>) are part of the contract.
	/// </summary>
	public static class MctierWidgets
	{
		public const string LabelChildName = "Label";
		public const string DotChildName = "Dot";
		public const string NameChildName = "Name";
		public const string DetailChildName = "Detail";
		public const string ActionChildName = "Action";

		/// <summary>Heat UI icons staged under Resources/HeatUI/Icons, plus our own additions.</summary>
		private static readonly string[] IconNames =
		{
			"Home", "PlayCircle", "SearchGames", "Multiplayer", "Chapters", "Settings",
			"Audio", "Controls", "Extras", "Information", "Load", "Restart", "Visuals"
		};

		// ---------------------------------------------------------------- structure

		/// <summary>Creates the overlay canvas, and an EventSystem if the scene has none.</summary>
		public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
		{
			GameObject host = new GameObject(name, typeof(RectTransform));
			if (parent != null)
			{
				host.transform.SetParent(parent, false);
			}
			RectTransform rect = (RectTransform)host.transform;
			Stretch(rect);

			Canvas canvas = host.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = sortOrder;
			canvas.pixelPerfect = false;

			CanvasScaler scaler = host.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1440f, 900f);
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
			scaler.matchWidthOrHeight = 0.5f;
			scaler.referencePixelsPerUnit = 100f;

			host.AddComponent<GraphicRaycaster>();
			EnsureEventSystem();
			return canvas;
		}

		/// <summary>Guarantees something can route pointer events to the UI.</summary>
		public static void EnsureEventSystem()
		{
			if (EventSystem.current != null)
			{
				return;
			}
			EventSystem existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
			if (existing != null)
			{
				return;
			}
			GameObject host = new GameObject("[Online UI Events]");
			host.AddComponent<EventSystem>();
			host.AddComponent<StandaloneInputModule>();
		}

		public static RectTransform CreateNode(string name, Transform parent)
		{
			GameObject host = new GameObject(name, typeof(RectTransform));
			if (parent != null)
			{
				host.transform.SetParent(parent, false);
			}
			RectTransform rect = (RectTransform)host.transform;
			rect.localScale = Vector3.one;
			rect.anchoredPosition3D = Vector3.zero;
			return rect;
		}

		public static void Stretch(RectTransform rect, float padding = 0f)
		{
			Stretch(rect, padding, padding, padding, padding);
		}

		public static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
		{
			if (rect == null)
			{
				return;
			}
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.offsetMin = new Vector2(left, bottom);
			rect.offsetMax = new Vector2(-right, -top);
		}

		public static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
		{
			if (rect == null)
			{
				return;
			}
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.offsetMin = offsetMin;
			rect.offsetMax = offsetMax;
		}

		public static LayoutElement EnsureLayoutElement(GameObject target)
		{
			if (target == null)
			{
				return null;
			}
			LayoutElement element = target.GetComponent<LayoutElement>();
			if (element == null)
			{
				element = target.AddComponent<LayoutElement>();
			}
			return element;
		}

		/// <summary>Removes every child, working in edit mode as well as at runtime.</summary>
		public static void DestroyChildren(Transform parent)
		{
			if (parent == null)
			{
				return;
			}
			for (int i = parent.childCount - 1; i >= 0; i--)
			{
				DestroyObject(parent.GetChild(i).gameObject);
			}
		}

		public static void DestroyObject(UnityEngine.Object target)
		{
			if (target == null)
			{
				return;
			}
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(target);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(target);
			}
		}

		public static void SetActive(Component component, bool active)
		{
			if (component != null && component.gameObject.activeSelf != active)
			{
				component.gameObject.SetActive(active);
			}
		}

		// ---------------------------------------------------------------- surfaces

		public static Image CreateImage(string name, Transform parent, Color color, UnityEngine.Sprite sprite = null)
		{
			RectTransform rect = CreateNode(name, parent);
			Image image = rect.gameObject.AddComponent<Image>();
			image.color = color;
			if (sprite != null)
			{
				image.sprite = sprite;
				image.type = Image.Type.Simple;
			}
			image.raycastTarget = true;
			return image;
		}

		public static Image CreatePanel(string name, Transform parent, Color color, int cornerRadius = 14)
		{
			Image image = CreateImage(name, parent, color);
			// Division style uses nearly square glass panes. The radius is clamped so existing
			// callers can keep their old values without bringing back large rounded cards. The glass
			// sprite is generated at runtime, so this path has no dependency on preview artwork.
			image.sprite = null;
			image.type = Image.Type.Sliced;
			return image;
		}

		/// <summary>
		/// Draws a real outline on a surface: a hollow 9-slice sprite stretched over the target, since
		/// a second filled rect would only tint the fill.
		///
		/// Heat UI's outline textures carry a 25px 9-slice border, which does not fit a 36px-tall
		/// field — Unity then degenerates the slices and paints a solid white block. So the large
		/// surfaces (cards, panels, the window) use Heat UI's texture and everything smaller falls
		/// back to a generated outline whose border fits.
		/// </summary>
		public static Image AddOutline(Image target, int cornerRadius, Color color, int thickness = 1,
			bool useHeatFrame = false)
		{
			if (target == null || cornerRadius <= 0)
			{
				return null;
			}
			UnityEngine.Sprite sprite = useHeatFrame
				? HeatFrame(thickness)
				: MctierTheme.RoundedRectOutline(cornerRadius, thickness);
			Image outline = CreateImage("Outline", target.transform, color, sprite);
			outline.type = Image.Type.Sliced;
			outline.raycastTarget = false;
			Stretch((RectTransform)outline.transform);
			// Above the parent's own fill, below whatever content is added afterwards.
			outline.transform.SetAsFirstSibling();
			return outline;
		}

		/// <summary>A glass card: translucent surface plus a real 1px outline.</summary>
		public static Image CreateCard(string name, Transform parent, int cornerRadius = 14, bool withBorder = true)
		{
			Image image = CreatePanel(name, parent, MctierTheme.CardBg, cornerRadius);
			if (withBorder && cornerRadius > 0)
			{
				AddCornerMarks(image.transform);
			}
			return image;
		}

        // DivisionInventoryContent::Card uses two 3px corner markers, never a boxed perimeter.
        private static void AddCornerMarks(Transform parent)
        {
            Image tl = CreateImage("CornerTopLeft", parent, MctierTheme.CardBorder);
            SetAnchors((RectTransform)tl.transform, new Vector2(0f,1f), new Vector2(0f,1f), new Vector2(0f,-3f), new Vector2(3f,0f));
            Image br = CreateImage("CornerBottomRight", parent, MctierTheme.CardBorder);
            SetAnchors((RectTransform)br.transform, new Vector2(1f,0f), new Vector2(1f,0f), new Vector2(-3f,0f), new Vector2(0f,3f));
            tl.raycastTarget = br.raycastTarget = false;
        }

		public static Image CreateSeparator(string name, Transform parent, float thickness = 1f)
		{
			Image image = CreateImage(name, parent, MctierTheme.Separator);
			image.raycastTarget = false;
			RectTransform rect = (RectTransform)image.transform;
			// Outside a layout group the default rect is 100x100, which turns a separator into a slab.
			rect.sizeDelta = new Vector2(rect.sizeDelta.x, thickness);
			LayoutElement element = EnsureLayoutElement(image.gameObject);
			element.minHeight = thickness;
			element.preferredHeight = thickness;
			element.flexibleHeight = 0f;
			return image;
		}

		/// <summary>A round status dot. Usable inside a layout group, or anchored by hand.</summary>
		public static Image CreateDot(string name, Transform parent, Color color, float size = 10f)
		{
			Image image = CreateImage(name, parent, color, HeatCircle());
			// The rect has to be sized here too: a LayoutElement only applies inside a layout group,
			// so on its own the dot kept the default 100x100 and rendered as a huge circle.
			RectTransform rect = (RectTransform)image.transform;
			rect.sizeDelta = new Vector2(size, size);
			LayoutElement element = EnsureLayoutElement(image.gameObject);
			element.minWidth = size;
			element.minHeight = size;
			element.preferredWidth = size;
			element.preferredHeight = size;
			element.flexibleWidth = 0f;
			element.flexibleHeight = 0f;
			return image;
		}

		// ---------------------------------------------------------------- text

		public static TMP_Text CreateLabel(string name, Transform parent, string text, float fontSize, Color color,
			TextAlignmentOptions alignment = TextAlignmentOptions.Left)
		{
			RectTransform rect = CreateNode(name, parent);
			TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
			ApplyFont(label);
			label.text = text ?? "";
			label.fontSize = fontSize;
			label.color = color;
			label.alignment = alignment;
			// Heat UI uses an airy geometric face for every label, not only headings. A small
			// tracking value keeps Chinese and Latin copy visually aligned with its prefabs.
			label.characterSpacing = 0.4f;
			label.enableWordWrapping = false;
			label.overflowMode = TextOverflowModes.Ellipsis;
			label.raycastTarget = false;
			return label;
		}

		public static TMP_Text CreateHeading(string name, Transform parent, string text, float fontSize = 30f)
		{
			TMP_Text label = CreateLabel(name, parent, text, fontSize, MctierTheme.TextPrimary, TextAlignmentOptions.Left);
			label.fontStyle = FontStyles.Bold;
			// Heat UI's headers are letter-spaced; that airiness is a big part of its look.
			label.characterSpacing = 1.5f;
			return label;
		}

		/// <summary>A compact framed glyph button used for close and toolbar actions.</summary>
		public static UnityEngine.UI.Button CreateCircleButton(string name, Transform parent, string glyph,
			Action onClick, float size = 34f)
		{
			UnityEngine.UI.Button button = CreateButtonFallback(name, parent, glyph,
				MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.08f), MctierTheme.TextPrimary,
				onClick, 16f, 2);
			RectTransform rect = (RectTransform)button.transform;
			rect.sizeDelta = new Vector2(size, size);
			return button;
		}

		public static TMP_Text CreateCaption(string name, Transform parent, string text, float fontSize = 16f)
		{
			return CreateLabel(name, parent, text, fontSize, MctierTheme.TextSecondary, TextAlignmentOptions.Left);
		}

		public static void SetText(TMP_Text label, string text)
		{
			if (label != null)
			{
				label.text = text ?? "";
			}
		}

		public static void SetLabelColor(TMP_Text label, Color color)
		{
			if (label != null)
			{
				label.color = color;
			}
		}

		private static void ApplyFont(TMP_Text label)
		{
			TMP_FontAsset font = MctierTheme.Font;
			if (font != null)
			{
				label.font = font;
			}
		}

		// ---------------------------------------------------------------- buttons

		public static UnityEngine.UI.Button CreateButton(string name, Transform parent, string label, Color background, Color foreground,
			Action onClick, float fontSize = 20f, int cornerRadius = 10)
		{
			return TryCreateHeatButton(name, parent, label, onClick, fontSize, "Button", background, foreground)
                ?? CreateButtonFallback(name, parent, label, background, foreground, onClick, fontSize, cornerRadius);
		}

		private static UnityEngine.UI.Button TryCreateHeatButton(string name, Transform parent, string label,
			Action onClick, float fontSize, string templateName, Color background, Color foreground)
		{
			GameObject template = HeatUiSkin.LoadTemplate(templateName);
			if (template == null || parent == null)
			{
				return null;
			}
			GameObject instance = null;
			try
			{
				instance = UnityEngine.Object.Instantiate(template, parent, false);
				instance.name = name;
				Michsky.UI.Heat.ButtonManager manager = instance.GetComponentInChildren<Michsky.UI.Heat.ButtonManager>(true);
				if (manager == null)
				{
					DestroyObject(instance);
					return null;
				}
				manager.useLocalization = false;
				manager.useCustomContent = false;
                manager.enableIcon = false;
                manager.useSounds = false;
                manager.onClick.RemoveAllListeners();
				manager.buttonText = label ?? "";
				manager.textSize = fontSize;
				manager.autoFitContent = false;
				if (manager.normalImageObj != null) { manager.normalImageObj.color = background; }
				if (manager.highlightImageObj != null) { manager.highlightImageObj.color = Color.Lerp(background, Color.white, 0.12f); }
				if (manager.disabledImageObj != null) { manager.disabledImageObj.color = MctierTheme.WithAlpha(background, 0.45f); }
				if (manager.normalTextObj != null) { manager.normalTextObj.color = foreground; }
				if (manager.highlightTextObj != null) { manager.highlightTextObj.color = foreground; }
				if (manager.disabledTextObj != null) { manager.disabledTextObj.color = MctierTheme.WithAlpha(foreground, 0.45f); }
				if (manager.normalTextObj != null) { manager.normalTextObj.gameObject.name = LabelChildName; }
				manager.UpdateUI();
                foreach (Michsky.UI.Heat.UIManagerText tint in instance.GetComponentsInChildren<Michsky.UI.Heat.UIManagerText>(true))
                { tint.useCustomFont = true; tint.useCustomColor = true; tint.enabled = false; }
                foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true))
                {
                    ApplyFont(text);
                    text.fontSize = fontSize;
                    text.fontStyle = FontStyles.Normal;
                    text.characterSpacing = 1f;
                    text.enableWordWrapping = false;
                    text.color = foreground;
                }
                foreach (HorizontalLayoutGroup layout in instance.GetComponentsInChildren<HorizontalLayoutGroup>(true))
                    layout.padding = new RectOffset(8, 8, 0, 0);
                ApplyHeatButtonStateColor(manager, "Normal", background);
				ApplyHeatButtonStateColor(manager, "Highlight", MctierTheme.Accent);
                if (manager.highlightTextObj != null) manager.highlightTextObj.color = MctierTheme.OnAccent;
				ApplyHeatButtonStateColor(manager, "Disabled", MctierTheme.WithAlpha(background, 0.45f));
				UnityEngine.UI.Button button = instance.GetComponent<UnityEngine.UI.Button>();
				if (button == null)
				{
					button = instance.AddComponent<UnityEngine.UI.Button>();
				}
				Image proxy = instance.GetComponent<Image>();
				if (proxy == null)
				{
					proxy = instance.AddComponent<Image>();
				}
				proxy.color = Color.clear;
				proxy.raycastTarget = true;
				button.targetGraphic = proxy;
				button.transition = Selectable.Transition.None;
                OnlineHeatButtonBinding binding = instance.AddComponent<OnlineHeatButtonBinding>();
                binding.Initialize(button, manager);
				if (onClick != null)
				{
					button.onClick.AddListener(delegate { onClick(); });
				}
				return button;
			}
			catch (Exception exception)
			{
				Debug.LogWarning("Heat UI button template '" + templateName + "' could not be used: " + exception.Message);
				if (instance != null)
				{
					DestroyObject(instance);
				}
				return null;
			}
		}

		/// <summary>Stops Heat UI's UIManagerImage from replacing lobby colors with its white defaults.</summary>
		public static void ApplyHeatButtonStateColor(Michsky.UI.Heat.ButtonManager manager, string stateName, Color color)
		{
			if (manager == null)
			{
				return;
			}
			Transform state = manager.transform.Find(stateName);
			if (state == null)
			{
				return;
			}
			Image[] images = state.GetComponentsInChildren<Image>(true);
			for (int i = 0; i < images.Length; i++)
			{
				if (images[i] == null || images[i].name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0 || images[i] == manager.normalImageObj || images[i] == manager.highlightImageObj || images[i] == manager.disabledImageObj)
				{
					continue;
				}
				Michsky.UI.Heat.UIManagerImage tint = images[i].GetComponent<Michsky.UI.Heat.UIManagerImage>();
				if (tint != null)
				{
					tint.useCustomColor = true;
				}
				images[i].color = color;
			}
		}

		private static UnityEngine.UI.Button CreateButtonFallback(string name, Transform parent, string label, Color background, Color foreground,
			Action onClick, float fontSize = 20f, int cornerRadius = 10)
		{
			RectTransform rect = CreateNode(name, parent);
			Image image = rect.gameObject.AddComponent<Image>();
			image.color = background;
			if (cornerRadius > 0)
			{
				// Buttons use the same near-square glass surface as cards. Keep the sprite generated at
				// runtime so its alpha is stable even when a Heat UI prefab is not available.
				image.sprite = null;
				image.type = Image.Type.Sliced;
			}

			UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
			button.targetGraphic = image;
			button.transition = Selectable.Transition.ColorTint;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
			colors.pressedColor = new Color(0.82f, 0.88f, 0.90f, 1f);
			colors.selectedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
			colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
			colors.colorMultiplier = 1f;
			colors.fadeDuration = 0.08f;
			button.colors = colors;
			if (onClick != null)
			{
				button.onClick.AddListener(delegate
				{
					onClick();
				});
			}

			TMP_Text caption = CreateLabel(LabelChildName, rect, label, fontSize, foreground, TextAlignmentOptions.Center);
			caption.characterSpacing = 3f;
			caption.overflowMode = TextOverflowModes.Ellipsis;
			Stretch((RectTransform)caption.transform, 8f, 2f, 8f, 2f);

			// Every glass control gets a visible frame. This also prevents a translucent button from
			// disappearing over a bright MainMenu background.
			if (cornerRadius > 0)
			{
				AddOutline(image, Mathf.Min(cornerRadius, 2), MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.14f));
			}
			return button;
		}

		public static UnityEngine.UI.Button CreatePrimaryButton(string name, Transform parent, string label, Action onClick, float fontSize = 20f)
		{
			return CreateButton(name, parent, label, MctierTheme.TextPrimary, MctierTheme.OnAccent, onClick, fontSize, 2);
		}

		public static UnityEngine.UI.Button CreateGhostButton(string name, Transform parent, string label, Action onClick, float fontSize = 18f)
		{
			return TryCreateHeatPanelButton(name, parent, label, onClick, fontSize)
                ?? CreateButtonFallback(name, parent, label, Color.clear, MctierTheme.TextPrimary, onClick, fontSize, 2);
		}

        private static UnityEngine.UI.Button TryCreateHeatPanelButton(string name, Transform parent, string label, Action onClick, float fontSize)
        {
            GameObject template = HeatUiSkin.LoadTemplate("Button (Panel Alt)");
            if (template == null) return null;
            GameObject instance = UnityEngine.Object.Instantiate(template, parent, false);
            instance.name = name;
            Michsky.UI.Heat.PanelButton panel = instance.GetComponent<Michsky.UI.Heat.PanelButton>();
            if (panel == null) { DestroyObject(instance); return null; }
            panel.useLocalization = false;
            panel.useSounds = false;
            panel.useCustomText = false;
            panel.buttonText = label;
            panel.buttonIcon = null;
            panel.onClick.RemoveAllListeners();
            panel.UpdateUI();
            panel.useSeperator = false;
            foreach (LayoutGroup layout in instance.GetComponentsInChildren<LayoutGroup>(true)) layout.enabled = false;
            foreach (ContentSizeFitter fitter in instance.GetComponentsInChildren<ContentSizeFitter>(true)) fitter.enabled = false;
            foreach (RectTransform rect in instance.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "Static" || rect.name == "Normal" || rect.name == "Highlight" || rect.name == "Selected" || rect.name == "Disabled") Stretch(rect);
            foreach (Michsky.UI.Heat.UIManagerText tint in instance.GetComponentsInChildren<Michsky.UI.Heat.UIManagerText>(true)) { tint.useCustomFont = true; tint.useCustomColor = true; tint.enabled = false; }
            foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                ApplyFont(text);
                text.text = label;
                text.fontSize = fontSize;
                text.fontStyle = FontStyles.Normal;
                text.characterSpacing = 2f;
                text.gameObject.name = LabelChildName;
                Stretch((RectTransform)text.transform, 18f, 2f, 18f, 2f);
                text.alignment = TextAlignmentOptions.Center;
            }
            UnityEngine.UI.Button button = instance.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = instance.AddComponent<UnityEngine.UI.Button>();
            Image proxy = instance.GetComponent<Image>();
            if (proxy == null) proxy = instance.AddComponent<Image>();
            proxy.color = Color.clear;
            proxy.raycastTarget = true;
            button.targetGraphic = proxy;
            button.transition = Selectable.Transition.None;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            instance.AddComponent<OnlineHeatButtonBinding>().InitializePanel(button, panel);
            return button;
        }

		public static UnityEngine.UI.Button CreateDangerButton(string name, Transform parent, string label, Action onClick, float fontSize = 18f)
		{
			return CreateButton(name, parent, label, MctierTheme.WithAlpha(MctierTheme.Danger, 0.15f), MctierTheme.Danger, onClick, fontSize, 2);
		}

		public static UnityEngine.UI.Button CreateSmallButton(string name, Transform parent, string label, Color background, Color foreground,
			Action onClick, float fontSize = 15f)
		{
			UnityEngine.UI.Button button = CreateButton(name, parent, label, background, foreground, onClick, fontSize, 2);
			LayoutElement element = EnsureLayoutElement(button.gameObject);
			element.minHeight = 30f;
			element.preferredHeight = 30f;
			return button;
		}

		// ---------------------------------------------------------------- inputs

		public static TMP_InputField CreateInput(string name, Transform parent, string placeholder, float fontSize = 18f)
		{
			RectTransform rect = CreateNode(name, parent);
			Image background = rect.gameObject.AddComponent<Image>();
			// Heat UI-style field: a recessed fill, one step darker than the card it sits on, with a
			// visible 1px edge so an empty field still reads as a control.
			background.color = MctierTheme.FieldBg;
			background.sprite = null;
			background.type = Image.Type.Sliced;
			AddOutline(background, 1, MctierTheme.FieldBorder, 2);

			RectTransform viewport = CreateNode("Text Area", rect);
			Stretch(viewport, 12f, 6f, 12f, 6f);

			TMP_Text text = CreateLabel("Text", viewport, "", fontSize, MctierTheme.TextPrimary);
			text.enableWordWrapping = false;
			text.overflowMode = TextOverflowModes.Overflow;
			Stretch((RectTransform)text.transform);

			TMP_Text placeholderLabel = CreateLabel("Placeholder", viewport, placeholder, fontSize, MctierTheme.TextMuted);
			placeholderLabel.enableWordWrapping = false;
			Stretch((RectTransform)placeholderLabel.transform);

			TMP_InputField field = rect.gameObject.AddComponent<TMP_InputField>();
			field.targetGraphic = background;
			field.textViewport = viewport;
			field.textComponent = text;
			field.placeholder = placeholderLabel;
			field.lineType = TMP_InputField.LineType.SingleLine;
			field.richText = false;
			field.restoreOriginalTextOnEscape = true;
			field.caretWidth = 2;
			field.caretColor = MctierTheme.Accent;
			field.customCaretColor = true;
			field.selectionColor = MctierTheme.WithAlpha(MctierTheme.Accent, 0.35f);
			field.fontAsset = MctierTheme.Font;
			field.pointSize = fontSize;
			// Highlight on hover/focus the way Heat UI fields brighten.
			ColorBlock colors = field.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
			colors.selectedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
			colors.pressedColor = Color.white;
			colors.fadeDuration = 0.08f;
			field.colors = colors;
			LayoutElement element = EnsureLayoutElement(rect.gameObject);
			element.minHeight = 36f;
			element.preferredHeight = 36f;
			return field;
		}

		// ---------------------------------------------------------------- toggles

		public static Toggle CreateSwitch(string name, Transform parent, bool value, Action<bool> onChanged)
		{
			RectTransform rect = CreateNode(name, parent);
			// Matches Heat UI's rendered switch: a capsule track with a round white knob.
			Image track = CreateImage("Track", rect, Color.clear, MctierTheme.RoundedRect(12));
			track.type = Image.Type.Sliced;
			RectTransform trackRect = (RectTransform)track.transform;
			trackRect.anchorMin = new Vector2(0f, 0.5f);
			trackRect.anchorMax = new Vector2(0f, 0.5f);
			trackRect.pivot = new Vector2(0f, 0.5f);
			trackRect.sizeDelta = new Vector2(46f, 24f);
			trackRect.anchoredPosition = Vector2.zero;
			AddOutline(track, 12, MctierTheme.TextPrimary, 2);

			Image knob = CreateImage("Knob", trackRect, MctierTheme.TextPrimary, HeatCircle());
			RectTransform knobRect = (RectTransform)knob.transform;
			knobRect.anchorMin = new Vector2(0f, 0.5f);
			knobRect.anchorMax = new Vector2(0f, 0.5f);
			knobRect.pivot = new Vector2(0f, 0.5f);
			knobRect.sizeDelta = new Vector2(18f, 18f);

			Toggle toggle = rect.gameObject.AddComponent<Toggle>();
			toggle.targetGraphic = track;
			toggle.graphic = null; // A switch keeps its knob visible in both states.
			toggle.isOn = value;
			LayoutElement element = EnsureLayoutElement(rect.gameObject);
			element.minWidth = 48f;
			element.preferredWidth = 48f;
			element.minHeight = 26f;
			element.preferredHeight = 26f;
			element.flexibleWidth = 0f;

			Action<bool> visual = delegate(bool on)
			{
				knobRect.anchoredPosition = new Vector2(on ? 25f : 3f, 0f);
				knob.color = MctierTheme.TextPrimary;
				track.color = Color.clear;
			};
			toggle.onValueChanged.AddListener(delegate(bool on)
			{
				visual(on);
			});
			visual(value);
			if (onChanged != null)
			{
				toggle.onValueChanged.AddListener(delegate(bool on)
				{
					onChanged(on);
				});
			}
			return toggle;
		}

		public static Slider CreateSlider(string name, Transform parent, float min, float max, float value, Action<float> onChanged)
		{
			RectTransform rect = CreateNode(name, parent);

			Image background = CreateImage("Background", rect, MctierTheme.Separator);
			RectTransform backgroundRect = (RectTransform)background.transform;
			Stretch(backgroundRect, 0f, 8f, 0f, 8f);
			background.sprite = MctierTheme.RoundedRect(6);
			background.type = Image.Type.Sliced;

			RectTransform fillArea = CreateNode("Fill Area", rect);
			Stretch(fillArea, 0f, 8f, 0f, 8f);
			Image fill = CreateImage("Fill", fillArea, MctierTheme.Accent);
			RectTransform fillRect = (RectTransform)fill.transform;
			fillRect.anchorMin = Vector2.zero;
			fillRect.anchorMax = Vector2.one;
			fillRect.offsetMin = Vector2.zero;
			fillRect.offsetMax = Vector2.zero;
			fill.sprite = MctierTheme.RoundedRect(6);
			fill.type = Image.Type.Sliced;

			RectTransform handleArea = CreateNode("Handle Slide Area", rect);
			Stretch(handleArea, 0f, 0f, 0f, 0f);
			Image handle = CreateImage("Handle", handleArea, MctierTheme.TextPrimary, MctierTheme.Circle());
			RectTransform handleRect = (RectTransform)handle.transform;
			handleRect.sizeDelta = new Vector2(18f, 18f);

			Slider slider = rect.gameObject.AddComponent<Slider>();
			slider.fillRect = fillRect;
			slider.handleRect = handleRect;
			slider.targetGraphic = handle;
			slider.direction = Slider.Direction.LeftToRight;
			slider.minValue = min;
			slider.maxValue = max;
			slider.wholeNumbers = false;
			slider.value = value;
			LayoutElement element = EnsureLayoutElement(rect.gameObject);
			element.minHeight = 26f;
			element.preferredHeight = 26f;
			if (onChanged != null)
			{
				slider.onValueChanged.AddListener(delegate(float v)
				{
					onChanged(v);
				});
			}
			return slider;
		}

		// ---------------------------------------------------------------- scrolling and lists

		/// <summary>
		/// A masked, scrollable area. With <paramref name="layoutChildren"/> the content is driven by
		/// a vertical layout group (rows added in order); without it the content keeps a fixed height
		/// of <paramref name="contentHeight"/> and children are positioned by hand, which is what the
		/// form pages do.
		/// </summary>
		public static ScrollRect CreateScrollView(string name, Transform parent, out RectTransform content, bool vertical = true,
			bool layoutChildren = true, float contentHeight = 0f)
		{
			RectTransform rect = CreateNode(name, parent);
			Image background = rect.gameObject.AddComponent<Image>();
			background.color = new Color(0f, 0f, 0f, 0f);
			background.raycastTarget = true;

			RectTransform viewport = CreateNode("Viewport", rect);
			Stretch(viewport, 0f, 0f, 12f, 0f);
			viewport.gameObject.AddComponent<RectMask2D>();

			RectTransform contentRect = CreateNode("Content", viewport);
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0.5f, 1f);
			contentRect.offsetMin = Vector2.zero;
			contentRect.offsetMax = Vector2.zero;
			contentRect.anchoredPosition = Vector2.zero;
			if (layoutChildren)
			{
				VerticalLayoutGroup layout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
				layout.spacing = 8f;
				layout.padding = new RectOffset(2, 2, 2, 2);
				layout.childControlHeight = true;
				layout.childControlWidth = true;
				layout.childForceExpandHeight = false;
				layout.childForceExpandWidth = true;
				layout.childAlignment = TextAnchor.UpperLeft;
				ContentSizeFitter fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
				fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
				fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			}
			else
			{
				// Hand-positioned content: a layout group would overwrite every anchoredPosition.
				contentRect.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, 1f));
			}

			RectTransform scrollbarRect = CreateNode("Scrollbar", rect);
			scrollbarRect.anchorMin = new Vector2(1f, 0f);
			scrollbarRect.anchorMax = new Vector2(1f, 1f);
			scrollbarRect.pivot = new Vector2(1f, 0.5f);
			scrollbarRect.sizeDelta = new Vector2(6f, 0f);
			scrollbarRect.anchoredPosition = Vector2.zero;
			Image scrollbarBackground = scrollbarRect.gameObject.AddComponent<Image>();
			// Heat UI has no rounded track: thin flat bars in translucent white.
			scrollbarBackground.color = MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.06f);

			RectTransform slidingArea = CreateNode("Sliding Area", scrollbarRect);
			Stretch(slidingArea, 0f, 0f, 0f, 0f);
			Image handle = CreateImage("Handle", slidingArea, MctierTheme.WithAlpha(MctierTheme.TextPrimary, 0.28f));
			RectTransform handleRect = (RectTransform)handle.transform;
			Stretch(handleRect);

			Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
			scrollbar.direction = Scrollbar.Direction.BottomToTop;
			scrollbar.handleRect = handleRect;
			scrollbar.targetGraphic = handle;

			ScrollRect scrollRect = rect.gameObject.AddComponent<ScrollRect>();
			scrollRect.viewport = viewport;
			scrollRect.content = contentRect;
			scrollRect.horizontal = false;
			scrollRect.vertical = vertical;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			scrollRect.inertia = true;
			scrollRect.decelerationRate = 0.1f;
			scrollRect.scrollSensitivity = 32f;
			scrollRect.verticalScrollbar = scrollbar;
			scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
			scrollRect.verticalScrollbarSpacing = -3f;
			content = contentRect;
			return scrollRect;
		}

		public static RectTransform CreateVerticalList(string name, Transform parent, float spacing = 10f, RectOffset padding = null)
		{
			RectTransform rect = CreateNode(name, parent);
			VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
			layout.spacing = spacing;
			layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
			layout.childControlHeight = true;
			layout.childControlWidth = true;
			layout.childForceExpandHeight = false;
			layout.childForceExpandWidth = true;
			layout.childAlignment = TextAnchor.UpperLeft;
			return rect;
		}

		public static RectTransform CreateHorizontalRow(string name, Transform parent, float spacing = 10f, RectOffset padding = null)
		{
			RectTransform rect = CreateNode(name, parent);
			HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = spacing;
			layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
			layout.childControlHeight = true;
			layout.childControlWidth = true;
			layout.childForceExpandHeight = true;
			layout.childForceExpandWidth = false;
			layout.childAlignment = TextAnchor.MiddleLeft;
			return rect;
		}

		/// <summary>
		/// A card-style list row with the fixed child names callers rely on:
		/// <c>Dot</c>, <c>Name</c>, <c>Detail</c> and <c>Action</c> (an empty, inactive container).
		/// </summary>
		public static GameObject CreateListRow(string name, Transform parent, float height = 64f, int cornerRadius = 12)
		{
			Image card = CreateCard(name, parent, cornerRadius, false);
			RectTransform rect = (RectTransform)card.transform;
			LayoutElement rowElement = EnsureLayoutElement(card.gameObject);
			rowElement.minHeight = height;
			rowElement.preferredHeight = height;
			rowElement.flexibleHeight = 0f;

			HorizontalLayoutGroup layout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
			layout.spacing = 10f;
			layout.padding = new RectOffset(14, 10, 8, 8);
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = false;
			layout.childAlignment = TextAnchor.MiddleLeft;

			CreateDot(DotChildName, rect, MctierTheme.TextMuted, 10f);

			RectTransform texts = CreateVerticalList("Texts", rect, 2f);
			LayoutElement textsElement = EnsureLayoutElement(texts.gameObject);
			textsElement.flexibleWidth = 1f;
			textsElement.minWidth = 80f;
			VerticalLayoutGroup textsLayout = texts.GetComponent<VerticalLayoutGroup>();
			if (textsLayout != null)
			{
				textsLayout.childControlHeight = true;
				textsLayout.childForceExpandHeight = false;
			}

			TMP_Text nameLabel = CreateLabel(NameChildName, texts, "", 16f, MctierTheme.TextPrimary);
			nameLabel.enableWordWrapping = false;
			LayoutElement nameElement = EnsureLayoutElement(nameLabel.gameObject);
			nameElement.minHeight = 20f;
			nameElement.preferredHeight = 20f;

			TMP_Text detailLabel = CreateLabel(DetailChildName, texts, "", 13f, MctierTheme.TextMuted);
			detailLabel.enableWordWrapping = false;
			LayoutElement detailElement = EnsureLayoutElement(detailLabel.gameObject);
			detailElement.minHeight = 17f;
			detailElement.preferredHeight = 17f;

			RectTransform actions = CreateHorizontalRow(ActionChildName, rect, 6f);
			HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
			if (actionsLayout != null)
			{
				actionsLayout.childAlignment = TextAnchor.MiddleRight;
			}
			LayoutElement actionsElement = EnsureLayoutElement(actions.gameObject);
			actionsElement.flexibleWidth = 0f;
			return card.gameObject;
		}

		/// <summary>Adds a button into the row's <c>Action</c> container and returns it.</summary>
		public static UnityEngine.UI.Button CreateRowAction(GameObject row, string label, Color background, Color foreground,
			Action onClick, float width = 96f)
		{
			return CreateRowExtraButton(row, label, background, foreground, onClick, width, false);
		}

		/// <summary>
		/// Adds an extra button to the row's action container. Used for the per-row secondary
		/// actions (收藏 / 删除) that sit before the main action.
		/// </summary>
		public static UnityEngine.UI.Button CreateRowExtraButton(GameObject row, string label, Color background, Color foreground,
			Action onClick, float width = 96f, bool placeholder = false)
		{
			if (row == null)
			{
				return null;
			}
			Transform container = row.transform.Find(ActionChildName);
			if (container == null)
			{
				container = CreateHorizontalRow(ActionChildName, row.transform, 6f);
			}
			if (placeholder && container.childCount > 0)
			{
				// The row template ships an inactive Action slot; fill it the first time.
				return null;
			}
			UnityEngine.UI.Button button = CreateSmallButton("RowAction", container, label, background, foreground, onClick, 14f);
			LayoutElement element = EnsureLayoutElement(button.gameObject);
			element.minWidth = width;
			element.preferredWidth = width;
			element.flexibleWidth = 0f;
			container.gameObject.SetActive(true);
			return button;
		}

		// ---------------------------------------------------------------- Heat UI sprites

		/// <summary>Prefabs shipped in Resources/HeatUI; their images point at the Heat UI textures.</summary>
		private static readonly string[] HeatPrefabs =
		{
			"Button", "Close Button", "Button (Panel Alt)", "Input Field", "Dropdown",
			"Settings Element (Switch)", "Settings Element (Slider)",
			"Settings Element (Dropdown)", "Settings Element (Horizontal Selector)"
		};

		private static bool s_heatScanned;
		private static UnityEngine.Sprite s_heatOutline1;
		private static UnityEngine.Sprite s_heatOutline2;
		private static UnityEngine.Sprite s_heatCircle;
		private static UnityEngine.Sprite s_heatRing;
		private static UnityEngine.Sprite s_heatShadow;

		/// <summary>
		/// True when the Heat UI textures were found. When they are, panels use Heat UI's own
		/// "flat fill + hairline frame" look (square corners, its borders, its circles); when they
		/// are not, the generated rounded sprites keep the lobby usable.
		/// </summary>
		public static bool HeatSpritesAvailable
		{
			get
			{
				EnsureHeatSprites();
				return s_heatOutline1 != null;
			}
		}

		/// <summary>A panel fill: Heat UI draws flat quads, so this is null when its sprites exist.</summary>
		public static UnityEngine.Sprite HeatFill(int cornerRadius)
		{
			EnsureHeatSprites();
			return s_heatOutline1 != null ? null : MctierTheme.RoundedRect(cornerRadius);
		}

		/// <summary>A 1px (or 2px) hairline frame: Heat UI's "Flat Outline" textures.</summary>
		public static UnityEngine.Sprite HeatFrame(int thickness)
		{
			EnsureHeatSprites();
			if (s_heatOutline1 != null)
			{
				return thickness >= 2 && s_heatOutline2 != null ? s_heatOutline2 : s_heatOutline1;
			}
			return MctierTheme.RoundedRectOutline(12, thickness);
		}

		public static UnityEngine.Sprite HeatCircle()
		{
			EnsureHeatSprites();
			return s_heatCircle != null ? s_heatCircle : MctierTheme.Circle();
		}

		public static UnityEngine.Sprite HeatRing(int thickness)
		{
			EnsureHeatSprites();
			return s_heatRing != null ? s_heatRing : MctierTheme.Ring(thickness);
		}

		public static UnityEngine.Sprite HeatShadow()
		{
			EnsureHeatSprites();
			return s_heatShadow;
		}

		private static void EnsureHeatSprites()
		{
			if (s_heatScanned)
			{
				return;
			}
			s_heatScanned = true;
			foreach (string prefabName in HeatPrefabs)
			{
				if (s_heatOutline1 != null && s_heatOutline2 != null && s_heatCircle != null &&
					s_heatRing != null && s_heatShadow != null)
				{
					break;
				}
				GameObject prefab = Resources.Load<GameObject>("HeatUI/" + prefabName);
				if (prefab == null)
				{
					continue;
				}
				Image[] images = prefab.GetComponentsInChildren<Image>(true);
				for (int i = 0; i < images.Length; i++)
				{
					UnityEngine.Sprite sprite = images[i].sprite;
					if (sprite == null)
					{
						continue;
					}
					string name = sprite.name;
					if (s_heatOutline1 == null && name.IndexOf("Flat Outline - 1x", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						s_heatOutline1 = sprite;
					}
					else if (s_heatOutline2 == null && name.IndexOf("Flat Outline - 2x", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						s_heatOutline2 = sprite;
					}
					else if (s_heatCircle == null && name.IndexOf("Radial Filled", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						s_heatCircle = sprite;
					}
					else if (s_heatRing == null && name.IndexOf("Radial Outline", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						s_heatRing = sprite;
					}
					else if (s_heatShadow == null && name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						s_heatShadow = sprite;
					}
				}
			}
		}

		/// <summary>An icon from Resources/HeatUI/Icons, tinted and sized.</summary>
		public static Image CreateIcon(string name, Transform parent, float size, Color color)
		{
			UnityEngine.Sprite sprite = LoadIcon(name);
			Image image = CreateImage(name + "Icon", parent, sprite == null ? Color.clear : color, sprite);
			image.raycastTarget = false;
			if (sprite != null)
			{
				image.preserveAspect = true;
			}
			RectTransform rect = (RectTransform)image.transform;
			rect.sizeDelta = new Vector2(size, size);
			return image;
		}

		// ---------------------------------------------------------------- icons

		public static UnityEngine.Sprite LoadIcon(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}
			UnityEngine.Sprite icon = Resources.Load<UnityEngine.Sprite>("HeatUI/Icons/" + name);
			return icon;
		}

		/// <summary>The icon resources the lobby can use, for the audit and for menus.</summary>
		public static string[] DiscoverIcons()
		{
			List<string> found = new List<string>();
			for (int i = 0; i < IconNames.Length; i++)
			{
				if (LoadIcon(IconNames[i]) != null)
				{
					found.Add(IconNames[i]);
				}
			}
			return found.ToArray();
		}

		public static string[] AvailableIcons()
		{
			return (string[])IconNames.Clone();
		}

		/// <summary>Drops the generated sprites. Used by the editor audit between runs.</summary>
		public static void ClearCache()
		{
			MctierTheme.ClearCache();
		}

		public static List<string> DescribeHierarchy(Transform root, int maxDepth = 4)
		{
			List<string> lines = new List<string>();
			if (root != null)
			{
				Describe(root, 0, maxDepth, lines);
			}
			return lines;
		}

		private static void Describe(Transform node, int depth, int maxDepth, List<string> lines)
		{
			if (depth > maxDepth)
			{
				return;
			}
			lines.Add(new string(' ', depth * 2) + node.name);
			for (int i = 0; i < node.childCount; i++)
			{
				Describe(node.GetChild(i), depth + 1, maxDepth, lines);
			}
		}
	}
}
