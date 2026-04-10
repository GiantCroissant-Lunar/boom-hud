using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DA_Assets.FCU
{
    public interface ISpriteDownloadStrategy
    {
        ImportMode Mode { get; }
        
        Task DownloadSprites(List<FObject> fobjects, CancellationToken token);
    }
}
