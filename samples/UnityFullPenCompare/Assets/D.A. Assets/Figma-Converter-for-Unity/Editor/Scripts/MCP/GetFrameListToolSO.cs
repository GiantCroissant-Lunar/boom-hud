using DA_Assets.Shared.MCP;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    [CreateAssetMenu(fileName = "GetFrameListTool", menuName = "D.A. Assets/FCU/MCP Tools/GetFrameList")]
    public class GetFrameListToolSO : McpToolSO
    {
        public override IMcpTool CreateInstance(object context)
        {
            return new GetFrameListTool(context as FigmaConverterUnity, this);
        }
    }
}
