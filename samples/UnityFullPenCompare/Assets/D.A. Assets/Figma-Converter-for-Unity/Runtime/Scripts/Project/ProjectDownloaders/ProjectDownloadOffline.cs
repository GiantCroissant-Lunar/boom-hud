using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    public class ProjectDownloadOffline : IProjectDownloadStrategy
    {
        private readonly FigmaConverterUnity _monoBeh;
        
        public ImportMode Mode => ImportMode.Offline;
        
        public ProjectDownloadOffline(FigmaConverterUnity monoBeh)
        {
            _monoBeh = monoBeh;
        }
        
        public async Task DownloadProjectAsync(CancellationToken token)
        {
            string archivePath = _monoBeh.Settings.MainSettings.OfflineArchivePath;

            if (string.IsNullOrWhiteSpace(archivePath))
            {
                throw new Exception(FcuLocKey.log_offline_archive_path_not_set.Localize());
            }

            _monoBeh.Events.OnProjectDownloadStart?.Invoke(_monoBeh);
            _monoBeh.InspectorDrawer.SelectableDocument.Childs.Clear();
            _monoBeh.EditorDelegateHolder.StartProgress?.Invoke(_monoBeh, ProgressBarCategory.ProjectDownloading, 0, true);

            Debug.Log(FcuLocKey.log_offline_loading_project.Localize(archivePath));

            var offlineData = ExtractArchive(archivePath);
            _monoBeh.CurrentProject.OfflineData = offlineData;

            var figmaProject = await LoadProjectFromJson(offlineData.JsonFilePath);
            _monoBeh.CurrentProject.FigmaProject = figmaProject;
            _monoBeh.CurrentProject.ProjectName = figmaProject.Name;

            _monoBeh.InspectorDrawer.FillSelectableFramesArray(figmaProject.Document);

            ApplyManifestSettings(offlineData.ManifestFilePath);

            Debug.Log(FcuLocKey.log_offline_project_loaded.Localize(figmaProject.Name));
        }
        
        public async Task<List<FObject>> DownloadAllNodes(string[] selectedIds, CancellationToken token)
        {
            List<FObject> result = new List<FObject>();
            var offlineData = _monoBeh.CurrentProject.OfflineData;

            // Re-extract if: data is missing, extracted folder is gone, or archive content changed (hash mismatch).
            string archivePath = _monoBeh.Settings.MainSettings.OfflineArchivePath;

            bool needReExtract = string.IsNullOrEmpty(offlineData.ExtractedFolderPath)
                || !Directory.Exists(offlineData.ExtractedFolderPath)
                || (File.Exists(archivePath) && ComputeArchiveHash(archivePath) != offlineData.ArchiveHash);

            if (needReExtract)
            {
                if (string.IsNullOrWhiteSpace(archivePath))
                {
                    throw new Exception(FcuLocKey.log_offline_archive_path_not_set_reload.Localize());
                }

                Debug.Log(FcuLocKey.log_offline_archive_changed.Localize(archivePath));
                offlineData = ExtractArchive(archivePath);
                _monoBeh.CurrentProject.OfflineData = offlineData;
            }

            _monoBeh.EditorDelegateHolder.StartProgress?.Invoke(_monoBeh, ProgressBarCategory.DownloadingNodes, selectedIds.Length, false);

            int loadedCount = 0;
            foreach (string nodeId in selectedIds)
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    FigmaProject nodeProject = await LoadNodeJsonAsync(offlineData.NodesFolder, nodeId);

                    if (!nodeProject.IsDefault() && !nodeProject.Nodes.IsEmpty())
                    {
                        foreach (var item in nodeProject.Nodes)
                        {
                            if (item.Value.IsDefault())
                                continue;

                            result.Add(item.Value.Document);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(FcuLocKey.log_offline_load_node_failed.Localize(nodeId, ex.Message));
                }
                finally
                {
                    loadedCount++;
                    _monoBeh.EditorDelegateHolder.UpdateProgress?.Invoke(_monoBeh, ProgressBarCategory.DownloadingNodes, loadedCount);
                }
            }

            _monoBeh.EditorDelegateHolder.CompleteProgress?.Invoke(_monoBeh, ProgressBarCategory.DownloadingNodes);
            Debug.Log(FcuLocKey.log_offline_nodes_loaded.Localize(result.Count));
            
            return result;
        }
        
        private OfflineProjectData ExtractArchive(string zipPath)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                throw new FileNotFoundException($"ZIP archive not found: {zipPath}");
            }

            string extractPath = Path.Combine(Application.persistentDataPath, FcuConfig.OfflineExtractFolderName);

            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }

            Directory.CreateDirectory(extractPath);

            Debug.Log(FcuLocKey.log_offline_extracting_to.Localize(extractPath));
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            var data = new OfflineProjectData(extractPath)
            {
                // Store MD5 hash of the source archive for change detection on next run.
                ArchiveHash = ComputeArchiveHash(zipPath)
            };

            if (!data.IsValid)
            {
                throw new InvalidDataException($"Invalid archive structure. Expected 'project.json' at: {data.JsonFilePath}");
            }

            Debug.Log(FcuLocKey.log_offline_extract_success.Localize(data.JsonFilePath));
            
            return data;
        }

        /// <summary>
        /// Computes an MD5 hash of the given file for reliable change detection.
        /// </summary>
        private static string ComputeArchiveHash(string filePath)
        {
            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(filePath);
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogError(FcuLocKey.log_offline_hash_failed.Localize(ex.Message));
                return null;
            }
        }

        private void ApplyManifestSettings(string manifestPath)
        {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
            // Case 1: file is absent — normal for archives built before manifest support was added.
            if (!File.Exists(manifestPath))
            {
                Debug.LogError(FcuLocKey.log_offline_manifest_missing.Localize());
                return;
            }

            string json;

            try
            {
                json = File.ReadAllText(manifestPath);
            }
            catch (Exception ex)
            {
                // Case 2: IO error while reading the file.
                Debug.LogError(FcuLocKey.log_offline_manifest_read_failed.Localize(ex.Message));
                return;
            }

            OfflineManifest manifest;

            try
            {
                manifest = DAJson.FromJson<OfflineManifest>(json);
            }
            catch (Exception ex)
            {
                // Case 3: JSON is malformed or cannot be deserialized.
                Debug.LogError(FcuLocKey.log_offline_manifest_parse_failed.Localize(ex.Message));
                return;
            }

            if (!manifest.IsValid)
            {
                Debug.LogError(FcuLocKey.log_offline_manifest_invalid.Localize(manifest.ExportScale, manifest.ImageFormat));
                return;
            }

            _monoBeh.Settings.ImageSpritesSettings.ImageScale = manifest.ExportScale;

            if (System.Enum.TryParse<ImageFormat>(manifest.ImageFormat, ignoreCase: true, out ImageFormat parsedFormat))
            {
                _monoBeh.Settings.ImageSpritesSettings.ImageFormat = parsedFormat;
            }

            Debug.Log(FcuLocKey.log_offline_manifest_applied.Localize(manifest.ExportScale, manifest.ImageFormat));
#endif
        }


        private async Task<FigmaProject> LoadProjectFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"project.json not found: {jsonPath}");
            }

            string jsonContent = File.ReadAllText(jsonPath);

