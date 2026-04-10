using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using DA_Assets.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    [Serializable]
    public class ProjectImporterBase : FcuBase
    {
        private List<FObject> currentPage => monoBeh.CurrentProject.CurrentPage;
        private FObject virtualPage;
        private string[] frameIds;

        private List<FObject> _downloadedNodes;
        private SyncHelper[] _syncHelpers;
        private SpriteIdentityCache _spriteIdentityCache;

        private IProjectImportStrategy _strategy;
        private UIFramework _cachedFramework;

        private IProjectImportStrategy Strategy
        {
            get
            {
                var framework = monoBeh.Settings.MainSettings.UIFramework;
                if (_strategy == null || _cachedFramework != framework)
                {
                    _cachedFramework = framework;
                    _strategy = framework switch
                    {
                        UIFramework.UGUI => new ProjectImportUGUI(monoBeh),
                        UIFramework.UITK => new ProjectImportUITK(monoBeh),
                        UIFramework.NOVA => new ProjectImportNova(monoBeh),
                        _ => throw new NotSupportedException($"UIFramework {framework} is not supported")
                    };
                }
                return _strategy;
            }
        }

        public async Task<ImportResult> Import_By_Step(int step, bool skipWaitingForWindows, CancellationToken token)
        {
            ImportResult result;
            int nextStep = step + 1;

            switch (step)
            {
                case 0:
                    {
                        // DownloadAllNodes handles both online and offline modes internally
                        _downloadedNodes = await monoBeh.ProjectDownloader.DownloadAllNodes(frameIds, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Nodes downloaded.", nextStep);
                        break;
                    }
                case 1:
                    {
                        virtualPage = new FObject
                        {
                            Id = FcuConfig.PARENT_ID,
                            Name = monoBeh.CurrentProject.ProjectName,
                            Children = _downloadedNodes,
                            Data = new SyncData
                            {
                                GameObject = monoBeh.gameObject,
                                RectGameObject = monoBeh.gameObject,
                                Names = new FNames
                                {
                                    ObjectName = FcuTag.Page.ToString(),
                                },
                                Tags = new List<FcuTag>
                                {
                                    FcuTag.Page
                                }
                            }
                        };

                        _downloadedNodes = null;
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Virtual page created.", nextStep);
                        break;
                    }
                case 2:
                    {
                        monoBeh.NameSetter.ClearNames();
                        currentPage.Clear();
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Names and current page cleared.", nextStep);
                        break;
                    }
                case 3:
                    {
                        await monoBeh.TagSetter.SetTags(virtualPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Tags set.", nextStep);
                        break;
                    }
                case 4:
                    {
                        await ConvertTreeToListAsync(virtualPage, currentPage, token);
                        BuildTransformComputationCache(currentPage);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Node tree converted to list.", nextStep);
                        break;
                    }
                case 5:
                    {
                        await monoBeh.ImageTypeSetter.SetImageTypes(currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Image types set.", nextStep);
                        break;
                    }
                case 6:
                    {
                        await monoBeh.ImageTypeSetter.SetInsideDownloadableFlags(currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Downloadable flags set.", nextStep);
                        break;
                    }
                case 7:
                    {
                        await monoBeh.HashGenerator.SetHashes(currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Hashes set.", nextStep);
                        break;
                    }
                case 8:
                    {
                        await monoBeh.NameSetter.SetNames(currentPage, FcuNameType.Folder, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Folder names set.", nextStep);
                        break;
                    }
                case 9:
                    {
                        await monoBeh.NameSetter.SetNames(currentPage, FcuNameType.File, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "File names set.", nextStep);
                        break;
                    }
                case 10:
                    {
                        await monoBeh.NameSetter.SetNames(currentPage, FcuNameType.UssClass, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "UssClass names set.", nextStep);
                        break;
                    }
                case 11:
                    {
                        monoBeh.CurrentProject.SetRootFrames(currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Root frames set.", nextStep);
                        break;
                    }
                case 12:
                    {
                        if (monoBeh.IsPlaying())
                        {
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess,
                                "Skipping the layout update because the project is in Play Mode. Proceed to the next step.", nextStep);
                            break;
                        }

                        _syncHelpers = monoBeh.SyncHelpers.GetAllSyncHelpers();

                        if (_syncHelpers.IsEmpty())
                        {
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess,
                                "Skipping the layout update because there are no components to update. Proceed to the next step.", nextStep);
                            break;
                        }

                        async Task UpdateLayoutAsync() =>
                            monoBeh.CurrentProject.CurrentPage = await ShowLayoutUpdaterWindow(_syncHelpers, currentPage, token);

                        if (skipWaitingForWindows)
                        {
                            _ = UpdateLayoutAsync();

                            result = ImportResult.Return(
                                ImportStatus.ImportStepSuccess,
                                $"Stop and inform the user that they need to see opened 'LayoutUpdater' window and choose the required settings. After that, the user must confirm continuing the import, after which the Agent should proceed to the next step.",
                                nextStep);
                        }
                        else
                        {
                            await UpdateLayoutAsync();
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Layout updated.", nextStep);
                        }
                        break;
                    }
                case 13:
                    {
                        if (monoBeh.IsPlaying())
                        {
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Skipping prefabs loading because the project is in Play Mode. Proceed to the next step.", nextStep);
                        }
                        else
                        {
                            await LoadPrefabs(token);
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Prefabs loaded.", nextStep);
                        }

                        BuildTransformComputationCache(currentPage);
                        break;
                    }
                case 14:
                    {
                        await DrawGameObjects(virtualPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "GameObjects drawn.", nextStep);
                        break;
                    }
                case 15:
                    {
                        monoBeh.CurrentProject.SetRootFrames(currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Root frames updated.", nextStep);
                        break;
                    }
                case 16:
                    {
                        monoBeh.TagSetter.CountTags(currentPage);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Tags counted.", nextStep);
                        break;
                    }
                case 17:
                    {
                        // Build the sprite identity cache once: top-down angle pass + render-key grouping.
                        // This replaces repeated GetSpriteRenderKey + GetAbsoluteMatrixAngle calls
                        // across SetSpritePaths, downloaders, and SpriteBatchWriter.
                        _spriteIdentityCache = SpriteIdentityCacheBuilder.Build(currentPage);

                        // Wire the cache into every consumer that previously recomputed render-keys.
                        monoBeh.SpriteDownloader.IdentityCache = _spriteIdentityCache;
                        SpriteBatchWriter.SetCache(_spriteIdentityCache);

                        await monoBeh.SpritePathSetter.SetSpritePaths(currentPage, _spriteIdentityCache, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Sprite paths set.", nextStep);
                        break;
                    }
                case 18:
                    {
                        await monoBeh.SpriteDownloader.DownloadSprites(currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Sprites downloaded.", nextStep);
                        break;
                    }
                case 19:
                    {
                        await SpriteBatchWriter.Flush(monoBeh, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Sprite batch flushed.", nextStep);
                        break;
                    }
                case 20:
                    {
                        if (monoBeh.IsPlaying())
                        {
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Skipping sprite generation because the project is in Play Mode. Proceed to the next step.", nextStep);
                        }
                        else
                        {
                            await monoBeh.SpriteGenerator.GenerateSprites(currentPage, token);
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Sprites generated.", nextStep);
                        }
                        break;
                    }
                case 21:
                    {
                        if (monoBeh.IsPlaying())
                        {
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Skipping sprite slicing.", nextStep);
                        }
                        else
                        {
                            await SpriteDataCalculator.CalculateAndSetSpriteData(currentPage, monoBeh, token);
                            await monoBeh.SpriteProcessor.MarkAsSprites(currentPage, token);
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Marked as sprites.", nextStep);
                        }
                        break;
                    }
                case 22:
                    {
                        await monoBeh.SpriteColorizer.ColorizeSprites(currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Sprites colorized.", nextStep);
                        break;
                    }
                case 23:
                    {
                        await monoBeh.SpriteSlicer.SliceSprites(currentPage, token);
                        await monoBeh.SpriteSlicer.AutoSliceSprites(currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Sprites sliced.", nextStep);
                        break;
                    }
                case 24:
                    {
                        if (monoBeh.IsPlaying())
                        {
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Skipping duplicate removal because the project is in Play Mode. Proceed to the next step.", nextStep);
                            break;
                        }

                        if (monoBeh.Settings.MainSettings.UseDuplicateFinder)
                        {
                            var groups = await monoBeh.SpriteDuplicateRemover.GetDuplicateGroups(token);

                            if (groups.IsEmpty())
                            {
                                result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Skipping duplicate removal. Proceed to the next step.", nextStep);
                                break;
                            }

                            async Task RemoveDuplicatesAsync() =>
                                await monoBeh.SpriteDuplicateRemover.RemoveDuplicates(currentPage, groups, token);

                            if (skipWaitingForWindows)
                            {
                                _ = RemoveDuplicatesAsync();
                                result = ImportResult.Return(
                                    ImportStatus.ImportStepSuccess,
                                    $"Stop and inform the user that they need to see opened '{nameof(SpriteDuplicateRemover)}' window and choose the required settings. After that, the user must confirm continuing the import, after which the Agent should proceed to the next step.",
                                    nextStep);
                            }
                            else
                            {
                                await RemoveDuplicatesAsync();
                                result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Duplicates removed.", nextStep);
                            }
                        }
                        else
                        {
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Skipping duplicate removal. Proceed to the next step.", nextStep);
                        }
                        break;
                    }
                case 25:
                    {
                        await monoBeh.FontDownloader.DownloadFonts(currentPage, token);
#if UNITY_EDITOR && TextMeshPro
                        async Task<FontMetricsWindowResult> ValidateTmpFontsAsync() =>
                            await TmpFontMetricsWindowHelper.ShowTmpFontMetricsWindow(monoBeh, currentPage, token);

                        if (skipWaitingForWindows)
                        {
                            _ = ValidateTmpFontsAsync();

                            result = ImportResult.Return(
                                ImportStatus.ImportStepSuccess,
                                "Stop and inform the user that they need to review the opened 'TMP Font Metrics' window. After that, the user must confirm continuing the import, after which the Agent should proceed to the next step.",
                                nextStep);
                        }
                        else
                        {
                            FontMetricsWindowResult tmpFontResult = await ValidateTmpFontsAsync();

                            if (tmpFontResult.Action == FontMetricsWindowAction.StopImport)
                            {
                                return monoBeh.AssetTools.StopAsset(ImportStatus.Stopped);
                            }

                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Fonts downloaded.", nextStep);
                        }
#endif
#if UNITY_EDITOR
                        if (monoBeh.UsingUI_Toolkit_Text())
                        {
                            async Task<FontMetricsWindowResult> ValidateUitkFontsAsync() =>
                                await UitkFontMetricsWindowHelper.ShowUitkFontMetricsWindow(monoBeh, currentPage, token);

                            if (skipWaitingForWindows)
                            {
                                _ = ValidateUitkFontsAsync();

                                result = ImportResult.Return(
                                    ImportStatus.ImportStepSuccess,
                                    "Stop and inform the user that they need to review the opened 'UITK Font Metrics' window. After that, the user must confirm continuing the import, after which the Agent should proceed to the next step.",
                                    nextStep);
                            }
                            else
                            {
                                FontMetricsWindowResult uitkFontResult = await ValidateUitkFontsAsync();

                                if (uitkFontResult.Action == FontMetricsWindowAction.StopImport)
                                {
                                    return monoBeh.AssetTools.StopAsset(ImportStatus.Stopped);
                                }

                                result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Fonts downloaded.", nextStep);
                            }
                        }
                        else
                        {
                            result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Fonts downloaded.", nextStep);
                        }
#else
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Fonts downloaded.", nextStep);
#endif
                        break;
                    }
                case 26:
                    {
                        await FinalSteps(virtualPage, currentPage, token);
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Final steps completed.", nextStep);
                        break;
                    }
                default:
                    {
                        result = ImportResult.Return(ImportStatus.ImportStepSuccess, "Import completed.", -1);
                        break;
                    }
            }

            return result;
        }

        public Task<List<FObject>> ShowLayoutUpdaterWindowInternal(SyncHelper[] syncHelpers, List<FObject> currentPage, CancellationToken token)
        {
            return LayoutUpdaterHelper.ShowLayoutUpdaterWindow(monoBeh, syncHelpers, currentPage, token);
        }

        public List<string> GetSelectedFrameIds()
        {
            List<string> selected = monoBeh.InspectorDrawer.SelectableDocument.Childs
                .SelectMany(si => si.Childs)
                .Where(si => si.Selected)
                .Select(si => si.Id)
                .ToList();

            return selected;
        }

        public async Task ConvertTreeToListAsync(FObject parent, List<FObject> fobjects, CancellationToken token)
        {
            Debug.Log(FcuLocKey.log_convert_tree_to_list.Localize());
            await Task.Run(() => ConvertTreeToList(parent, fobjects, 0, -1, token), token);
        }

        private void ConvertTreeToList(FObject parent, List<FObject> fobjects, int depth, int parentIndex, CancellationToken token)
        {
            foreach (FObject child in parent.Children)
            {
                token.ThrowIfCancellationRequested();

                if (child.Data.IsEmpty || child.ContainsTag(FcuTag.Ignore))
                {
                    child.SetFlagToAllChilds(x => x.Data.IsEmpty = true);
                    continue;
                }

                child.Data.HierarchyLevel = depth + 1;
                child.Data.ParentIndex = parentIndex;

                int currentIndex = fobjects.Count;
                fobjects.Add(child);

                if (parentIndex >= 0 && !parent.ContainsTag(FcuTag.Page))
                {
                    fobjects[parentIndex].Data.ChildIndexes.Add(currentIndex);
                }

                if (child.Data.ForceImage)
                {
                    child.SetFlagToAllChilds(x => x.Data.IsEmpty = true);
                    continue;
                }

                if (child.Children.IsEmpty())
                    continue;

                ConvertTreeToList(child, fobjects, depth + 1, currentIndex, token);
            }
        }

        public void ClearAfterImport()
        {
            if (monoBeh.IsDebug())
                return;

            SyncHelper[] syncHelpers = monoBeh.SyncHelpers.GetAllSyncHelpers();

            Parallel.ForEach(syncHelpers,
                syncHelper => { ObjectCleaner.ClearByAttribute<ClearAttribute>(syncHelper.Data); });

            monoBeh.ImageTypeSetter.ClearAllIds();
            monoBeh.CanvasDrawer.ButtonDrawer.Buttons.Clear();
            monoBeh.CanvasDrawer.GameObjectDrawer.ClearTempRectFrames();
            monoBeh.ImageTypeSetter.ClearAllIds();
            monoBeh.CanvasDrawer.ButtonDrawer.Buttons.Clear();

            _downloadedNodes = null;
            _syncHelpers = null;
        }

        private static void BuildTransformComputationCache(List<FObject> fobjects)
        {
            if (fobjects.IsEmpty())
            {
                return;
            }

            foreach (FObject fobject in fobjects)
            {
                SyncData data = fobject.Data;
                if (data == null)
                {
                    continue;
                }

                float matrixAngle = fobject.GetAngleFromMatrix();
                float figmaRotationAngle = fobject.GetAngleFromField();
                if (figmaRotationAngle == 0f)
                {
                    figmaRotationAngle = matrixAngle;
                }

                float absoluteMatrixAngle = matrixAngle;
                float absoluteFigmaRotationAngle = figmaRotationAngle;
                bool hasRotatedAncestor = false;

                if (!ReferenceEquals(data.Parent, null) &&
                    data.Parent.Data != null &&
                    data.Parent.Data.HasTransformComputationCache)
                {
                    SyncData parentData = data.Parent.Data;
                    absoluteMatrixAngle += parentData.CachedAbsoluteMatrixAngle;
                    absoluteFigmaRotationAngle += parentData.CachedAbsoluteFigmaRotationAngle;
                    hasRotatedAncestor = parentData.CachedHasRotatedAncestor || Mathf.Abs(parentData.CachedMatrixAngle) > 0.001f;
                }

                data.CachedMatrixAngle = matrixAngle;
                data.CachedFigmaRotationAngle = figmaRotationAngle;
                data.CachedAbsoluteMatrixAngle = absoluteMatrixAngle;
                data.CachedAbsoluteFigmaRotationAngle = absoluteFigmaRotationAngle;
                data.CachedHasRotatedAncestor = hasRotatedAncestor;
                data.HasTransformComputationCache = true;
                data.HasCachedGlobalRect = false;
                data.FRect = default;
            }
        }

        public void SetFrameIds(params string[] frameIds) => this.frameIds = frameIds;

        private Task<List<FObject>> ShowLayoutUpdaterWindow(SyncHelper[] syncHelpers, List<FObject> currentPage, CancellationToken token)
        {
            return Strategy.ShowLayoutUpdaterWindow(syncHelpers, currentPage, token);
        }

        private Task LoadPrefabs(CancellationToken token)
        {
            return Strategy.LoadPrefabs(token);
        }

        private Task DrawGameObjects(FObject virtualPage, CancellationToken token)
        {
            return Strategy.DrawGameObjects(virtualPage, token);
        }

        private Task FinalSteps(FObject virtualPage, List<FObject> currentPage, CancellationToken token)
        {
            return Strategy.FinalSteps(virtualPage, currentPage, token);
        }
    }
}
