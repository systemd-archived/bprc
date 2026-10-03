using Innovation;

internal class CoreService : ICoreService
{
	private bool m_feedback;

	private INCommandManager Manager => Singleton<INCommandManager>.Instance;

	private INCommandManager.ScriptWriter Writer => Manager?.Writer;

	public void Write(object arg)
	{
		Writer?.Write(arg);
	}

	public void Write(params object[] args)
	{
		Writer?.Write(string.Concat(args));
	}

	public void WriteLine(object arg)
	{
		Writer?.WriteLine(arg);
	}

	public void WriteLine(params object[] args)
	{
		Writer?.WriteLine(string.Concat(args));
	}

	public void Clear()
	{
		Manager?.Clear();
	}

	public void EnableFeedback()
	{
		m_feedback = true;
	}

	public void DisableFeedback()
	{
		m_feedback = false;
	}

	public void Feedback(object arg)
	{
		if (m_feedback)
		{
			WriteLine(arg);
		}
	}

	public void Feedback(params object[] args)
	{
		if (m_feedback)
		{
			WriteLine(args);
		}
	}

	public object GetSettingsValue(string name)
	{
		INSettings.GetSettingsValue(name, out var result);
		return result.BoxedValue;
	}

	public void SetSettingsValue(string name, object value)
	{
		INSettings.SetSettingsValue(name, value);
	}

	public void ResetSettings()
	{
		INSettings.ResetSettings();
	}

	public object GetUserSettingsValue(string name)
	{
		return INSettingsInterface.Instance.GetValue(name);
	}

	public void SetUserSettingsValue(string name, object value)
	{
		INSettingsInterface.Instance.SetValue(name, value);
	}

	public void ResetUserSettings()
	{
		INSettingsInterface.Instance.Reset();
	}

	public void SaveUserSettings()
	{
		INSettingsInterface.Instance.Save();
	}

	public GameState GetGameState()
	{
		LevelManager levelManager = WPFMonoBehaviour.levelManager;
		if (levelManager == null)
		{
			return GameState.Undefined;
		}
		return (GameState)levelManager.gameState;
	}
}
