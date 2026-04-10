using DA_Assets.Shared.MCP;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    [CreateAssetMenu(fileName = "SetProjectUrlTool", menuName = "D.A. Assets/FCU/MCP Tools/SetProjectUrl")]
    public class SetProjectUrlToolSO : McpToolSO
    {
        public override IMcpTool CreateInstance(object context)
        {
            return new SetProjectUrlTool(context as FigmaConverterUnity, this);
        }
    }
}
