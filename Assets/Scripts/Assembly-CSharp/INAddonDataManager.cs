using System;
using System.Collections.Generic;
using System.IO;
using Innovation;
using UnityEngine;

public class INAddonDataManager
{
	[Serializable]
	public struct INGadgetButtonEntry
	{
		public float time;

		public SortedPartType type;

		public int tier;

		public INGadgetButtonEntry(float time, BasePart.PartType type, int tier)
		{
			this.time = time;
			this.type = type.ToSortedPartType();
			this.tier = tier;
		}
	}

	[Serializable]
	public class INGadgetButtonData
	{
		public List<INGadgetButtonEntry> items;
	}

	public struct INPartEntry
	{
		public Vector2 position;

		public Vector2 velocity;

		public Quaternion rotation;

		public float angularVelocity;

		public INPartEntry(Vector2 position, Vector2 velocity, Quaternion rotation, float angularVelocity)
		{
			this.position = position;
			this.velocity = velocity;
			this.rotation = rotation;
			this.angularVelocity = angularVelocity;
		}

		public INPartEntry(Vector2 position, Vector2 velocity, Vector3 angle, float angularVelocity)
		{
			this.position = position;
			this.velocity = velocity;
			rotation = default(Quaternion);
			rotation.eulerAngles = angle;
			this.angularVelocity = angularVelocity;
		}
	}

	public struct INContraptionEntry
	{
		public float time;

		public INPartEntry[] parts;

		public float Time
		{
			get
			{
				return time;
			}
			set
			{
				time = value;
			}
		}

		public INContraptionEntry(int count)
		{
			parts = new INPartEntry[count];
			time = UnityEngine.Time.fixedTime - s_startTime;
		}

		public INPartEntry Get(int index)
		{
			return parts[index];
		}

		public void Set(int index, Vector2 position, Vector2 velocity, Quaternion rotation, float angularVelocity)
		{
			parts[index] = new INPartEntry(position, velocity, rotation, angularVelocity);
		}
	}

	public struct INContraptionDataValue
	{
		public List<INContraptionEntry> items;

		public INContraptionData Serialize()
		{
			INContraptionData iNContraptionData = new INContraptionData();
			iNContraptionData.frames = new List<INContraptionFrame>(items.Count);
			for (int i = 0; i < items.Count; i++)
			{
				iNContraptionData.frames.Add(new INContraptionFrame(items[i]));
			}
			return iNContraptionData;
		}
	}

	[Serializable]
	public class INContraptionFrame
	{
		public string time;

		public string[] parts;

		public INContraptionFrame(string time, string[] parts)
		{
			this.time = time;
			this.parts = parts;
		}

		public INContraptionFrame(INContraptionEntry entry)
		{
			time = entry.time.ToString();
			parts = new string[entry.parts.Length];
			for (int i = 0; i < entry.parts.Length; i++)
			{
				INPartEntry iNPartEntry = entry.parts[i];
				parts[i] = new string[4]
				{
					iNPartEntry.position.Vector2ToString(),
					iNPartEntry.velocity.Vector2ToString(),
					iNPartEntry.rotation.eulerAngles.Vector3ToString(),
					iNPartEntry.angularVelocity.ToString()
				}.ArrayToString();
			}
		}
	}

	[Serializable]
	public class INContraptionData
	{
		public List<INContraptionFrame> frames;

		public INContraptionDataValue Parse()
		{
			INContraptionDataValue result = default(INContraptionDataValue);
			result.items = new List<INContraptionEntry>(frames.Count);
			INContraptionEntry item = default(INContraptionEntry);
			for (int i = 0; i < frames.Count; i++)
			{
				item.time = float.Parse(frames[i].time);
				item.parts = new INPartEntry[frames[i].parts.Length];
				for (int j = 0; j < frames[i].parts.Length; j++)
				{
					string text = frames[i].parts[j];
					string[] array = text.Substring(1, text.Length - 2).Split(new string[2] { "; ", ";" }, StringSplitOptions.RemoveEmptyEntries);
					item.parts[j] = new INPartEntry(array[0].ToValue<Vector2>(), array[1].ToValue<Vector2>(), array[2].ToValue<Vector3>(), array[3].ToValue<float>());
				}
				result.items.Add(item);
			}
			return result;
		}
	}

	public static float s_lastTime;

	public static float s_startTime;

	public static string s_recordPath;

	public static INGadgetButtonData s_gadgetButtonData;

	public static INGadgetButtonData s_gadgetButtonDataRecord;

	public static string s_recordPathContraption;

	public static INContraptionDataValue s_contraptionData;

