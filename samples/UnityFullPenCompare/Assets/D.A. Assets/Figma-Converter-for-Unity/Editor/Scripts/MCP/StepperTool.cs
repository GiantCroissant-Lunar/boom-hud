using DA_Assets.Shared.MCP;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DA_Assets.FCU.MCP
{
    public class StepperTool : FcuMcpToolBase
    {
        public StepperTool(FigmaConverterUnity monoBeh, McpToolSO toolSO) : base(monoBeh, toolSO)
        {
        }

        protected override async Task<IReadOnlyList<ContentItem>> ExecuteWithContextAsync(FigmaConverterUnity monoBeh, Dictionary<string, object> args)
        {
            int stepIndex = 0;

            if (args.TryGetValue("step_index", out object stepIndexObj))
            {
                stepIndex = Convert.ToInt32(stepIndexObj);
            }

            ImportResult result = await monoBeh.ProjectImporter.Start_Import_By_Step(stepIndex);

            return new[]
            {
                new ContentItem
                {
                    Type = "text",
                    Text = result.Message
                }
            };
        }
    }
}
