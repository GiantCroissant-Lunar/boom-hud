using System;
using System.Collections.Generic;
using System.Text;

namespace DA_Assets.FCU.Snapshot
{
    public static class SnapshotReportFormatter
    {
        /// <summary>
        /// Formats a ComparisonReport into a compact diff-only text
        /// suitable for MCP tool responses. Only deviations are included.
        /// </summary>
        public static string Format(ComparisonReport report, string rootName, string baselineName)
        {
            var sb = new StringBuilder();

            float health = report.TotalComponents > 0
                ? (report.TotalComponents - report.TotalDeviations) / (float)report.TotalComponents * 100f
                : 100f;

            sb.AppendLine($"Snapshot Test: {rootName} vs {baselineName}");
            sb.AppendLine($"Components: {report.TotalComponents} | Deviations: {report.TotalDeviations} | Health: {health:F1}%");

            if (report.TotalDeviations == 0)
            {
                sb.AppendLine();
                sb.AppendLine("No deviations found. Scene matches baseline.");
                return sb.ToString();
            }

            foreach (var rootEntry in report.RootEntries)
            {
                CollectDeviations(rootEntry, sb);
            }

            return sb.ToString();
        }

        private static void CollectDeviations(GameObjectEntry entry, StringBuilder sb)
        {
            bool hasFigma = !string.IsNullOrEmpty(entry.FigmaJson);
            bool hasDeviations = false;

            // Check if this GO has any deviations before printing Figma ref.
            foreach (var comp in entry.Components)
            {
                if (comp.Status != EntryStatus.Match)
                {
                    hasDeviations = true;
                    break;
                }
            }

            // Print Figma reference once per GO, before component diffs.
            if (hasFigma && hasDeviations)
            {
                sb.AppendLine();
                sb.AppendLine($"[FIGMA REF] {entry.RelativePath}:");
                string[] figmaLines = NormalizeSplit(entry.FigmaJson);

                foreach (string fl in figmaLines)
                {
                    sb.AppendLine($"  {fl.TrimEnd()}");
                }
            }

            foreach (var comp in entry.Components)
            {
                if (comp.Status == EntryStatus.Match)
                    continue;

                sb.AppendLine();

                switch (comp.Status)
                {
                    case EntryStatus.Diff:
                        FormatDiff(entry.RelativePath, comp, sb);
                        break;
                    case EntryStatus.Missing:
                        sb.AppendLine($"[MISSING] {entry.RelativePath} > {comp.FileName}");
                        sb.AppendLine("  Component exists in baseline but absent in scene.");
                        break;
                    case EntryStatus.Extra:
                        sb.AppendLine($"[EXTRA] {entry.RelativePath} > {comp.FileName}");
                        sb.AppendLine("  New component in scene (not in baseline).");
                        break;
                }
            }

            foreach (var child in entry.Children)
            {
                CollectDeviations(child, sb);
            }
        }

        /// <summary>
        /// Produces a field-level diff: only lines that differ between
        /// baseline and scene JSON are included.
        /// </summary>
        private static void FormatDiff(string path, ComponentEntry comp, StringBuilder sb)
        {
            string[] baselineLines = NormalizeSplit(comp.BaselineJson);
            string[] sceneLines = NormalizeSplit(comp.SceneJson);

            int changedFields = 0;
            var diffLines = new List<string>();

            int maxLen = Math.Max(baselineLines.Length, sceneLines.Length);

            for (int i = 0; i < maxLen; i++)
            {
                string bLine = i < baselineLines.Length ? baselineLines[i].Trim() : "";
                string sLine = i < sceneLines.Length ? sceneLines[i].Trim() : "";

                if (string.Equals(bLine, sLine, StringComparison.Ordinal))
                    continue;

                changedFields++;

                string fieldName = ExtractFieldName(bLine);

                if (!string.IsNullOrEmpty(fieldName))
                {
                    string bValue = ExtractValue(bLine);
                    string sValue = ExtractValue(sLine);
                    diffLines.Add($"  {fieldName}:  {bValue} -> {sValue}");
                }
                else
                {
                    // Fallback: show raw lines.
                    diffLines.Add($"  - {bLine}");
                    diffLines.Add($"  + {sLine}");
                }
            }

            sb.AppendLine($"[DIFF] {path} > {comp.FileName}  ({changedFields} field(s) changed)");

            foreach (string line in diffLines)
            {
                sb.AppendLine(line);
            }
        }

        private static string[] NormalizeSplit(string json)
        {
            if (string.IsNullOrEmpty(json))
                return Array.Empty<string>();

            return json.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        }

        /// <summary>
        /// Extracts the JSON key name from a line like:  "m_SizeDelta": { "x": 32.0 }
        /// Returns null if no key found.
        /// </summary>
        private static string ExtractFieldName(string line)
        {
            int quoteStart = line.IndexOf('"');
            if (quoteStart < 0) return null;

            int quoteEnd = line.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return null;

            int colonIdx = line.IndexOf(':', quoteEnd);
            if (colonIdx < 0) return null;

            return line.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        /// <summary>
        /// Extracts the value portion of a JSON line after the colon.
        /// </summary>
        private static string ExtractValue(string line)
        {
            int colonIdx = line.IndexOf(':');
            if (colonIdx < 0) return line.Trim();

            string value = line.Substring(colonIdx + 1).Trim();

            // Remove trailing comma.
            if (value.EndsWith(","))
                value = value.Substring(0, value.Length - 1).Trim();

            return value;
        }
    }
}
