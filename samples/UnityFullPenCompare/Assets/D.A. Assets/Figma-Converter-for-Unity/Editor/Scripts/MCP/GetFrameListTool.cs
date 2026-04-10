using DA_Assets.FCU;
using DA_Assets.Shared.MCP;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA_Assets.FCU.MCP
{
    public class GetFrameListTool : FcuMcpToolBase
    {
        public GetFrameListTool(FigmaConverterUnity monoBeh, McpToolSO toolSO) : base(monoBeh, toolSO)
        {
        }

        protected override Task<IReadOnlyList<ContentItem>> ExecuteWithContextAsync(FigmaConverterUnity monoBeh, Dictionary<string, object> args)
        {
            SelectableFObject document = monoBeh.InspectorDrawer.SelectableDocument;

            if (document == null || document.Childs == null || document.Childs.Count == 0)
            {
                IReadOnlyList<ContentItem> emptyResponse = new[]
                {
                    new ContentItem
                    {
                        Type = "text",
                        Text = GetTemplate("empty")
                    }
                };

                return Task.FromResult(emptyResponse);
            }

            List<PageInfo> pages = document.Childs
                .Select(p => new PageInfo(p, p.Childs ?? new List<SelectableFObject>()))
                .ToList();

            string tree = BuildTree(pages);
            string table = BuildTable(pages, out List<string> selectedPages, out List<string> selectedFrames);
            string selectedInfo = $"Selected: pages={(selectedPages.Any() ? string.Join(", ", selectedPages) : "none")}; frames={(selectedFrames.Any() ? string.Join(", ", selectedFrames) : "none")}";

            IReadOnlyList<ContentItem> response = new[]
            {
                new ContentItem
                {
                    Type = "text",
                    Text = $"{GetTemplate("instruction")}\n\n" +
                           $"{GetTemplate("hierarchy_prefix")}\n{tree}\n\n" +
                           $"{GetTemplate("agent_data_prefix")}\n{table}\n{selectedInfo}"
                }
            };

            return Task.FromResult(response);
        }

        private static string BuildTree(IEnumerable<PageInfo> pages)
        {
            StringBuilder sb = new StringBuilder();

            foreach (PageInfo page in pages)
            {
                sb.AppendLine($"- {page.Page.Name}");

                foreach (SelectableFObject frame in page.Frames)
                {
                    sb.AppendLine($"  - {frame.Name}");
                }

                if (page.Frames.Count == 0)
                {
                    sb.AppendLine("  - (no frames)");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string BuildTable(IEnumerable<PageInfo> pages, out List<string> selectedPages, out List<string> selectedFrames)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("type | name | pageId | frameId");

            selectedPages = new List<string>();
            selectedFrames = new List<string>();

            foreach (PageInfo page in pages)
            {
                sb.AppendLine($"page | {page.Page.Name} | {page.Page.Id} |");

                if (page.Page.Selected && !string.IsNullOrWhiteSpace(page.Page.Id))
                {
                    selectedPages.Add(page.Page.Id);
                }

                foreach (SelectableFObject frame in page.Frames)
                {
                    string frameKey = $"{page.Page.Id}:{frame.Id}";
                    sb.AppendLine($"frame | {frame.Name} | {page.Page.Id} | {frame.Id}");

                    if (frame.Selected && !string.IsNullOrWhiteSpace(frame.Id))
                    {
                        selectedFrames.Add(frameKey);
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        private readonly struct PageInfo
        {
            public PageInfo(SelectableFObject page, List<SelectableFObject> frames)
            {
                Page = page;
                Frames = frames;
            }

            public SelectableFObject Page { get; }
            public List<SelectableFObject> Frames { get; }
        }
    }
}