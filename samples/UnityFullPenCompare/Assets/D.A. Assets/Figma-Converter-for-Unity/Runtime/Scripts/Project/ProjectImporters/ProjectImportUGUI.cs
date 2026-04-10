using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DA_Assets.FCU
{
    public class ProjectImportUGUI : IProjectImportStrategy
    {
        private readonly FigmaConverterUnity _monoBeh;
        
        public UIFramework Framework => UIFramework.UGUI;
        
        public ProjectImportUGUI(FigmaConverterUnity monoBeh)
        {
            _monoBeh = monoBeh;
        }
        
        public async Task<List<FObject>> ShowLayoutUpdaterWindow(
            SyncHelper[] syncHelpers,
            List<FObject> currentPage,
            CancellationToken token)
        {
            return await LayoutUpdaterHelper.ShowLayoutUpdaterWindow(_monoBeh, syncHelpers, currentPage, token);
        }
        
        public Task LoadPrefabs(CancellationToken token)
        {
            _monoBeh.CurrentProject.LoadLocalPrefabs(token);
            return Task.CompletedTask;
        }
        
        public Task DrawGameObjects(FObject virtualPage, CancellationToken token)
        {
            _monoBeh.CanvasDrawer.GameObjectDrawer.Draw(virtualPage, token);
            return Task.CompletedTask;
        }
        
        public async Task FinalSteps(FObject virtualPage, List<FObject> currentPage, CancellationToken token)
        {
            _monoBeh.CanvasDrawer.AddCanvasComponent();

            await _monoBeh.TransformSetter.SetTransformPos(currentPage);
            await _monoBeh.TransformSetter.MoveUguiTransforms(currentPage);
            await _monoBeh.TransformSetter.RestoreParents(currentPage);
            await _monoBeh.TransformSetter.SetStretchAllIfNeeded(currentPage);
            await _monoBeh.TransformSetter.SetSiblingIndex(currentPage);

            token.ThrowIfCancellationRequested();

            await _monoBeh.CanvasDrawer.DrawToCanvas(currentPage, token);
        }
    }
}
