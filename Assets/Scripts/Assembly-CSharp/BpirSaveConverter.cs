using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>BPIR 2022.1.10 CSV, JSON and encrypted XML to BPRC's per-part parameter format.</summary>
public static class BpirSaveConverter
{
    public const string SourceKey = "@bpir20220110";
    /// <summary>BPIR Bind applies a nonzero rotZ over an initially identity game orientation.
    /// Convert that loaded absolute pose to BPRC's additive control after the native orientation is known.
    /// Marker 3 persists the correction so reload, clipboard and undo cannot subtract it again.</summary>
    public static void RestoreImportedRotation(BasePart part)
    {
        if (part == null || !INParamSystem.Enabled || !part.TryGetStoredParam(SourceKey, out float version) || version != 2f) return;
        // Fine-angle edits made in BPRC already describe an explicit visual pose.
        if (!part.HasCustomParam(FineRotate.AngleKey) && part.TryGetStoredParam(PartPhysicsParameters.RotationKey, out float sourceAngle) &&
            BuildMachineTransform.Finite(sourceAngle) && !Mathf.Approximately(sourceAngle, 0f))
        {
            PartRotationAnimator.Complete(part.transform);
            Quaternion native = Quaternion.AngleAxis(-BuildMachineTransform.Read(part).angle - sourceAngle, Vector3.forward) * part.transform.localRotation;
            part.SetStoredParam(PartPhysicsParameters.RotationKey, Mathf.DeltaAngle(native.eulerAngles.z, sourceAngle));
        }
        part.SetStoredParam(SourceKey, 3f);
        part.contraption?.RefreshPartPose(part);
        PartRotationAnimator.Complete(part.transform);
    }
    private sealed class Unit
    {
        public int type, index, x, y, rotation, layer = 1, gravity = -1, drag = -1;
        public bool flipped;
        public float mass = -1f, size = -1f, power = -1f, suction = -1f;
        public string members;
    }

    public sealed class Result
    {
        public INContraptionData data;
        public bool UsesMultipleLayers { get; internal set; }
        public readonly List<string> warnings = new List<string>();
        public string Summary => "已转换 BPIR 存档（" + data.Units.Count + " 个部件）" +
            (UsesMultipleLayers ? "，已识别多图层结构（自动按存档图层建造）" : string.Empty) +
            (warnings.Count == 0 ? string.Empty : "，" + warnings.Count + " 项兼容提示：" + string.Join("；", warnings));
    }

    public static bool TryConvert(byte[] bytes, bool requireExtendedFields, out Result result, out string error)
    {
        result = null; error = null;
        try
        {
            if (bytes == null || bytes.Length == 0) throw new FormatException("存档为空");
            string text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            List<Unit> units;
            bool extended;
            if (text.StartsWith("{")) units = ReadJson(text, out extended);
            else if (text.StartsWith("<")) units = ReadXml(bytes, out extended);
            else if (text.IndexOf('\0') < 0 && text.IndexOf(',') >= 0 && char.IsDigit(text[0]))
                units = ReadCsv(text, out extended);
            else
                units = ReadXml(new CryptoUtility("3b91A049Ca7HvSjhxT35").Decrypt(bytes, 0), out extended);
            if (requireExtendedFields && !extended) return false;
            result = Convert(units);
            return true;
        }
        catch (Exception exception)
        {
            error = "BPIR 存档转换失败：" + exception.Message;
            return false;
        }
    }

