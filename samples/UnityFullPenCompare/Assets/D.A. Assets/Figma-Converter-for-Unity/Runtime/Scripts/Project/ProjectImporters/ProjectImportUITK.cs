using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS1998

namespace DA_Assets.FCU
{
    public class ProjectImportUITK : IProjectImportStrategy
    {
        private readonly FigmaConverterUnity _monoBeh;
        
        public UIFramework Framework => UIFramework.UITK;
        
        public ProjectImportUITK(FigmaConverterUnity monoBeh)
        {
            _monoBeh = monoBeh;
        }
        
        public async Task<List<FObject>> ShowLayoutUpdaterWindow(
            SyncHelper[] syncHelpers,
            List<FObject> currentPage,
            CancellationToken token)
        {
            return null;
        }
        
        public Task LoadPrefabs(CancellationToken token)
        {
            return Task.CompletedTask;
        }
        
        public Task DrawGameObjects(FObject virtualPage, CancellationToken token)
        {
            return Task.CompletedTask;
        }
        
        public async Task FinalSteps(FObject virtualPage, List<FObject> currentPage, CancellationToken token)
        {
            await _monoBeh.NameSetter.Set_UITK_Names(currentPage);

            if (_monoBeh.UITK_Converter != null)
            {
                await _monoBeh.UITK_Converter.Convert(virtualPage, currentPage, token);
            }
        }
    }
}
