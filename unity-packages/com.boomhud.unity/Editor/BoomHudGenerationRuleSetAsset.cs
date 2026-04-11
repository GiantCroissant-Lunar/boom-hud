using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using static BoomHud.Unity.Editor.BoomHudGenerationRuleValueConverter;

namespace BoomHud.Unity.Editor
{
    [CreateAssetMenu(
        fileName = "BoomHudGenerationRules",
        menuName = "BoomHud/Generation Rule Set",
        order = 2000)]
    internal sealed class BoomHudGenerationRuleSetAsset : ScriptableObject
    {
        public string version = "1.0";
        public List<BoomHudGenerationRuleEntry> rules = new();

        internal string ToCanonicalJson()
        {
            return BoomHudGenerationRuleJson.Serialize(ToDocument());
        }

        internal void LoadCanonicalJson(string json)
        {
            ApplyDocument(BoomHudGenerationRuleJson.Deserialize(json));
        }

        private BoomHudGenerationRuleSetDocument ToDocument()
        {
            var document = new BoomHudGenerationRuleSetDocument
            {
                version = string.IsNullOrWhiteSpace(version) ? "1.0" : version,
                rules = new List<BoomHudGenerationRuleDocument>()
            };

            foreach (var rule in rules)
            {
                document.rules.Add(rule.ToDocument());
            }

            return document;
        }

        private void ApplyDocument(BoomHudGenerationRuleSetDocument document)
        {
            version = string.IsNullOrWhiteSpace(document.version) ? "1.0" : document.version;
            rules = new List<BoomHudGenerationRuleEntry>();

            if (document.rules == null)
            {
                return;
            }

            foreach (var rule in document.rules)
            {
                rules.Add(BoomHudGenerationRuleEntry.FromDocument(rule));
            }
        }
    }

    [Serializable]
    internal sealed class BoomHudGenerationRuleEntry
    {
        public string name = string.Empty;
        public string phase = string.Empty;
        public string cost = string.Empty;
        public List<BoomHudGenerationRuleFactEntry> preconditions = new();
        public List<BoomHudGenerationRuleFactEntry> effects = new();
        public BoomHudGenerationActionTemplateEntry template = new();
        public BoomHudGenerationRuleSelectorEntry selector = new();
        public BoomHudGenerationRuleActionEntry action = new();

        internal BoomHudGenerationRuleDocument ToDocument()
            => new()
            {
                name = string.IsNullOrWhiteSpace(name) ? null : name,
                phase = ParseEnum<BoomHudGenerationRulePhase>(phase),
                cost = ParseDouble(cost),
                preconditions = ToFactDocuments(preconditions),
                effects = ToFactDocuments(effects),
                template = template.IsEmpty ? null : template.ToDocument(),
                selector = selector.ToDocument(),
                action = action.ToDocument()
            };

        internal static BoomHudGenerationRuleEntry FromDocument(BoomHudGenerationRuleDocument document)
            => new()
            {
                name = document.name ?? string.Empty,
                phase = document.phase?.ToString() ?? string.Empty,
                cost = FormatNumber(document.cost),
                preconditions = FromFactDocuments(document.preconditions),
                effects = FromFactDocuments(document.effects),
                template = BoomHudGenerationActionTemplateEntry.FromDocument(document.template),
                selector = BoomHudGenerationRuleSelectorEntry.FromDocument(document.selector),
                action = BoomHudGenerationRuleActionEntry.FromDocument(document.action)
            };

        private static List<BoomHudGenerationRuleFactDocument>? ToFactDocuments(List<BoomHudGenerationRuleFactEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var documents = new List<BoomHudGenerationRuleFactDocument>();
            foreach (var entry in entries)
            {
                var document = entry.ToDocument();
                if (!string.IsNullOrWhiteSpace(document.key))
                {
                    documents.Add(document);
                }
            }

            return documents.Count == 0 ? null : documents;
        }

        private static List<BoomHudGenerationRuleFactEntry> FromFactDocuments(List<BoomHudGenerationRuleFactDocument>? documents)
        {
            var entries = new List<BoomHudGenerationRuleFactEntry>();
            if (documents == null)
            {
                return entries;
            }

            foreach (var document in documents)
            {
                entries.Add(BoomHudGenerationRuleFactEntry.FromDocument(document));
            }

            return entries;
        }
    }

    [Serializable]
    internal sealed class BoomHudGenerationRuleFactEntry
    {
        public string key = string.Empty;
        public string value = string.Empty;

        internal BoomHudGenerationRuleFactDocument ToDocument()
            => new()
            {
                key = NullIfWhiteSpace(key),
                value = NullIfWhiteSpace(value)
            };

        internal static BoomHudGenerationRuleFactEntry FromDocument(BoomHudGenerationRuleFactDocument? document)
            => new()
            {
                key = document?.key ?? string.Empty,
                value = document?.value ?? string.Empty
            };
    }

    [Serializable]
    internal sealed class BoomHudGenerationRuleSelectorEntry
    {
        public string backend = string.Empty;
        public string documentName = string.Empty;
        public string nodeId = string.Empty;
        public string sourceNodeId = string.Empty;
        public string componentType = string.Empty;
        public string metadataKey = string.Empty;
        public string metadataValue = string.Empty;
        public string clipId = string.Empty;
        public string trackId = string.Empty;
        public string targetId = string.Empty;
        public string motionProperty = string.Empty;
        public string sequenceId = string.Empty;

        internal BoomHudGenerationRuleSelectorDocument ToDocument()
            => new()
            {
                backend = NullIfWhiteSpace(backend),
                documentName = NullIfWhiteSpace(documentName),
                nodeId = NullIfWhiteSpace(nodeId),
                sourceNodeId = NullIfWhiteSpace(sourceNodeId),
                componentType = ParseEnum<BoomHudGenerationComponentType>(componentType),
                metadataKey = NullIfWhiteSpace(metadataKey),
                metadataValue = NullIfWhiteSpace(metadataValue),
                clipId = NullIfWhiteSpace(clipId),
                trackId = NullIfWhiteSpace(trackId),
                targetId = NullIfWhiteSpace(targetId),
                motionProperty = NullIfWhiteSpace(motionProperty),
                sequenceId = NullIfWhiteSpace(sequenceId)
            };