    public static bool LooksLikeExtendedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        if (text.StartsWith("{"))
        {
            try
            {
                var units = Token(JObject.Parse(text), "Units") as JArray;
                if (units == null) return false;
                foreach (var token in units)
                    if (token is JObject unit && (Token(unit, "Mass") != null || Token(unit, "Members") != null || Token(unit, "Layer") != null ||
                        Token(unit, "Gravity") != null || Token(unit, "Drag") != null || Token(unit, "Size") != null || Token(unit, "Power") != null || Token(unit, "Suction") != null)) return true;
            }
            catch { return false; }
            return false;
        }
        if (text.StartsWith("<"))
        {
            try
            {
                using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                foreach (var item in XDocument.Load(reader).Descendants("ContraptionDatasetUnit"))
                    if (item.Attribute("mass") != null || item.Attribute("members") != null || item.Attribute("layer") != null ||
                        item.Attribute("gravity") != null || item.Attribute("drag") != null || item.Attribute("scale") != null ||
                        item.Attribute("power") != null || item.Attribute("suction") != null) return true;
            }
            catch { return text.Contains("ContraptionDataset") && !text.Contains("customParams="); }
            return false;
        }
        foreach (var row in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = row.Split(new[] { ',', '\t' }, StringSplitOptions.None);
            if (columns.Length >= 8 || (columns.Length == 7 && float.TryParse(columns[6], NumberStyles.Float,
                CultureInfo.InvariantCulture, out _))) return true;
        }
        return false;
    }

    private static List<Unit> ReadCsv(string text, out bool extended)
    {
        extended = false;
        var units = new List<Unit>();
        foreach (string row in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] c = row.Split(new[] { ',', '\t' }, StringSplitOptions.None);
            if (c.Length < 6 || c.Length > 14) throw new FormatException("CSV 每行应为 6 至 14 列");
            extended |= c.Length > 6;
            int flip = Integer(c[5]);
            if (flip != 0 && flip != 1) throw new FormatException("翻转值应为 0 或 1");
            var unit = new Unit { type = Integer(c[0]), index = Integer(c[1]), x = Integer(c[2]), y = Integer(c[3]),
                rotation = Integer(c[4]), flipped = flip != 0 };
            if (c.Length > 6) unit.mass = Number(c[6]);
            if (c.Length > 7) unit.gravity = Integer(c[7]);
            if (c.Length > 8) unit.drag = Integer(c[8]);
            if (c.Length > 9) unit.size = Number(c[9]);
            if (c.Length > 10) unit.power = Number(c[10]);
            if (c.Length > 11) unit.suction = Number(c[11]);
            if (c.Length > 12) unit.members = c[12];
            if (c.Length > 13) unit.layer = Integer(c[13]);
            units.Add(unit);
        }
        return units;
    }

    private static List<Unit> ReadJson(string text, out bool extended)
    {
        extended = false;
        var source = Token(JObject.Parse(text), "Units") as JArray;
        if (source == null) throw new FormatException("缺少 Units 数组");
        var units = new List<Unit>();
        foreach (var token in source)
        {
            if (!(token is JObject item)) throw new FormatException("部件记录不是对象");
            var unit = new Unit { type = RequiredInt(item, "Type"), index = RequiredInt(item, "Index"),
                x = RequiredInt(item, "X"), y = RequiredInt(item, "Y"), rotation = RequiredInt(item, "Rotation") };
            if (Token(item, "Flipped") == null || Token(item, "Flipped").Type != JTokenType.Boolean) throw new FormatException("缺少布尔值 Flipped");
            unit.flipped = Token(item, "Flipped").Value<bool>();
            foreach (var property in item.Properties())
            {
                string value = property.Value.ToString();
                switch (property.Name.ToLowerInvariant())
                {
                    case "mass": unit.mass = Number(value); extended = true; break;
                    case "gravity": unit.gravity = Integer(value); extended = true; break;
                    case "drag": unit.drag = Integer(value); extended = true; break;
                    case "size": unit.size = Number(value); extended = true; break;
                    case "power": unit.power = Number(value); extended = true; break;
                    case "suction": unit.suction = Number(value); extended = true; break;
                    case "members": unit.members = property.Value.Type == JTokenType.Null ? null : value; extended = true; break;
                    case "layer": unit.layer = Integer(value); extended = true; break;
                }
            }
            units.Add(unit);
        }
        return units;
    }

    private static JToken Token(JObject item, string key) => item.GetValue(key, StringComparison.OrdinalIgnoreCase);

    private static int RequiredInt(JObject item, string key)
    {
        if (Token(item, key) == null || Token(item, key).Type != JTokenType.Integer) throw new FormatException("缺少整数 " + key);
        return Integer(Token(item, key).ToString());
    }

    private static List<Unit> ReadXml(byte[] bytes, out bool extended)
    {
        extended = false;
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = XDocument.Load(reader);
        if (document.Root?.Name.LocalName != "ContraptionDataset") throw new FormatException("XML 根节点不是 ContraptionDataset");
        var list = document.Root.Element("ContraptionDatasetList");
        if (list == null) throw new FormatException("缺少 ContraptionDatasetList");
        var units = new List<Unit>();
        foreach (var item in list.Elements("ContraptionDatasetUnit"))
        {
            int rawType = Integer(RequiredAttribute(item, "partType"));
            if (rawType <= 0 || rawType > 50) throw new FormatException("未知的 BPIR 部件类型 " + rawType);
            var unit = new Unit { type = (int)((BasePart.PartType)rawType).ToSortedPartType(),
                index = Integer(RequiredAttribute(item, "customPartIndex")), x = Integer(RequiredAttribute(item, "x")),
                y = Integer(RequiredAttribute(item, "y")), rotation = Integer(RequiredAttribute(item, "rot")),
                flipped = XmlConvert.ToBoolean(RequiredAttribute(item, "flipped")) };
            foreach (var attribute in item.Attributes())
            {
                switch (attribute.Name.LocalName)
                {
                    case "mass": unit.mass = Number(attribute.Value); extended = true; break;
                    case "gravity": unit.gravity = Integer(attribute.Value); extended = true; break;
                    case "drag": unit.drag = Integer(attribute.Value); extended = true; break;
                    case "scale": unit.size = Number(attribute.Value); extended = true; break;
                    case "power": unit.power = Number(attribute.Value); extended = true; break;
                    case "suction": unit.suction = Number(attribute.Value); extended = true; break;
                    case "members": unit.members = attribute.Value; extended = true; break;
                    case "layer": unit.layer = Integer(attribute.Value); extended = true; break;
                }
            }
            units.Add(unit);
        }
        return units;
    }

    private static string RequiredAttribute(XElement item, string key) => item.Attribute(key)?.Value ??
        throw new FormatException("缺少 XML 属性 " + key);
    private static int Integer(string text) => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static float Number(string text)
    {
        float value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!BuildMachineTransform.Finite(value)) throw new FormatException("不支持 NaN 或 Infinity");
        return value;
    }

    private static Result Convert(List<Unit> source)
    {
        var result = new Result { data = new INContraptionData(source.Count) };
        foreach (var unit in source)
        {
            if (unit.type <= 0 || unit.type >= 49 || unit.index < 0 || unit.rotation < 0 || unit.rotation > 7)
                throw new FormatException("非法的部件类型、索引或旋转");
            if (Math.Abs((long)unit.x) > 1000000 || Math.Abs((long)unit.y) > 1000000)
                throw new FormatException("部件坐标超出可转换范围");
            unit.layer = Mathf.Clamp(unit.layer, 1, 6);
            result.UsesMultipleLayers |= unit.layer > 1;
        }
        // Layer 1 is the shared chassis: keep common coordinates and source ordering for enclosure.
        foreach (var unit in source)
        {
                int layer = unit.layer;
                var parameters = new Dictionary<string, float>();
                parameters[SourceKey] = 2f;
                if (unit.mass >= 0f) parameters["m_mass"] = Mathf.Max(0.0001f, unit.mass);
                if (unit.gravity >= 0) parameters[PartPhysicsParameters.GravityKey] = unit.gravity != 0 ? 1f : 0f;
                if (unit.drag >= 0) parameters[PartPhysicsParameters.DragKey] = unit.drag != 0 ? 0.2f : 0f;
                if (unit.size >= 0f) parameters[BasePart.PartScaleKey] = BasePart.ValidScale(unit.size);
                if (unit.power >= 0f) parameters[PartPhysicsParameters.PowerKey] = unit.power;
                if (unit.suction >= 0f) parameters[PartPhysicsParameters.SuctionKey] = unit.suction;
                parameters[PartPhysicsParameters.LayerKey] = layer;
                parameters[GridlessBuild.GroupKey] = 0;
                ConvertMembers(unit.members, parameters, result.warnings);
                var target = new INContraptionData.Unit(unit.type, unit.index, unit.x, unit.y, unit.rotation, unit.flipped);
                var builder = new StringBuilder();
                foreach (var pair in parameters)
                {
                    if (builder.Length > 0) builder.Append(';');
                    builder.Append(pair.Key).Append(':').Append(pair.Value.ToString("R", CultureInfo.InvariantCulture));
                }
                target.CustomParams = builder.ToString(); result.data.Units.Add(target);
        }
        return result;
    }

    private static void ConvertMembers(string members, Dictionary<string, float> target, List<string> warnings)
    {
        if (string.IsNullOrEmpty(members)) return;
        foreach (string entry in members.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            int split = entry.IndexOf('=');
            if (split <= 0 || split == entry.Length - 1) throw new FormatException("无效的成员参数 " + entry);
            string key = entry.Substring(0, split).Trim(), raw = entry.Substring(split + 1).Trim();
            if (key.IndexOfAny(new[] { ',', '\t', '\r', '\n' }) >= 0 || raw.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new FormatException("成员参数包含非法分隔符");
            float value;
            if (bool.TryParse(raw, out bool flag)) value = flag ? 1f : 0f;
            else if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                string warning = "未转换非数值成员 " + key;
                if (!warnings.Contains(warning)) warnings.Add(warning);
                continue;
            }
            if (!BuildMachineTransform.Finite(value)) throw new FormatException("成员参数包含非有限数值");
            string name = key.Substring(key.LastIndexOf(':') + 1);
            switch (key.StartsWith("PartPhysicsOverride:") ? name : key)
            {
                case "@offX": case "_offX": target[PartPhysicsParameters.OffsetXKey] = Mathf.Clamp(value, -1f, 1f); break;
                case "@offY": case "_offY": target[PartPhysicsParameters.OffsetYKey] = Mathf.Clamp(value, -1f, 1f); break;
                case "@rotZ": case "_rotZ": target[PartPhysicsParameters.RotationKey] = Mathf.DeltaAngle(0f, value); break;
                case "@stretchX": case "_stretchX": target[BasePart.PartScaleXKey] = BasePart.ValidScale(value); break;
                case "@stretchY": case "_stretchY": target[BasePart.PartScaleYKey] = BasePart.ValidScale(value); break;
                case "@layer": case "_layer": target[PartPhysicsParameters.DepthKey] = Mathf.Clamp(Mathf.RoundToInt(value), -50, 50); break;
                case "_size": target[BasePart.PartScaleKey] = BasePart.ValidScale(value); break;
                case "_power": target[PartPhysicsParameters.PowerKey] = Mathf.Max(0f, value); break;
                case "_suction": target[PartPhysicsParameters.SuctionKey] = Mathf.Max(0f, value); break;
                default:
                    if (name == "m_mass") target["m_mass"] = Mathf.Max(0.0001f, value);
                    else if (name != "m_layerIndex" && name != "LayerIndex" && !key.StartsWith("@"))
                    {
                        target[key.Replace(':', '.')] = value;
                        WarnMissingMember(key, name, warnings);
                    }
                    else
                    {
                        string warning = "未转换成员 " + key;
                        if (!warnings.Contains(warning)) warnings.Add(warning);
                    }
                    break;
            }
        }
    }

    private static void WarnMissingMember(string key, string member, List<string> warnings)
    {
        int separator = key.IndexOf(':');
        if (separator < 0) return;
        Type type = typeof(BasePart).Assembly.GetType(key.Substring(0, separator));
        while (type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            if (type.GetField(member, flags) != null || type.GetProperty(member, flags)?.CanWrite == true) return;
            type = type.BaseType;
        }
        string warning = "主工程未找到成员 " + key + "（数值已保留）";
        if (!warnings.Contains(warning)) warnings.Add(warning);
    }

    public static string SerializeCsv(INContraptionData data)
    {
        var builder = new StringBuilder();
        foreach (var unit in data.Units)
            builder.Append(unit.Type.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(unit.Index.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(unit.X.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(unit.Y.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(unit.Rotation.ToString(CultureInfo.InvariantCulture)).Append(',').Append(unit.Flipped ? '1' : '0')
                .Append(',').Append(unit.CustomParams).AppendLine();
        return builder.ToString();
    }
}
