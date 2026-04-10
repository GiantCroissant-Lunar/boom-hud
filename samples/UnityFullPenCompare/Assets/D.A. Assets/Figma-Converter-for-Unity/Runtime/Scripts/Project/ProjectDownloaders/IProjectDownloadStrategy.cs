using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DA_Assets.FCU
{
    public interface IProjectDownloadStrategy
    {
        ImportMode Mode { get; }
        
        Task DownloadProjectAsync(CancellationToken token);
        
        Task<List<FObject>> DownloadAllNodes(string[] selectedIds, CancellationToken token);
    }
}