        internal static BoomHudGenerationRuleSelectorEntry FromDocument(BoomHudGenerationRuleSelectorDocument? document)
            => new()
            {
                backend = document?.backend ?? string.Empty,
                documentName = document?.documentName ?? string.Empty,
                nodeId = document?.nodeId ?? string.Empty,
                sourceNodeId = document?.sourceNodeId ?? string.Empty,
                componentType = document?.componentType?.ToString() ?? string.Empty,
                metadataKey = document?.metadataKey ?? string.Empty,
                metadataValue = document?.metadataValue ?? string.Empty,
                clipId = document?.clipId ?? string.Empty,
                trackId = document?.trackId ?? string.Empty,
                targetId = document?.targetId ?? string.Empty,
                motionProperty = document?.motionProperty ?? string.Empty,
                sequenceId = document?.sequenceId ?? string.Empty
            };
    }

    [Serializable]
    internal sealed class BoomHudGenerationRuleActionEntry
    {
        public string controlType = string.Empty;
        public BoomHudGenerationTextRuleActionEntry text = new();
        public BoomHudGenerationIconRuleActionEntry icon = new();
        public BoomHudGenerationLayoutRuleActionEntry layout = new();
        public BoomHudGenerationMotionRuleActionEntry motion = new();

        internal BoomHudGenerationRuleActionDocument ToDocument()
            => new()
            {
                controlType = NullIfWhiteSpace(controlType),
                text = text.IsEmpty ? null : text.ToDocument(),
                icon = icon.IsEmpty ? null : icon.ToDocument(),
                layout = layout.IsEmpty ? null : layout.ToDocument(),
                motion = motion.IsEmpty ? null : motion.ToDocument()
            };

        internal static BoomHudGenerationRuleActionEntry FromDocument(BoomHudGenerationRuleActionDocument? document)
            => new()
            {
                controlType = document?.controlType ?? string.Empty,
                text = BoomHudGenerationTextRuleActionEntry.FromDocument(document?.text),
                icon = BoomHudGenerationIconRuleActionEntry.FromDocument(document?.icon),
                layout = BoomHudGenerationLayoutRuleActionEntry.FromDocument(document?.layout),
                motion = BoomHudGenerationMotionRuleActionEntry.FromDocument(document?.motion)
            };
    }

    [Serializable]
    internal sealed class BoomHudGenerationActionTemplateEntry
    {
        public string kind = string.Empty;
        public string numberValue = string.Empty;
        public string stringValue = string.Empty;
        public string boolValue = string.Empty;
        public List<BoomHudGenerationTemplateParameterEntry> parameters = new();

        internal bool IsEmpty
            => string.IsNullOrWhiteSpace(kind)
                && string.IsNullOrWhiteSpace(numberValue)
                && string.IsNullOrWhiteSpace(stringValue)
                && string.IsNullOrWhiteSpace(boolValue)
                && (parameters == null || parameters.Count == 0);

        internal BoomHudGenerationActionTemplateDocument ToDocument()
            => new()
            {
                kind = NullIfWhiteSpace(kind),
                numberValue = ParseDouble(numberValue),
                stringValue = NullIfWhiteSpace(stringValue),
                boolValue = ParseBool(boolValue),
                parameters = ToParameterMap(parameters)
            };

        internal static BoomHudGenerationActionTemplateEntry FromDocument(BoomHudGenerationActionTemplateDocument? document)
            => new()
            {
                kind = document?.kind ?? string.Empty,
                numberValue = FormatNumber(document?.numberValue),
                stringValue = document?.stringValue ?? string.Empty,
                boolValue = FormatBool(document?.boolValue),
                parameters = FromParameterMap(document?.parameters)
            };

        private static Dictionary<string, string>? ToParameterMap(List<BoomHudGenerationTemplateParameterEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var key = NullIfWhiteSpace(entry.key);
                var value = NullIfWhiteSpace(entry.value);
                if (key != null && value != null)
                {
                    map[key] = value;
                }
            }

            return map.Count == 0 ? null : map;
        }

