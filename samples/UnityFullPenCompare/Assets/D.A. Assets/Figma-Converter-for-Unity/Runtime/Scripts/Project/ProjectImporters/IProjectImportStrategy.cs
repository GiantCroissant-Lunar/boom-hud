using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DA_Assets.FCU
{
    public interface IProjectImportStrategy
    {
        UIFramework Framework { get; }
        
        Task<List<FObject>> ShowLayoutUpdaterWindow(
            SyncHelper[] syncHelpers,
            List<FObject> currentPage,
            CancellationToken token);
        
        Task LoadPrefabs(CancellationToken token);
        
        Task DrawGameObjects(FObject virtualPage, CancellationToken token);
        
        Task FinalSteps(FObject virtualPage, List<FObject> currentPage, CancellationToken token);
    }
}
