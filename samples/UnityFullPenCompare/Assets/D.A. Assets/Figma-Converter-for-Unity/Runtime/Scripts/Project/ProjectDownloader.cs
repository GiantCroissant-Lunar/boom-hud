using DA_Assets.Constants;
using DA_Assets.DAI;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DA_Assets.FCU
{
    [Serializable]
    public class ProjectDownloader : FcuBase
    {
        private IProjectDownloadStrategy _strategy;
        private ImportMode _cachedMode;
        
        private IProjectDownloadStrategy Strategy
        {
            get
            {
                var currentMode = monoBeh.Settings.MainSettings.ImportMode;
                if (_strategy == null || _cachedMode != currentMode)
                {
                    _cachedMode = currentMode;
                    _strategy = currentMode switch
                    {
                        ImportMode.Online => new ProjectDownloadOnline(monoBeh),
                        ImportMode.Offline => new ProjectDownloadOffline(monoBeh),
                        _ => throw new NotSupportedException($"ImportMode {currentMode} is not supported")
                    };
                }
                return _strategy;
            }
        }
        
        public async Task<ImportResult> DownloadProject()
        {
            ImportResult result;

            monoBeh.AssetTools.StopAsset(ImportStatus.Technical);
            monoBeh.ProjectImporter.ImportTokenSource = new CancellationTokenSource();

            try
            {
                if (monoBeh.IsJsonNetExists() == false)
                {
                    throw new Exception(FcuLocKey.log_cant_find_package.Localize(DAConstants.JsonNetPackageName));
                }
                
                await Strategy.DownloadProjectAsync(monoBeh.ProjectImporter.ImportTokenSource.Token);
                monoBeh.Events.OnProjectDownloaded?.Invoke(monoBeh);
                result = monoBeh.AssetTools.StopAsset(ImportStatus.ProjectDownloadSuccess);
            }
            catch (OperationCanceledException)
            {
                result = monoBeh.AssetTools.StopAsset(ImportStatus.Stopped);
            }
            catch (Exception ex)
            {
                result = monoBeh.AssetTools.StopAsset(ImportStatus.Exception, ex);
                monoBeh.Events.OnProjectDownloadFail?.Invoke(monoBeh);
            }
            finally
            {
                monoBeh.EditorDelegateHolder.CompleteProgress?.Invoke(monoBeh, ProgressBarCategory.ProjectDownloading);
            }

            return result;
        }
        
        public async Task<FigmaProject> GetFileStructureAsync(int depth, CancellationToken ct = default)
        {
            DARequest projectRequest = RequestCreator.CreateFileStructRequest(
                monoBeh.RequestSender.GetRequestHeader(monoBeh.Authorizer.Token),
                monoBeh.Settings.MainSettings.ProjectId,
                depth);

            DAResult<FigmaProject> result = await monoBeh.RequestSender.SendRequest<FigmaProject>(projectRequest, ct);
            
            return result.Object;
        }
        
        public Task<List<FObject>> DownloadAllNodes(string[] selectedIds, CancellationToken token)
        {
            return Strategy.DownloadAllNodes(selectedIds, token);
        }

        /// <summary>
        /// Removes the temporary extraction folder after a successful offline import.
        /// No-op in Online mode.
        /// </summary>
        public void CleanupOfflineData()
        {
            if (Strategy is ProjectDownloadOffline offline)
            {
                offline.Cleanup();
            }
        }
    }
}
