using UnityEngine;

public class ShowOnce : MonoBehaviour
{
	public string key = string.Empty;

	private int showTimes = 1;

	private void Awake()
	{
		string text = "show_count_" + key;
		int num = GameProgress.GetInt(text);
		if (num >= showTimes)
		{
			Object.Destroy(base.gameObject);
		}
		else
		{
			GameProgress.SetInt(text, num + 1);
		}
	}
}
