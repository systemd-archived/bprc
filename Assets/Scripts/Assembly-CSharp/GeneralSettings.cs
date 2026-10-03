using System;
using UnityEngine;

[Serializable]
public class GeneralSettings : SettingsBase
{
	private float m_gravityX;

	private float m_gravityY;

	private float m_timeScale;

	private bool m_fullScreen;

	private bool m_openVersionSelector;

	private bool m_rc1Controls;

	private InterfaceLanguage m_language;

	private bool m_showDebugPopup;

	private bool m_noGridMode;
	private bool m_mergeGridlessGroups;
	private bool m_ignoreGridlessOverlap = true;
	private bool m_multiLayerBuild;
	[Obsolete("BPIR layered saves are always recognized automatically.")]
	[Newtonsoft.Json.JsonIgnore]
	public bool AutoDetectMultiLayer
	{
		get => true;
		set { }
	}
	private bool m_layerSelfCollision;
	public bool MultiLayerBuild
	{
		get => m_multiLayerBuild;
		set
		{
			if (m_multiLayerBuild == value) return;
			m_multiLayerBuild = value; OnPropertyChanged(nameof(MultiLayerBuild));
			WPFMonoBehaviour.levelManager?.ContraptionProto?.RebuildLayerMap();
			ContraptionLayers.Select(1);
			WPFMonoBehaviour.levelManager?.ContraptionProto?.RefreshConnections();
		}
	}
	public bool LayerSelfCollision
	{
		get => m_layerSelfCollision;
		set
		{
			if (m_layerSelfCollision == value) return;
			m_layerSelfCollision = value; OnPropertyChanged(nameof(LayerSelfCollision));
			WPFMonoBehaviour.levelManager?.ContraptionRunning?.GetComponent<ContraptionLayerCollisions>()?.Refresh();
		}
	}
	private float m_uiScale = 1f;

	public float UiScale
	{
		get => m_uiScale;
		set
		{
			if (!float.IsFinite(value)) return;
			float scale = Mathf.Clamp(value, 0.5f, 1.5f);
			if (m_uiScale == scale) return;
			m_uiScale = scale;
			OnPropertyChanged(nameof(UiScale));
		}
	}

	private int m_fineRotationStep;

	private bool m_aeronauticsBlueprint;

	public bool AeronauticsBlueprint
	{
		get => m_aeronauticsBlueprint;
		set
		{
			if (m_aeronauticsBlueprint == value) return;
			m_aeronauticsBlueprint = value;
			OnPropertyChanged(nameof(AeronauticsBlueprint));
		}
	}

	/// <summary>
	/// 部件精细旋转: the step, in degrees, the build screen rotates a part by. <c>0</c> keeps the stock
	/// 45/90 degree increments.
	/// </summary>
	/// <remarks>
	/// An integer rather than the boolean Besiege uses: the reference exposes an adjustable snap angle
	/// (<c>StatMaster.Mode.Transform.Snap.rotation</c>, applied as <c>Round(value / snap) * snap</c>), so
	/// the useful setting is the step, and <c>0</c> is then the natural "off" - the build screen already
	/// has fixed increments to fall back to. Clamped to 1..90: below 1 is meaningless and above 90 the
	/// stock increments are finer anyway. See <see cref="FineRotate"/>.
	/// </remarks>
	public int FineRotationStep
	{
		get
		{
			return m_fineRotationStep;
		}
		set
		{
			int clamped = value < 0 ? 0 : (value > 90 ? 90 : value);
			if (m_fineRotationStep == clamped)
			{
				return;
			}
			m_fineRotationStep = clamped;
			OnPropertyChanged("FineRotationStep");
			// This changes subsequent rotation steps; saved angles keep their orientation.
			WPFMonoBehaviour.levelManager?.ContraptionProto?.RefreshPartPositions();
		}
	}

