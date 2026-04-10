using DA_Assets.Shared.MCP;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DA_Assets.FCU.MCP
{
    public class FcuInfoTool : FcuMcpToolBase
    {
        public FcuInfoTool(FigmaConverterUnity monoBeh, McpToolSO toolSO) : base(monoBeh, toolSO)
        {
        }

        protected override Task<IReadOnlyList<ContentItem>> ExecuteWithContextAsync(FigmaConverterUnity monoBeh, Dictionary<string, object> args)
        {
            IReadOnlyList<ContentItem> response = new[]
            {
                new ContentItem
                {
                    Type = "text",
                    Text = GetTemplate("info")
                }
            };

            return Task.FromResult(response);
        }
    }
}
