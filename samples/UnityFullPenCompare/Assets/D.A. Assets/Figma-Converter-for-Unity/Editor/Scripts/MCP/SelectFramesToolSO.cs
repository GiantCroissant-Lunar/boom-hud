using DA_Assets.Shared.MCP;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    [CreateAssetMenu(fileName = "SelectFramesTool", menuName = "D.A. Assets/FCU/MCP Tools/SelectFrames")]
    public class SelectFramesToolSO : McpToolSO
    {
        public override IMcpTool CreateInstance(object context)
        {
            return new SelectFramesTool(context as FigmaConverterUnity, this);
        }
    }
}