	/// <summary>
	/// 无网格模式: the build screen places parts off the lattice and snaps them to neighbouring parts
	/// instead of to grid cells.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Only the visual/physical position inside the cell changes; the integer cell coordinates the build
	/// model, the save format and the multiplayer snapshot are built on stay exactly as they were. See
	/// <see cref="GridlessBuild"/> for the mechanism and for what the mode deliberately does not cover.
	/// </para>
	/// <para>
	/// A plain boolean setting - the panel reflects every public property of this class, so the switch
	/// and its row appear in 通用设置 with no UI work beyond the locale entry. Flipping it re-lays out a
	/// contraption that is already on screen, so the change is visible immediately rather than on the
	/// next part placed.
	/// </para>
	/// </remarks>
	public bool NoGridMode
	{
		get
		{
			return m_noGridMode;
		}
		set
		{
			if (m_noGridMode == value)
			{
				return;
			}
			m_noGridMode = value;
			OnPropertyChanged("NoGridMode");
			// The placement mode changes the grid display without relocating a running machine.
			WPFMonoBehaviour.levelManager?.ContraptionProto?.RefreshPartPositions();
			GridlessBuild.NotifyModeChanged();
		}
	}

	/// <summary>Merge free-build groups at overlapping connection circles on a completed transform.</summary>
	public bool MergeGridlessGroups
	{
		get => m_mergeGridlessGroups;
		set
		{
			if (m_mergeGridlessGroups == value) return;
			m_mergeGridlessGroups = value;
			OnPropertyChanged(nameof(MergeGridlessGroups));
		}
	}

	public bool IgnoreGridlessOverlap
	{
		get => m_ignoreGridlessOverlap;
		set
		{
			if (m_ignoreGridlessOverlap == value) return;
			m_ignoreGridlessOverlap = value;
			OnPropertyChanged(nameof(IgnoreGridlessOverlap));
		}
	}

	/// <summary>
	/// The language the interface is read in.
	/// </summary>
	/// <remarks>
	/// Declared first so that it becomes the first row of 通用设置, which is where a language belongs. The
	/// row is a Heat UI horizontal selector - the pack's own control for a short list of choices - because
	/// <c>INSettingsInterface</c> builds one for any enum-valued setting, so this property plus
	/// <see cref="Apply"/> is the whole of the settings-side wiring.
	/// </remarks>
	public InterfaceLanguage Language
	{
		get
		{
			return m_language;
		}
		set
		{
			if (m_language != value)
			{
				m_language = value;
				OnPropertyChanged("Language");
			}
		}
	}

	public float GravityX
	{
		get
		{
			return m_gravityX;
		}
		set
		{
			if (m_gravityX != value && float.IsFinite(value))
			{
				m_gravityX = value;
				OnPropertyChanged("GravityX");
			}
		}
	}

	public float GravityY
	{
		get
		{
			return m_gravityY;
		}
		set
		{
			if (m_gravityY != value && float.IsFinite(value))
			{
				m_gravityY = value;
				OnPropertyChanged("GravityY");
			}
		}
	}

	public float TimeScale
	{
		get
		{
			return m_timeScale;
		}
		set
		{
			if (m_timeScale != value && float.IsFinite(value) && value >= 0f && value <= 10f)
			{
				m_timeScale = value;
				OnPropertyChanged("TimeScale");
			}
		}
	}

	public bool FullScreen
	{
		get
		{
			return m_fullScreen;
		}
		set
		{
			if (m_fullScreen != value)
			{
				m_fullScreen = value;
				OnPropertyChanged("FullScreen");
			}
		}
	}

	/// <summary>
	/// Whether the game offers the version selector at startup.
	/// </summary>
	/// <remarks>
	/// This is 通用设置's row for the mod flag <see cref="INFeature.OpenVersionSelector"/>, and it is a
	/// two-way proxy onto the feature's own value rather than a second copy of it: the getter reads
	/// <c>INSettings</c> live, so this row and the boot decision in <c>INInitializer</c> - which reads the
	/// same feature through <c>INSettings.PeekBool</c> before any version has been chosen - can never
	/// disagree. The value therefore still lives in INFeatureSettings.json, the per-version defaults in
	/// INSettings{B,A,O}Exp.json still apply, and the panel's reset path restores them through
	/// <c>INSettings.ResetUiSharedDefaults</c>, exactly as the other shared feature rows do.
	/// </remarks>
	public bool OpenVersionSelector
	{
		get
		{
			try
			{
				return INSettings.GetBool(INFeature.OpenVersionSelector);
			}
			catch
			{
				return m_openVersionSelector;
			}
		}
		set
		{
			bool current;
			try
			{
				current = INSettings.GetBool(INFeature.OpenVersionSelector);
			}
			catch
			{
				current = m_openVersionSelector;
			}
			m_openVersionSelector = value;
			if (current != value)
			{
				INSettings.SetValue(INFeature.OpenVersionSelector, new Variant<bool>(value));
				OnPropertyChanged("OpenVersionSelector");
			}
		}
	}

