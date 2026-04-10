using DA_Assets.FCU.Snapshot;
using DA_Assets.Shared.MCP;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    public class RunSnapshotTestTool : FcuMcpToolBase
    {
        public RunSnapshotTestTool(FigmaConverterUnity monoBeh, McpToolSO toolSO) : base(monoBeh, toolSO)
        {
        }

        protected override Task<IReadOnlyList<ContentItem>> ExecuteWithContextAsync(FigmaConverterUnity monoBeh, Dictionary<string, object> args)
        {
            // Resolve root frame.
            Transform root = monoBeh.SnapshotSettings.RootFrame;

            if (root == null)
            {
                return ErrorResult(GetTemplate("error_no_root"));
            }

            // Resolve baseline name.
            string baselineName;

            if (args.TryGetValue("baseline", out var baselineObj))
            {
                baselineName = Convert.ToString(baselineObj, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            else
            {
                baselineName = monoBeh.SnapshotSettings.SelectedBaseline;
            }

            if (string.IsNullOrEmpty(baselineName))
            {
                return ErrorResult(GetTemplate("error_no_baseline"));
            }

            // Validate baseline exists.
            string[] available = SnapshotSaver.GetAvailableBaselines();
            bool found = false;

            foreach (string name in available)
            {
                if (string.Equals(name, baselineName, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    baselineName = name;
                    break;
                }
            }

            if (!found)
            {
                string list = available.Length > 0
                    ? string.Join(", ", available)
                    : "none";

                return ErrorResult(FormatTemplate("error_baseline_not_found", baselineName, list));
            }

            // Run comparison.
            string zipPath = SnapshotSaver.GetBaselinePath(baselineName);
            ComparisonReport report = SnapshotComparer.Compare(root, zipPath);

            string formatted = SnapshotReportFormatter.Format(report, root.name, baselineName);

            IReadOnlyList<ContentItem> response = new[]
            {
                new ContentItem
                {
                    Type = "text",
                    Text = formatted
                }
            };

            return Task.FromResult(response);
        }

        private static Task<IReadOnlyList<ContentItem>> ErrorResult(string message)
        {
            IReadOnlyList<ContentItem> response = new[]
            {
                new ContentItem
                {
                    Type = "text",
                    Text = message
                }
            };

            return Task.FromResult(response);
        }
    }
}