#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
            DAResult<FigmaProject> obj = await DAJson.FromJsonAsync<FigmaProject>(jsonContent);
            var project = obj.Object;
            Debug.Log(FcuLocKey.log_offline_project_json_loaded.Localize(project.Name));
            
            return project;
#else
            throw new InvalidOperationException("JSON.NET is required for offline import. Please install Newtonsoft.Json package.");
#endif
        }

        private async Task<FigmaProject> LoadNodeJsonAsync(string nodesFolder, string nodeId)
        {
            string sanitizedId = OfflineProjectData.SanitizeNodeId(nodeId);
            string jsonPath = Path.Combine(nodesFolder, $"{sanitizedId}{FcuConfig.OfflineNodeFileExtension}");

            if (!File.Exists(jsonPath))
            {
                Debug.LogWarning(FcuLocKey.log_offline_node_json_not_found.Localize(jsonPath));
                
                return default;
            }

            string jsonContent = File.ReadAllText(jsonPath);

#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
            DAResult<FigmaProject> obj = await DAJson.FromJsonAsync<FigmaProject>(jsonContent);
            Debug.Log(FcuLocKey.log_offline_node_json_loaded.Localize(nodeId));
            
            return obj.Object;
#else
            throw new InvalidOperationException("JSON.NET is required for offline import.");
#endif
        }

        public void Cleanup()
        {
            var extractedPath = _monoBeh.CurrentProject.OfflineData.ExtractedFolderPath;
            
            if (!string.IsNullOrEmpty(extractedPath) && Directory.Exists(extractedPath))
            {
                try
                {
                    Directory.Delete(extractedPath, true);
                    Debug.Log(FcuLocKey.log_offline_cleanup.Localize(extractedPath));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(FcuLocKey.log_offline_cleanup_failed.Localize(ex.Message));
                }
            }
        }
    }
}