        private static List<BoomHudGenerationTemplateParameterEntry> FromParameterMap(Dictionary<string, string>? parameters)
        {
            var entries = new List<BoomHudGenerationTemplateParameterEntry>();
            if (parameters == null)
            {
                return entries;
            }

            foreach (var pair in parameters)
            {
                entries.Add(new BoomHudGenerationTemplateParameterEntry
                {
                    key = pair.Key,
                    value = pair.Value
                });
            }

            return entries;
        }
    }

    [Serializable]
    internal sealed class BoomHudGenerationTemplateParameterEntry
    {
        public string key = string.Empty;
        public string value = string.Empty;
    }

    [Serializable]
    internal sealed class BoomHudGenerationTextRuleActionEntry
    {
        public string lineHeight = string.Empty;
        public string wrapText = string.Empty;
        public string fontFamily = string.Empty;
        public string fontSize = string.Empty;
        public string letterSpacing = string.Empty;
        public string textGrowth = string.Empty;

        internal bool IsEmpty
            => string.IsNullOrWhiteSpace(lineHeight)
                && string.IsNullOrWhiteSpace(wrapText)
                && string.IsNullOrWhiteSpace(fontFamily)
                && string.IsNullOrWhiteSpace(fontSize)
                && string.IsNullOrWhiteSpace(letterSpacing)
                && string.IsNullOrWhiteSpace(textGrowth);

        internal BoomHudGenerationTextRuleActionDocument ToDocument()
            => new()
            {
                lineHeight = ParseDouble(lineHeight),
                wrapText = ParseBool(wrapText),
                fontFamily = NullIfWhiteSpace(fontFamily),
                fontSize = ParseDouble(fontSize),
                letterSpacing = ParseDouble(letterSpacing),
                textGrowth = NullIfWhiteSpace(textGrowth)
            };

        internal static BoomHudGenerationTextRuleActionEntry FromDocument(BoomHudGenerationTextRuleActionDocument? document)
            => new()
            {
                lineHeight = FormatNumber(document?.lineHeight),
                wrapText = FormatBool(document?.wrapText),
                fontFamily = document?.fontFamily ?? string.Empty,
                fontSize = FormatNumber(document?.fontSize),
                letterSpacing = FormatNumber(document?.letterSpacing),
                textGrowth = document?.textGrowth ?? string.Empty
            };
    }

    [Serializable]
    internal sealed class BoomHudGenerationIconRuleActionEntry
    {
        public string baselineOffset = string.Empty;
        public string opticalCentering = string.Empty;
        public string sizeMode = string.Empty;
        public string fontSize = string.Empty;

        internal bool IsEmpty
            => string.IsNullOrWhiteSpace(baselineOffset)
                && string.IsNullOrWhiteSpace(opticalCentering)
                && string.IsNullOrWhiteSpace(sizeMode)
                && string.IsNullOrWhiteSpace(fontSize);

        internal BoomHudGenerationIconRuleActionDocument ToDocument()
            => new()
            {
                baselineOffset = ParseDouble(baselineOffset),
                opticalCentering = ParseBool(opticalCentering),
                sizeMode = NullIfWhiteSpace(sizeMode),
                fontSize = ParseDouble(fontSize)
            };

        internal static BoomHudGenerationIconRuleActionEntry FromDocument(BoomHudGenerationIconRuleActionDocument? document)
            => new()
            {
                baselineOffset = FormatNumber(document?.baselineOffset),
                opticalCentering = FormatBool(document?.opticalCentering),
                sizeMode = document?.sizeMode ?? string.Empty,
                fontSize = FormatNumber(document?.fontSize)
            };
    }

    [Serializable]
    internal sealed class BoomHudGenerationLayoutRuleActionEntry
    {
        public string forceAbsolutePositioning = string.Empty;
        public string stretchWidth = string.Empty;
        public string stretchHeight = string.Empty;
        public string preferContentWidth = string.Empty;
        public string preferContentHeight = string.Empty;
        public string edgeAlignment = string.Empty;
        public string gap = string.Empty;
        public string padding = string.Empty;
        public string offsetX = string.Empty;
        public string offsetY = string.Empty;

        internal bool IsEmpty
            => string.IsNullOrWhiteSpace(forceAbsolutePositioning)
                && string.IsNullOrWhiteSpace(stretchWidth)
                && string.IsNullOrWhiteSpace(stretchHeight)
                && string.IsNullOrWhiteSpace(preferContentWidth)
                && string.IsNullOrWhiteSpace(preferContentHeight)
                && string.IsNullOrWhiteSpace(edgeAlignment)
                && string.IsNullOrWhiteSpace(gap)
                && string.IsNullOrWhiteSpace(padding)
                && string.IsNullOrWhiteSpace(offsetX)
                && string.IsNullOrWhiteSpace(offsetY);

        internal BoomHudGenerationLayoutRuleActionDocument ToDocument()
            => new()
            {
                forceAbsolutePositioning = ParseBool(forceAbsolutePositioning),
                stretchWidth = ParseBool(stretchWidth),
                stretchHeight = ParseBool(stretchHeight),
                preferContentWidth = ParseBool(preferContentWidth),
                preferContentHeight = ParseBool(preferContentHeight),
                edgeAlignment = NullIfWhiteSpace(edgeAlignment),
                gap = ParseDouble(gap),
                padding = ParseDouble(padding),
                offsetX = ParseDouble(offsetX),
                offsetY = ParseDouble(offsetY)
            };

        internal static BoomHudGenerationLayoutRuleActionEntry FromDocument(BoomHudGenerationLayoutRuleActionDocument? document)
            => new()
            {
                forceAbsolutePositioning = FormatBool(document?.forceAbsolutePositioning),
                stretchWidth = FormatBool(document?.stretchWidth),
                stretchHeight = FormatBool(document?.stretchHeight),
                preferContentWidth = FormatBool(document?.preferContentWidth),
                preferContentHeight = FormatBool(document?.preferContentHeight),
                edgeAlignment = document?.edgeAlignment ?? string.Empty,
                gap = FormatNumber(document?.gap),
                padding = FormatNumber(document?.padding),
                offsetX = FormatNumber(document?.offsetX),
                offsetY = FormatNumber(document?.offsetY)
            };
    }

    [Serializable]
    internal sealed class BoomHudGenerationMotionRuleActionEntry
    {
        public string durationQuantizationFrames = string.Empty;
        public string easingRemapTo = string.Empty;
        public string sequenceFillMode = string.Empty;
        public string defaultSequenceId = string.Empty;
        public string clipStartOffsetFrames = string.Empty;
        public string forceStepText = string.Empty;
        public string forceStepVisibility = string.Empty;
        public string runtimePropertySupportFallback = string.Empty;
        public string targetResolutionPolicy = string.Empty;
        public string sequenceGroupingPolicy = string.Empty;

        internal bool IsEmpty
            => string.IsNullOrWhiteSpace(durationQuantizationFrames)
                && string.IsNullOrWhiteSpace(easingRemapTo)
                && string.IsNullOrWhiteSpace(sequenceFillMode)
                && string.IsNullOrWhiteSpace(defaultSequenceId)
                && string.IsNullOrWhiteSpace(clipStartOffsetFrames)
                && string.IsNullOrWhiteSpace(forceStepText)
                && string.IsNullOrWhiteSpace(forceStepVisibility)
                && string.IsNullOrWhiteSpace(runtimePropertySupportFallback)
                && string.IsNullOrWhiteSpace(targetResolutionPolicy)
                && string.IsNullOrWhiteSpace(sequenceGroupingPolicy);

        internal BoomHudGenerationMotionRuleActionDocument ToDocument()
            => new()
            {
                durationQuantizationFrames = ParseInt(durationQuantizationFrames),
                easingRemapTo = NullIfWhiteSpace(easingRemapTo),
                sequenceFillMode = NullIfWhiteSpace(sequenceFillMode),
                defaultSequenceId = NullIfWhiteSpace(defaultSequenceId),
                clipStartOffsetFrames = ParseInt(clipStartOffsetFrames),
                forceStepText = ParseBool(forceStepText),
                forceStepVisibility = ParseBool(forceStepVisibility),
                runtimePropertySupportFallback = NullIfWhiteSpace(runtimePropertySupportFallback),
                targetResolutionPolicy = NullIfWhiteSpace(targetResolutionPolicy),
                sequenceGroupingPolicy = NullIfWhiteSpace(sequenceGroupingPolicy)
            };

        internal static BoomHudGenerationMotionRuleActionEntry FromDocument(BoomHudGenerationMotionRuleActionDocument? document)
            => new()
            {
                durationQuantizationFrames = FormatInt(document?.durationQuantizationFrames),
                easingRemapTo = document?.easingRemapTo ?? string.Empty,
                sequenceFillMode = document?.sequenceFillMode ?? string.Empty,
                defaultSequenceId = document?.defaultSequenceId ?? string.Empty,
                clipStartOffsetFrames = FormatInt(document?.clipStartOffsetFrames),
                forceStepText = FormatBool(document?.forceStepText),
                forceStepVisibility = FormatBool(document?.forceStepVisibility),
                runtimePropertySupportFallback = document?.runtimePropertySupportFallback ?? string.Empty,
                targetResolutionPolicy = document?.targetResolutionPolicy ?? string.Empty,
                sequenceGroupingPolicy = document?.sequenceGroupingPolicy ?? string.Empty
            };
    }

    internal enum BoomHudGenerationComponentType
    {
        Label,
        Badge,
        Button,
        TextInput,
        TextArea,
        Checkbox,
        RadioButton,
        ProgressBar,
        Slider,
        Icon,
        Image,
        MenuBar,
        Menu,
        MenuItem,
        Timeline,
        Container,
        ScrollView,
        Panel,
        TabView,
        SplitView,
        ListBox,
        ListView,
        TreeView,
        DataGrid,
        Stack,
        Grid,
        Dock,
        Spacer
    }

    internal enum BoomHudGenerationRulePhase
    {
        Normalize,
        Structure,
        Layout,
        Text,
        Icon,
        Motion,
        Finalize
    }

    internal sealed class BoomHudGenerationRuleSetDocument
    {
        public string version { get; set; } = "1.0";
        public List<BoomHudGenerationRuleDocument>? rules { get; set; } = new();
    }

    internal sealed class BoomHudGenerationRuleDocument
    {
        public string? name { get; set; }
        public BoomHudGenerationRulePhase? phase { get; set; }
        public double? cost { get; set; }
        public List<BoomHudGenerationRuleFactDocument>? preconditions { get; set; }
        public List<BoomHudGenerationRuleFactDocument>? effects { get; set; }
        public BoomHudGenerationActionTemplateDocument? template { get; set; }
        public BoomHudGenerationRuleSelectorDocument selector { get; set; } = new();
        public BoomHudGenerationRuleActionDocument action { get; set; } = new();
    }

    internal sealed class BoomHudGenerationRuleFactDocument
    {
        public string? key { get; set; }
        public string? value { get; set; }
    }

    internal sealed class BoomHudGenerationRuleSelectorDocument
    {
        public string? backend { get; set; }
        public string? documentName { get; set; }
        public string? nodeId { get; set; }
        public string? sourceNodeId { get; set; }
        public BoomHudGenerationComponentType? componentType { get; set; }
        public string? metadataKey { get; set; }
        public string? metadataValue { get; set; }
        public string? clipId { get; set; }
        public string? trackId { get; set; }
        public string? targetId { get; set; }
        public string? motionProperty { get; set; }
        public string? sequenceId { get; set; }
    }

    internal sealed class BoomHudGenerationRuleActionDocument
    {
        public string? controlType { get; set; }
        public BoomHudGenerationTextRuleActionDocument? text { get; set; }
        public BoomHudGenerationIconRuleActionDocument? icon { get; set; }
        public BoomHudGenerationLayoutRuleActionDocument? layout { get; set; }
        public BoomHudGenerationMotionRuleActionDocument? motion { get; set; }
    }

    internal sealed class BoomHudGenerationActionTemplateDocument
    {
        public string? kind { get; set; }
        public double? numberValue { get; set; }
        public string? stringValue { get; set; }
        public bool? boolValue { get; set; }
        public Dictionary<string, string>? parameters { get; set; }
    }

    internal sealed class BoomHudGenerationTextRuleActionDocument
    {
        public double? lineHeight { get; set; }
        public bool? wrapText { get; set; }
        public string? fontFamily { get; set; }
        public double? fontSize { get; set; }
        public double? letterSpacing { get; set; }
        public string? textGrowth { get; set; }
    }

    internal sealed class BoomHudGenerationIconRuleActionDocument
    {
        public double? baselineOffset { get; set; }
        public bool? opticalCentering { get; set; }
        public string? sizeMode { get; set; }
        public double? fontSize { get; set; }
    }

    internal sealed class BoomHudGenerationLayoutRuleActionDocument
    {
        public bool? forceAbsolutePositioning { get; set; }
        public bool? stretchWidth { get; set; }
        public bool? stretchHeight { get; set; }
        public bool? preferContentWidth { get; set; }
        public bool? preferContentHeight { get; set; }
        public string? edgeAlignment { get; set; }
        public double? gap { get; set; }
        public double? padding { get; set; }
        public double? offsetX { get; set; }
        public double? offsetY { get; set; }
    }

    internal sealed class BoomHudGenerationMotionRuleActionDocument
    {
        public int? durationQuantizationFrames { get; set; }
        public string? easingRemapTo { get; set; }
        public string? sequenceFillMode { get; set; }
        public string? defaultSequenceId { get; set; }
        public int? clipStartOffsetFrames { get; set; }
        public bool? forceStepText { get; set; }
        public bool? forceStepVisibility { get; set; }
        public string? runtimePropertySupportFallback { get; set; }
        public string? targetResolutionPolicy { get; set; }
        public string? sequenceGroupingPolicy { get; set; }
    }

    internal static class BoomHudGenerationRuleValueConverter
    {
        internal static string? NullIfWhiteSpace(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        internal static double? ParseDouble(string value)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

        internal static bool? ParseBool(string value)
            => bool.TryParse(value, out var parsed) ? parsed : null;

        internal static int? ParseInt(string value)
            => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

        internal static TEnum? ParseEnum<TEnum>(string value) where TEnum : struct, Enum
            => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;

        internal static string FormatNumber(double? value)
            => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        internal static string FormatBool(bool? value)
            => value?.ToString().ToLowerInvariant() ?? string.Empty;

        internal static string FormatInt(int? value)
            => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    internal static class BoomHudGenerationRuleJson
    {
        internal static string Serialize(BoomHudGenerationRuleSetDocument document)
        {
            var builder = new StringBuilder(512);
            WriteRuleSet(builder, document, 0);
            return builder.ToString();
        }

        internal static BoomHudGenerationRuleSetDocument Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new BoomHudGenerationRuleSetDocument();
            }

            var parser = new Parser(json);
            var value = parser.ParseValue();
            if (value is not Dictionary<string, object?> root)
            {
                return new BoomHudGenerationRuleSetDocument();
            }

            return ReadRuleSet(root);
        }

        private static void WriteRuleSet(StringBuilder builder, BoomHudGenerationRuleSetDocument document, int indent)
        {
            builder.AppendLine("{");
            WriteProperty(builder, indent + 1, "version", document.version ?? "1.0", hasTrailingComma: true);
            Indent(builder, indent + 1);
            builder.AppendLine("\"rules\": [");

            var rules = document.rules ?? new List<BoomHudGenerationRuleDocument>();
            for (var index = 0; index < rules.Count; index++)
            {
                WriteRule(builder, rules[index], indent + 2);
                if (index < rules.Count - 1)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }

            Indent(builder, indent + 1);
            builder.AppendLine("]");
            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteRule(StringBuilder builder, BoomHudGenerationRuleDocument rule, int indent)
        {
            builder.AppendLine("{");
            var wroteAny = false;

            if (!string.IsNullOrWhiteSpace(rule.name))
            {
                WriteProperty(builder, indent + 1, "name", rule.name!, HasRuleNestedContent(rule));
                wroteAny = true;
            }

            if (rule.phase.HasValue)
            {
                if (wroteAny)
                {
                    builder.AppendLine(",");
                }

                WriteProperty(builder, indent + 1, "phase", ToCamelCase(rule.phase.Value.ToString()), HasRuleNestedContent(rule, includePhase: false));
                wroteAny = true;
            }

            if (rule.cost.HasValue)
            {
                if (wroteAny)
                {
                    builder.AppendLine(",");
                }

                WriteProperty(builder, indent + 1, "cost", rule.cost.Value, HasRuleNestedContent(rule, includePhase: false, includeCost: false));
                wroteAny = true;
            }

            WriteNestedObjectIfPresent(builder, indent, "preconditions", rule.preconditions != null && rule.preconditions.Count > 0, nestedBuilder => WriteFactArray(nestedBuilder, rule.preconditions!, indent + 1), ref wroteAny);
            WriteNestedObjectIfPresent(builder, indent, "effects", rule.effects != null && rule.effects.Count > 0, nestedBuilder => WriteFactArray(nestedBuilder, rule.effects!, indent + 1), ref wroteAny);
            WriteNestedObjectIfPresent(builder, indent, "template", rule.template != null, nestedBuilder => WriteTemplate(nestedBuilder, rule.template!, indent + 1), ref wroteAny);
            WriteNestedObjectIfPresent(builder, indent, "selector", true, nestedBuilder => WriteSelector(nestedBuilder, rule.selector, indent + 1), ref wroteAny);
            WriteNestedObjectIfPresent(builder, indent, "action", true, nestedBuilder => WriteAction(nestedBuilder, rule.action, indent + 1), ref wroteAny);

            if (wroteAny)
            {
                builder.AppendLine();
            }

            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteFactArray(StringBuilder builder, List<BoomHudGenerationRuleFactDocument> facts, int indent)
        {
            builder.AppendLine("[");
            for (var index = 0; index < facts.Count; index++)
            {
                builder.AppendLine("{");
                var properties = BuildProperties(
                    ("key", facts[index].key),
                    ("value", facts[index].value));
                WritePropertyBlock(builder, indent + 1, properties);
                builder.AppendLine();
                Indent(builder, indent);
                builder.Append('}');
                if (index < facts.Count - 1)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }

            Indent(builder, indent - 1);
            builder.Append(']');
        }

        private static void WriteSelector(StringBuilder builder, BoomHudGenerationRuleSelectorDocument selector, int indent)
        {
            builder.AppendLine("{");
            var properties = BuildProperties(
                ("backend", selector.backend),
                ("documentName", selector.documentName),
                ("nodeId", selector.nodeId),
                ("sourceNodeId", selector.sourceNodeId),
                ("componentType", selector.componentType.HasValue ? ToCamelCase(selector.componentType.Value.ToString()) : null),
                ("metadataKey", selector.metadataKey),
                ("metadataValue", selector.metadataValue),
                ("clipId", selector.clipId),
                ("trackId", selector.trackId),
                ("targetId", selector.targetId),
                ("motionProperty", selector.motionProperty),
                ("sequenceId", selector.sequenceId));

            WritePropertyBlock(builder, indent + 1, properties);
            builder.AppendLine();
            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteTemplate(StringBuilder builder, BoomHudGenerationActionTemplateDocument template, int indent)
        {
            builder.AppendLine("{");
            var wroteAny = false;
            var properties = BuildProperties(
                ("kind", template.kind),
                ("numberValue", template.numberValue),
                ("stringValue", template.stringValue),
                ("boolValue", template.boolValue));

            if (properties.Count > 0)
            {
                WritePropertyBlock(builder, indent + 1, properties);
                wroteAny = true;
            }

            WriteNestedObjectIfPresent(builder, indent, "parameters", template.parameters != null && template.parameters.Count > 0, nestedBuilder => WriteStringMap(nestedBuilder, template.parameters!, indent + 1), ref wroteAny);

            if (wroteAny)
            {
                builder.AppendLine();
            }

            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteAction(StringBuilder builder, BoomHudGenerationRuleActionDocument action, int indent)
        {
            builder.AppendLine("{");
            var wroteAny = false;

            if (!string.IsNullOrWhiteSpace(action.controlType))
            {
                WriteProperty(builder, indent + 1, "controlType", action.controlType!, HasNestedContent(action));
                wroteAny = true;
            }

            WriteNestedObjectIfPresent(builder, indent, "text", action.text != null, nestedBuilder => WriteTextAction(nestedBuilder, action.text!, indent + 1), ref wroteAny);
            WriteNestedObjectIfPresent(builder, indent, "icon", action.icon != null, nestedBuilder => WriteIconAction(nestedBuilder, action.icon!, indent + 1), ref wroteAny);
            WriteNestedObjectIfPresent(builder, indent, "layout", action.layout != null, nestedBuilder => WriteLayoutAction(nestedBuilder, action.layout!, indent + 1), ref wroteAny);
            WriteNestedObjectIfPresent(builder, indent, "motion", action.motion != null, nestedBuilder => WriteMotionAction(nestedBuilder, action.motion!, indent + 1), ref wroteAny);

            if (wroteAny)
            {
                builder.AppendLine();
            }

            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteTextAction(StringBuilder builder, BoomHudGenerationTextRuleActionDocument action, int indent)
        {
            builder.AppendLine("{");
            var properties = BuildProperties(
                ("lineHeight", action.lineHeight),
                ("wrapText", action.wrapText),
                ("fontFamily", action.fontFamily),
                ("fontSize", action.fontSize),
                ("letterSpacing", action.letterSpacing),
                ("textGrowth", action.textGrowth));
            WritePropertyBlock(builder, indent + 1, properties);
            builder.AppendLine();
            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteIconAction(StringBuilder builder, BoomHudGenerationIconRuleActionDocument action, int indent)
        {
            builder.AppendLine("{");
            var properties = BuildProperties(
                ("baselineOffset", action.baselineOffset),
                ("opticalCentering", action.opticalCentering),
                ("sizeMode", action.sizeMode),
                ("fontSize", action.fontSize));
            WritePropertyBlock(builder, indent + 1, properties);
            builder.AppendLine();
            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteLayoutAction(StringBuilder builder, BoomHudGenerationLayoutRuleActionDocument action, int indent)
        {
            builder.AppendLine("{");
            var properties = BuildProperties(
                ("forceAbsolutePositioning", action.forceAbsolutePositioning),
                ("stretchWidth", action.stretchWidth),
                ("stretchHeight", action.stretchHeight),
                ("preferContentWidth", action.preferContentWidth),
                ("preferContentHeight", action.preferContentHeight),
                ("edgeAlignment", action.edgeAlignment),
                ("gap", action.gap),
                ("padding", action.padding),
                ("offsetX", action.offsetX),
                ("offsetY", action.offsetY));
            WritePropertyBlock(builder, indent + 1, properties);
            builder.AppendLine();
            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteMotionAction(StringBuilder builder, BoomHudGenerationMotionRuleActionDocument action, int indent)
        {
            builder.AppendLine("{");
            var properties = BuildProperties(
                ("durationQuantizationFrames", action.durationQuantizationFrames),
                ("easingRemapTo", action.easingRemapTo),
                ("sequenceFillMode", action.sequenceFillMode),
                ("defaultSequenceId", action.defaultSequenceId),
                ("clipStartOffsetFrames", action.clipStartOffsetFrames),
                ("forceStepText", action.forceStepText),
                ("forceStepVisibility", action.forceStepVisibility),
                ("runtimePropertySupportFallback", action.runtimePropertySupportFallback),
                ("targetResolutionPolicy", action.targetResolutionPolicy),
                ("sequenceGroupingPolicy", action.sequenceGroupingPolicy));
            WritePropertyBlock(builder, indent + 1, properties);
            builder.AppendLine();
            Indent(builder, indent);
            builder.Append('}');
        }

        private static void WriteStringMap(StringBuilder builder, Dictionary<string, string> map, int indent)
        {
            builder.AppendLine("{");
            var index = 0;
            foreach (var pair in map)
            {
                WriteProperty(builder, indent + 1, pair.Key, pair.Value, index < map.Count - 1);
                if (index < map.Count - 1)
                {
                    builder.AppendLine(",");
                }
                else
                {
                    builder.AppendLine();
                }

                index++;
            }

            Indent(builder, indent);
            builder.Append('}');
        }

        private static List<KeyValuePair<string, object?>> BuildProperties(params (string Key, object? Value)[] entries)
        {
            var properties = new List<KeyValuePair<string, object?>>();
            foreach (var entry in entries)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                if (entry.Value is string text && string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                properties.Add(new KeyValuePair<string, object?>(entry.Key, entry.Value));
            }

            return properties;
        }

        private static void WritePropertyBlock(StringBuilder builder, int indent, List<KeyValuePair<string, object?>> properties)
        {
            for (var index = 0; index < properties.Count; index++)
            {
                var property = properties[index];
                WriteProperty(builder, indent, property.Key, property.Value, index < properties.Count - 1);
            }
        }

        private static void WriteNestedObjectIfPresent(StringBuilder builder, int indent, string name, bool present, Action<StringBuilder> writeNested, ref bool wroteAny)
        {
            if (!present)
            {
                return;
            }

            if (wroteAny)
            {
                builder.AppendLine(",");
            }

            WriteObjectProperty(builder, indent + 1, name, writeNested, false);
            wroteAny = true;
        }

        private static void WriteObjectProperty(StringBuilder builder, int indent, string name, Action<StringBuilder> writeValue, bool hasTrailingComma)
        {
            Indent(builder, indent);
            builder.Append('"');
            builder.Append(name);
            builder.Append("\": ");
            writeValue(builder);
            if (hasTrailingComma)
            {
                builder.Append(',');
            }
        }

        private static void WriteProperty(StringBuilder builder, int indent, string name, object value, bool hasTrailingComma)
        {
            Indent(builder, indent);
            builder.Append('"');
            builder.Append(name);
            builder.Append("\": ");
            WritePrimitive(builder, value);
            if (hasTrailingComma)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        private static void WritePrimitive(StringBuilder builder, object value)
        {
            switch (value)
            {
                case string text:
                    builder.Append('"');
                    builder.Append(Escape(text));
                    builder.Append('"');
                    break;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    break;
                case double number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                case float number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                case int number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                case long number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    builder.Append('"');
                    builder.Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));
                    builder.Append('"');
                    break;
            }
        }

        private static BoomHudGenerationRuleSetDocument ReadRuleSet(Dictionary<string, object?> root)
        {
            var document = new BoomHudGenerationRuleSetDocument
            {
                version = ReadString(root, "version") ?? "1.0",
                rules = new List<BoomHudGenerationRuleDocument>()
            };

            if (ReadArray(root, "rules") is { } rules)
            {
                foreach (var item in rules)
                {
                    if (item is Dictionary<string, object?> ruleObject)
                    {
                        document.rules.Add(ReadRule(ruleObject));
                    }
                }
            }

            return document;
        }

        private static BoomHudGenerationRuleDocument ReadRule(Dictionary<string, object?> data)
        {
            return new BoomHudGenerationRuleDocument
            {
                name = ReadString(data, "name"),
                phase = ParseEnum<BoomHudGenerationRulePhase>(ReadString(data, "phase")),
                cost = ReadDouble(data, "cost"),
                preconditions = ReadFactArray(data, "preconditions"),
                effects = ReadFactArray(data, "effects"),
                template = ReadObject(data, "template") is { } template ? ReadTemplate(template) : null,
                selector = ReadSelector(ReadObject(data, "selector")),
                action = ReadAction(ReadObject(data, "action"))
            };
        }

        private static List<BoomHudGenerationRuleFactDocument>? ReadFactArray(Dictionary<string, object?> data, string key)
        {
            if (ReadArray(data, key) is not { } items)
            {
                return null;
            }

            var facts = new List<BoomHudGenerationRuleFactDocument>();
            foreach (var item in items)
            {
                if (item is not Dictionary<string, object?> factObject)
                {
                    continue;
                }

                facts.Add(new BoomHudGenerationRuleFactDocument
                {
                    key = ReadString(factObject, "key"),
                    value = ReadString(factObject, "value")
                });
            }

            return facts;
        }

        private static BoomHudGenerationRuleSelectorDocument ReadSelector(Dictionary<string, object?>? data)
        {
            data ??= new Dictionary<string, object?>();
            return new BoomHudGenerationRuleSelectorDocument
            {
                backend = ReadString(data, "backend"),
                documentName = ReadString(data, "documentName"),
                nodeId = ReadString(data, "nodeId"),
                sourceNodeId = ReadString(data, "sourceNodeId"),
                componentType = ParseEnum<BoomHudGenerationComponentType>(ReadString(data, "componentType")),
                metadataKey = ReadString(data, "metadataKey"),
                metadataValue = ReadString(data, "metadataValue"),
                clipId = ReadString(data, "clipId"),
                trackId = ReadString(data, "trackId"),
                targetId = ReadString(data, "targetId"),
                motionProperty = ReadString(data, "motionProperty"),
                sequenceId = ReadString(data, "sequenceId")
            };
        }

        private static BoomHudGenerationActionTemplateDocument ReadTemplate(Dictionary<string, object?> data)
        {
            return new BoomHudGenerationActionTemplateDocument
            {
                kind = ReadString(data, "kind"),
                numberValue = ReadDouble(data, "numberValue"),
                stringValue = ReadString(data, "stringValue"),
                boolValue = ReadBool(data, "boolValue"),
                parameters = ReadStringMap(ReadObject(data, "parameters"))
            };
        }

        private static BoomHudGenerationRuleActionDocument ReadAction(Dictionary<string, object?>? data)
        {
            data ??= new Dictionary<string, object?>();
            return new BoomHudGenerationRuleActionDocument
            {
                controlType = ReadString(data, "controlType"),
                text = ReadObject(data, "text") is { } text ? ReadTextAction(text) : null,
                icon = ReadObject(data, "icon") is { } icon ? ReadIconAction(icon) : null,
                layout = ReadObject(data, "layout") is { } layout ? ReadLayoutAction(layout) : null,
                motion = ReadObject(data, "motion") is { } motion ? ReadMotionAction(motion) : null
            };
        }

        private static BoomHudGenerationTextRuleActionDocument ReadTextAction(Dictionary<string, object?> data)
        {
            return new BoomHudGenerationTextRuleActionDocument
            {
                lineHeight = ReadDouble(data, "lineHeight"),
                wrapText = ReadBool(data, "wrapText"),
                fontFamily = ReadString(data, "fontFamily"),
                fontSize = ReadDouble(data, "fontSize"),
                letterSpacing = ReadDouble(data, "letterSpacing"),
                textGrowth = ReadString(data, "textGrowth")
            };
        }

        private static BoomHudGenerationIconRuleActionDocument ReadIconAction(Dictionary<string, object?> data)
        {
            return new BoomHudGenerationIconRuleActionDocument
            {
                baselineOffset = ReadDouble(data, "baselineOffset"),
                opticalCentering = ReadBool(data, "opticalCentering"),
                sizeMode = ReadString(data, "sizeMode"),
                fontSize = ReadDouble(data, "fontSize")
            };
        }

        private static BoomHudGenerationLayoutRuleActionDocument ReadLayoutAction(Dictionary<string, object?> data)
        {
            return new BoomHudGenerationLayoutRuleActionDocument
            {
                forceAbsolutePositioning = ReadBool(data, "forceAbsolutePositioning"),
                stretchWidth = ReadBool(data, "stretchWidth"),
                stretchHeight = ReadBool(data, "stretchHeight"),
                preferContentWidth = ReadBool(data, "preferContentWidth"),
                preferContentHeight = ReadBool(data, "preferContentHeight"),
                edgeAlignment = ReadString(data, "edgeAlignment"),
                gap = ReadDouble(data, "gap"),
                padding = ReadDouble(data, "padding"),
                offsetX = ReadDouble(data, "offsetX"),
                offsetY = ReadDouble(data, "offsetY")
            };
        }

        private static BoomHudGenerationMotionRuleActionDocument ReadMotionAction(Dictionary<string, object?> data)
        {
            return new BoomHudGenerationMotionRuleActionDocument
            {
                durationQuantizationFrames = ReadInt(data, "durationQuantizationFrames"),
                easingRemapTo = ReadString(data, "easingRemapTo"),
                sequenceFillMode = ReadString(data, "sequenceFillMode"),
                defaultSequenceId = ReadString(data, "defaultSequenceId"),
                clipStartOffsetFrames = ReadInt(data, "clipStartOffsetFrames"),
                forceStepText = ReadBool(data, "forceStepText"),
                forceStepVisibility = ReadBool(data, "forceStepVisibility"),
                runtimePropertySupportFallback = ReadString(data, "runtimePropertySupportFallback"),
                targetResolutionPolicy = ReadString(data, "targetResolutionPolicy"),
                sequenceGroupingPolicy = ReadString(data, "sequenceGroupingPolicy")
            };
        }

        private static string? ReadString(Dictionary<string, object?> data, string key)
            => data.TryGetValue(key, out var value) ? value as string : null;

        private static double? ReadDouble(Dictionary<string, object?> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            return value switch
            {
                double number => number,
                float number => number,
                int number => number,
                long number => number,
                string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }

        private static int? ReadInt(Dictionary<string, object?> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            return value switch
            {
                int number => number,
                long number => (int)number,
                double number => (int)number,
                float number => (int)number,
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }

        private static bool? ReadBool(Dictionary<string, object?> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            return value switch
            {
                bool boolean => boolean,
                string text when bool.TryParse(text, out var parsed) => parsed,
                _ => null
            };
        }

        private static Dictionary<string, object?>? ReadObject(Dictionary<string, object?> data, string key)
            => data.TryGetValue(key, out var value) ? value as Dictionary<string, object?> : null;

        private static List<object?>? ReadArray(Dictionary<string, object?> data, string key)
            => data.TryGetValue(key, out var value) ? value as List<object?> : null;

        private static Dictionary<string, string>? ReadStringMap(Dictionary<string, object?>? data)
        {
            if (data == null)
            {
                return null;
            }

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in data)
            {
                if (pair.Value is string value && !string.IsNullOrWhiteSpace(value))
                {
                    map[pair.Key] = value;
                }
            }

            return map.Count == 0 ? null : map;
        }

        private static bool HasNestedContent(BoomHudGenerationRuleActionDocument action)
            => action.text != null || action.icon != null || action.layout != null || action.motion != null;

        private static bool HasRuleNestedContent(
            BoomHudGenerationRuleDocument rule,
            bool includePhase = true,
            bool includeCost = true)
            => (includePhase && rule.phase.HasValue)
               || (includeCost && rule.cost.HasValue)
               || (rule.preconditions != null && rule.preconditions.Count > 0)
               || (rule.effects != null && rule.effects.Count > 0)
               || rule.template != null
               || rule.selector != null
               || rule.action != null;

        private static string ToCamelCase(string value)
            => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

        private static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                .Replace("\t", "\\t", StringComparison.Ordinal);
        }

        private static void Indent(StringBuilder builder, int indent)
        {
            for (var index = 0; index < indent; index++)
            {
                builder.Append("  ");
            }
        }

        private sealed class Parser
        {
            private readonly string _json;
            private int _index;

            internal Parser(string json)
            {
                _json = json;
            }

            internal object? ParseValue()
            {
                SkipIgnored();
                if (_index >= _json.Length)
                {
                    return null;
                }

                return _json[_index] switch
                {
                    '{' => ParseObject(),
                    '[' => ParseArray(),
                    '"' => ParseString(),
                    't' => ParseTrue(),
                    'f' => ParseFalse(),
                    'n' => ParseNull(),
                    '-' => ParseNumber(),
                    _ when char.IsDigit(_json[_index]) => ParseNumber(),
                    _ => throw new FormatException($"Unexpected JSON token '{_json[_index]}' at index {_index}.")
                };
            }

            private Dictionary<string, object?> ParseObject()
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                _index++;

                while (true)
                {
                    SkipIgnored();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    var key = ParseString();
                    SkipIgnored();
                    Expect(':');
                    var value = ParseValue();
                    result[key] = value;

                    SkipIgnored();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                    SkipIgnored();
                    if (TryConsume('}'))
                    {
                        return result;
                    }
                }
            }

            private List<object?> ParseArray()
            {
                var result = new List<object?>();
                _index++;

                while (true)
                {
                    SkipIgnored();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    result.Add(ParseValue());

                    SkipIgnored();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                    SkipIgnored();
                    if (TryConsume(']'))
                    {
                        return result;
                    }
                }
            }

            private string ParseString()
            {
                Expect('"');
                var builder = new StringBuilder();

                while (_index < _json.Length)
                {
                    var ch = _json[_index++];
                    if (ch == '"')
                    {
                        return builder.ToString();
                    }

                    if (ch != '\\')
                    {
                        builder.Append(ch);
                        continue;
                    }

                    if (_index >= _json.Length)
                    {
                        break;
                    }

                    var escaped = _json[_index++];
                    builder.Append(escaped switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        '/' => '/',
                        'b' => '\b',
                        'f' => '\f',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        'u' => ParseUnicodeEscape(),
                        _ => escaped
                    });
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (_index + 4 > _json.Length)
                {
                    throw new FormatException("Invalid unicode escape.");
                }

                var hex = _json.Substring(_index, 4);
                _index += 4;
                return (char)Convert.ToInt32(hex, 16);
            }

            private object ParseTrue()
            {
                ExpectSequence("true");
                return true;
            }

            private object ParseFalse()
            {
                ExpectSequence("false");
                return false;
            }

            private object? ParseNull()
            {
                ExpectSequence("null");
                return null;
            }

            private double ParseNumber()
            {
                var start = _index;
                if (_json[_index] == '-')
                {
                    _index++;
                }

                while (_index < _json.Length && char.IsDigit(_json[_index]))
                {
                    _index++;
                }

                if (_index < _json.Length && _json[_index] == '.')
                {
                    _index++;
                    while (_index < _json.Length && char.IsDigit(_json[_index]))
                    {
                        _index++;
                    }
                }

                if (_index < _json.Length && (_json[_index] == 'e' || _json[_index] == 'E'))
                {
                    _index++;
                    if (_index < _json.Length && (_json[_index] == '+' || _json[_index] == '-'))
                    {
                        _index++;
                    }

                    while (_index < _json.Length && char.IsDigit(_json[_index]))
                    {
                        _index++;
                    }
                }

                var number = _json.Substring(start, _index - start);
                return double.Parse(number, CultureInfo.InvariantCulture);
            }

            private void SkipIgnored()
            {
                while (_index < _json.Length)
                {
                    if (char.IsWhiteSpace(_json[_index]))
                    {
                        _index++;
                        continue;
                    }

                    if (_index + 1 < _json.Length && _json[_index] == '/' && _json[_index + 1] == '/')
                    {
                        _index += 2;
                        while (_index < _json.Length && _json[_index] != '\n')
                        {
                            _index++;
                        }

                        continue;
                    }

                    if (_index + 1 < _json.Length && _json[_index] == '/' && _json[_index + 1] == '*')
                    {
                        _index += 2;
                        while (_index + 1 < _json.Length && !(_json[_index] == '*' && _json[_index + 1] == '/'))
                        {
                            _index++;
                        }

                        _index = Math.Min(_index + 2, _json.Length);
                        continue;
                    }

                    break;
                }
            }

            private bool TryConsume(char expected)
            {
                if (_index < _json.Length && _json[_index] == expected)
                {
                    _index++;
                    return true;
                }

                return false;
            }

            private void Expect(char expected)
            {
                SkipIgnored();
                if (_index >= _json.Length || _json[_index] != expected)
                {
                    throw new FormatException($"Expected '{expected}' at index {_index}.");
                }

                _index++;
            }

            private void ExpectSequence(string expected)
            {
                SkipIgnored();
                for (var index = 0; index < expected.Length; index++)
                {
                    if (_index + index >= _json.Length || _json[_index + index] != expected[index])
                    {
                        throw new FormatException($"Expected '{expected}' at index {_index}.");
                    }
                }

                _index += expected.Length;
            }
        }
    }
}
