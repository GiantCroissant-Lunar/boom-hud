using DA_Assets.Shared.MCP;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    [CreateAssetMenu(fileName = "FcuInfoTool", menuName = "D.A. Assets/FCU/MCP Tools/FcuInfo")]
    public class FcuInfoToolSO : McpToolSO
    {
        public override IMcpTool CreateInstance(object context)
        {
            return new FcuInfoTool(context as FigmaConverterUnity, this);
        }
    }
}