	public static INContraptionDataValue s_contraptionDataRecord;

	public static void LoadButtonData(string path)
	{
		s_gadgetButtonData = Json.Deserialize<INGadgetButtonData>(File.ReadAllText(path));
	}

	public static void LoadContraptionData(string path)
	{
		s_contraptionData = Json.Deserialize<INContraptionData>(File.ReadAllText(path)).Parse();
	}

	public static void RecordButtonData(string path)
	{
		s_recordPath = path;
		s_gadgetButtonDataRecord = new INGadgetButtonData();
		s_gadgetButtonDataRecord.items = new List<INGadgetButtonEntry>();
	}

	public static void RecordContraptionData(string path)
	{
		s_recordPathContraption = path;
		s_contraptionDataRecord = default(INContraptionDataValue);
		s_contraptionDataRecord.items = new List<INContraptionEntry>();
	}

	public static void Start()
	{
		s_startTime = Time.fixedTime;
	}

	public static void FixedUpdate()
	{
		if (s_contraptionDataRecord.items != null)
		{
			List<BasePart> parts = Contraption.Instance.Parts;
			INContraptionEntry item = new INContraptionEntry(parts.Count);
			for (int i = 0; i < parts.Count; i++)
			{
				Rigidbody rigidbody = parts[i].rigidbody;
				item.Set(i, rigidbody.position, rigidbody.velocity, rigidbody.rotation, rigidbody.angularVelocity.z);
			}
			s_contraptionDataRecord.items.Add(item);
		}
		if (s_contraptionData.items == null)
		{
			return;
		}
		List<BasePart> parts2 = Contraption.Instance.Parts;
		foreach (INContraptionEntry item2 in s_contraptionData.items)
		{
			float time = item2.Time;
			if (time > Time.fixedTime - s_startTime - Time.fixedDeltaTime * 0.5f && time < Time.fixedTime - s_startTime + Time.fixedDeltaTime * 0.5f)
			{
				for (int j = 0; j < item2.parts.Length; j++)
				{
					Rigidbody rigidbody2 = parts2[j].rigidbody;
					Vector3 position = rigidbody2.position;
					Vector3 velocity = rigidbody2.velocity;
					Vector3 angularVelocity = rigidbody2.angularVelocity;
					INPartEntry iNPartEntry = item2.Get(j);
					position.x = iNPartEntry.position.x;
					position.y = iNPartEntry.position.y;
					velocity.x = iNPartEntry.velocity.x;
					velocity.y = iNPartEntry.velocity.y;
					angularVelocity.z = iNPartEntry.angularVelocity;
					rigidbody2.position = position;
					rigidbody2.velocity = velocity;
					rigidbody2.rotation = iNPartEntry.rotation;
					rigidbody2.angularVelocity = angularVelocity;
				}
			}
		}
	}

	public static void OnDestroy()
	{
		if (!string.IsNullOrEmpty(s_recordPath))
		{
			SaveButtonDataRecord();
		}
		if (!string.IsNullOrEmpty(s_recordPathContraption))
		{
			SaveContraptionDataRecord();
		}
	}

	public static void RecordIndex(float time, BasePart.PartType type, int tier)
	{
		if (s_gadgetButtonDataRecord != null)
		{
			s_gadgetButtonDataRecord.items.Add(new INGadgetButtonEntry(time - s_startTime, type, tier));
		}
	}

	public static void SaveButtonDataRecord()
	{
		string value = Json.Serialize(s_gadgetButtonDataRecord);
		using (StreamWriter streamWriter = new StreamWriter(s_recordPath))
		{
			streamWriter.Write(value);
		}
		s_recordPath = null;
		s_gadgetButtonDataRecord = null;
	}

	public static void SaveContraptionDataRecord()
	{
		string value = Json.Serialize(s_contraptionDataRecord.Serialize());
		using (StreamWriter streamWriter = new StreamWriter(s_recordPathContraption))
		{
			streamWriter.Write(value);
		}
		s_recordPathContraption = null;
		s_contraptionDataRecord = default(INContraptionDataValue);
	}

	public static IEnumerable<(BasePart.PartType, int)> FindIndex(float time)
	{
		if (s_gadgetButtonData == null)
		{
			yield break;
		}
		float currentTime = time - s_startTime;
		float deltaTime = Time.fixedDeltaTime * 0.5f;
		foreach (INGadgetButtonEntry item in s_gadgetButtonData.items)
		{
			if (item.time >= s_lastTime + deltaTime && item.time < currentTime + deltaTime)
			{
				yield return (item.type.ToPartType(), item.tier);
			}
		}
		s_lastTime = currentTime;
	}
}
