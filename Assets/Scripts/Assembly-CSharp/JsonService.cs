using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Innovation;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

internal class JsonService : IJsonService
{
	private class Vector2Converter : JsonConverter<Vector2>
	{
		public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
			{
				throw new JsonSerializationException("Cannot convert null value to Vector2.");
			}
			if (reader.TokenType == JsonToken.String)
			{
				float[] array = ReadSingleArray((string)reader.Value);
				if (array.Length != 2)
				{
					throw new JsonSerializationException("Error parsing Vector2 string.");
				}
				return new Vector2(array[0], array[1]);
			}
			throw new JsonSerializationException("Unexpected token or value when parsing Vector2.");
		}

		public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
		{
			string value2 = WriteSingleArray(value.x, value.y);
			writer.WriteValue(value2);
		}
	}

	private class Vector3Converter : JsonConverter<Vector3>
	{
		public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
			{
				throw new JsonSerializationException("Cannot convert null value to Vector3.");
			}
			if (reader.TokenType == JsonToken.String)
			{
				float[] array = ReadSingleArray((string)reader.Value);
				if (array.Length != 3)
				{
					throw new JsonSerializationException("Error parsing Vector3 string.");
				}
				return new Vector3(array[0], array[1], array[2]);
			}
			throw new JsonSerializationException("Unexpected token or value when parsing Vector3.");
		}

		public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
		{
			string value2 = WriteSingleArray(value.x, value.y, value.z);
			writer.WriteValue(value2);
		}
	}

	private class Vector4Converter : JsonConverter<Vector4>
	{
		public override Vector4 ReadJson(JsonReader reader, Type objectType, Vector4 existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
			{
				throw new JsonSerializationException("Cannot convert null value to Quaternion.");
			}
			if (reader.TokenType == JsonToken.String)
			{
				float[] array = ReadSingleArray((string)reader.Value);
				if (array.Length != 4)
				{
					throw new JsonSerializationException("Error parsing Vector4 string.");
				}
				return new Vector4(array[0], array[1], array[2], array[3]);
			}
			throw new JsonSerializationException("Unexpected token or value when parsing Vector4.");
		}

		public override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer)
		{
			string value2 = WriteSingleArray(value.x, value.y, value.z, value.w);
			writer.WriteValue(value2);
		}
	}

	private class QuaternionConverter : JsonConverter<Quaternion>
	{
		public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
			{
				throw new JsonSerializationException("Cannot convert null value to Quaternion.");
			}
			if (reader.TokenType == JsonToken.String)
			{
				float[] array = ReadSingleArray((string)reader.Value);
				if (array.Length != 4)
				{
					throw new JsonSerializationException("Error parsing Vector4 string.");
				}
				return new Quaternion(array[0], array[1], array[2], array[3]);
			}
			throw new JsonSerializationException("Unexpected token or value when parsing Vector4.");
		}

		public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
		{
			string value2 = WriteSingleArray(value.x, value.y, value.z, value.w);
			writer.WriteValue(value2);
		}
	}

	private class ConverterContractResolver : DefaultContractResolver
	{
		private List<JsonConverter> m_converters;

		public ConverterContractResolver(List<JsonConverter> converters)
		{
			m_converters = converters;
		}

		protected override JsonContract CreateContract(Type objectType)
		{
			JsonContract jsonContract = base.CreateContract(objectType);
			foreach (JsonConverter converter in m_converters)
			{
				if (converter.CanConvert(objectType))
				{
					jsonContract.Converter = converter;
					break;
				}
			}
			return jsonContract;
		}
	}

	private List<JsonConverter> m_converters;

	private JsonSerializerSettings m_settings;

	public JsonService()
	{
		m_converters = new List<JsonConverter>
		{
			new StringEnumConverter(),
			new VersionConverter(),
			new Vector2Converter(),
			new Vector3Converter(),
			new Vector4Converter(),
			new QuaternionConverter()
		};
		m_settings = new JsonSerializerSettings
		{
			ContractResolver = new ConverterContractResolver(m_converters)
			{
				NamingStrategy = new CamelCaseNamingStrategy()
			}
		};
	}

	public string Serialize<T>(T value)
	{
		return Serialize(value, indented: true);
	}

	public string Serialize<T>(T value, bool indented)
	{
		StringWriter stringWriter = new StringWriter(new StringBuilder(256), CultureInfo.InvariantCulture);
		Serialize(stringWriter, value, indented);
		return stringWriter.ToString();
	}

	public void Serialize<T>(TextWriter writer, T value)
	{
		Serialize(writer, value, indented: true);
	}

	public void Serialize<T>(TextWriter writer, T value, bool indented)
	{
		JsonSerializer jsonSerializer = JsonSerializer.CreateDefault(m_settings);
		jsonSerializer.Formatting = (indented ? Formatting.Indented : Formatting.None);
		using JsonTextWriter jsonTextWriter = new JsonTextWriter(writer);
		jsonTextWriter.Indentation = 1;
		jsonTextWriter.IndentChar = '\t';
		jsonTextWriter.Formatting = jsonSerializer.Formatting;
		jsonSerializer.Serialize(jsonTextWriter, value, null);
	}

	public T Deserialize<T>(string text)
	{
		return Deserialize<T>(new StringReader(text));
	}

	public T Deserialize<T>(TextReader reader)
	{
		JsonSerializer jsonSerializer = JsonSerializer.CreateDefault(m_settings);
		using JsonTextReader reader2 = new JsonTextReader(reader);
		return (T)jsonSerializer.Deserialize(reader2, typeof(T));
	}

	private static float[] ReadSingleArray(string text)
	{
		string[] array = text.Split(',');
		float[] array2 = new float[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array2[i] = float.Parse(array[i]);
		}
		return array2;
	}

	private static string WriteSingleArray(params float[] values)
	{
		return string.Join(',', values);
	}
}