	/// <summary>
	/// Whether the RC1 driving controls are shown while a contraption is running.
	/// </summary>
	/// <remarks>
	/// 通用设置's row for the mod flag <see cref="INFeature.Rc1Controls"/>, written as the same two-way
	/// proxy as <see cref="OpenVersionSelector"/>: the getter reads <c>INSettings</c> live, so this row
	/// and <c>INRc1Controls</c> - which reads the feature directly while the game is running - can never
	/// disagree, and the value keeps living in INFeatureSettings.json with the per-version defaults.
	/// Off by default: switching it on adds an on-screen control set the stock game does not have.
	/// </remarks>
	public bool Rc1Controls
	{
		get
		{
			try
			{
				return INSettings.GetBool(INFeature.Rc1Controls);
			}
			catch
			{
				return m_rc1Controls;
			}
		}
		set
		{
			bool current;
			try
			{
				current = INSettings.GetBool(INFeature.Rc1Controls);
			}
			catch
			{
				current = m_rc1Controls;
			}
			m_rc1Controls = value;
			if (current != value)
			{
				INSettings.SetValue(INFeature.Rc1Controls, new Variant<bool>(value));
				OnPropertyChanged("Rc1Controls");
			}
		}
	}

	/// <summary>
	/// Whether the in-game debug console shows its floating popup window.
	/// </summary>
	/// <remarks>
	/// An ordinary boolean setting - 通用设置 renders it as a switch, the value is stored with the rest of
	/// the file, and <see cref="Apply"/> hands it to <see cref="INDebugConsole.PopupEnabled"/>, which is
	/// the window itself. Declared last so that its switch sits directly above the row that opens the
	/// console, and off by default: the floating button is a debugging aid, so it appears only in a
	/// session that asked for it.
	/// </remarks>
	public bool ShowDebugPopup
	{
		get
		{
			return m_showDebugPopup;
		}
		set
		{
			if (m_showDebugPopup != value)
			{
				m_showDebugPopup = value;
				INDebugConsole.PopupEnabled = value;
				OnPropertyChanged("ShowDebugPopup");
			}
		}
	}

	public GeneralSettings()
	{
		Language = InterfaceLanguage.FollowSystem;
		GravityX = 0f;
		GravityY = -9.81f;
		TimeScale = 1f;
		FullScreen = Screen.fullScreen;
	}

	public GeneralSettings(GeneralSettings settings)
		: this()
	{
		Update(settings);
	}

	public override void Apply()
	{
		// Before the rest: the language decides which text everything else is read in, and this runs while
		// the settings are loading, so the panels that are built afterwards build in the right one.
		INLocalization.SetLanguage(Language);
		Physics.gravity = new Vector2(GravityX, GravityY);
		if (!GameTime.IsPaused())
		{
			Time.timeScale = TimeScale;
		}
		INGameManager.Instance?.SetFullScreen(FullScreen);
		INDebugConsole.PopupEnabled = ShowDebugPopup;
		INUiScale.Refresh();
		INBlueprintPartEffect.SetEnabled(AeronauticsBlueprint);
	}

	public void Update(GeneralSettings settings)
	{
		if (settings != null)
		{
			Language = settings.Language;
			GravityX = settings.GravityX;
			GravityY = settings.GravityY;
			TimeScale = settings.TimeScale;
			FullScreen = settings.FullScreen;
			OpenVersionSelector = settings.OpenVersionSelector;
			Rc1Controls = settings.Rc1Controls;
			ShowDebugPopup = settings.ShowDebugPopup;
			NoGridMode = settings.NoGridMode;
			MergeGridlessGroups = settings.MergeGridlessGroups;
			IgnoreGridlessOverlap = settings.IgnoreGridlessOverlap;
			MultiLayerBuild = settings.MultiLayerBuild;
			LayerSelfCollision = settings.LayerSelfCollision;
			UiScale = settings.UiScale;
			FineRotationStep = settings.FineRotationStep;
			AeronauticsBlueprint = settings.AeronauticsBlueprint;
		}
	}
}
