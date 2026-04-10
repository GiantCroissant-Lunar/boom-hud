using DA_Assets.Constants;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.Tools;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    [Serializable]
    public class ProjectImporter : FcuBase
    {
        public CancellationTokenSource ImportTokenSource { get; set; }

        private ProjectImporterBase _importer;
        public ProjectImporterBase Importer => _importer;

        public Task<ImportResult> Start_Import_By_Step(int step)
        {
            return ExecuteSafe(async () =>
            {
                ImportResult result;

                if (step == 0)
                {
                    InitializeImport();
                }
                else if (_importer == null || this.ImportTokenSource == null)
                {
                    monoBeh.AssetTools.StopAsset(ImportStatus.Technical);
                    return ImportResult.Return(ImportStatus.Exception, "Import is not initialized. Run import step 0 first.", 0);
                }

                result = await _importer.Import_By_Step(
                    step,
                    skipWaitingForWindows: true,
                    this.ImportTokenSource.Token);

                if (result.NextStep == -1)
                {
                    AferImport();
                    return monoBeh.AssetTools.StopAsset(ImportStatus.ImportSuccess);
                }

                return result;
            });
        }

        public Task<ImportResult> StartImport()
        {
            return ExecuteSafe(async () =>
            {
                InitializeImport();

                int currentStep = 0;
                ImportResult result = default;

                while (currentStep != -1)
                {
                    result = await _importer.Import_By_Step(
                        currentStep,
                        skipWaitingForWindows: false,
                        this.ImportTokenSource.Token);

                    if (result.Status != ImportStatus.ImportStepSuccess)
                    {
                        return result;
                    }

                    currentStep = result.NextStep;
                }

                AferImport();
                return monoBeh.AssetTools.StopAsset(ImportStatus.ImportSuccess);
            });
        }

        private void InitializeImport()
        {
            if (monoBeh.IsJsonNetExists() == false)
            {
                throw new Exception(FcuLocKey.log_cant_find_package.Localize(DAConstants.JsonNetPackageName));
            }

            if (!monoBeh.Authorizer.IsAuthed())
            {
                throw new Exception("Not authorized. Please authenticate first.");
            }

            if (monoBeh.InspectorDrawer.SelectableDocument.IsProjectEmpty())
            {
                throw new Exception(FcuLocKey.log_project_empty.Localize());
            }

            if (!ValidateImportSettings(out string reason))
            {
                throw new Exception(reason);
            }

            _importer = CreateImporter();

            monoBeh.AssetTools.StopAsset(ImportStatus.Technical);

            string[] frameIds = _importer.GetSelectedFrameIds().ToArray();

            if (frameIds.Length < 1)
            {
                throw new Exception(FcuLocKey.log_nothing_to_import.Localize());
            }

            _importer.SetFrameIds(frameIds);

            BeforeImport();

            this.ImportTokenSource = new CancellationTokenSource();
        }

        private async Task<ImportResult> ExecuteSafe(Func<Task<ImportResult>> action)
        {
            try
            {
                return await action();
            }
            catch (OperationCanceledException)
            {
                return monoBeh.AssetTools.StopAsset(ImportStatus.Stopped);
            }
            catch (Exception ex)
            {
                return monoBeh.AssetTools.StopAsset(ImportStatus.Exception, ex);
            }
        }

        void BeforeImport()
        {
            ImportTempObject.DestroyAll();
            SceneBackuper.TryBackupActiveScene();
            monoBeh.Events.OnImportStart?.Invoke(monoBeh);
            monoBeh.FolderCreator.CreateAll();
        }

        void AferImport()
        {
            _importer.ClearAfterImport();
            SceneBackuper.MakeActiveSceneDirty();
            monoBeh.AssetTools.ShowRateMe();
            monoBeh.ProjectDownloader.CleanupOfflineData();
        }


        ProjectImporterBase CreateImporter()
        {
            var pib = new ProjectImporterBase();
            pib.Init(monoBeh);
            
            return pib;
        }

        private bool ValidateImportSettings(out string reason)
        {
            bool? result = null;
            reason = "null";

            if (monoBeh.IsUITK())
            {
                if (monoBeh.Settings.ImageSpritesSettings.ImageComponent != ImageComponent.UI_Toolkit_Image)
                {
                    reason = FcuLocKey.log_import_failed_incompatible.Localize(
                        $"{nameof(UIFramework)}.{UIFramework.UITK}", $"{nameof(ImageFormat)}.{monoBeh.Settings.ImageSpritesSettings.ImageComponent}");
                    result = false;
                }
                else if (monoBeh.Settings.TextFontsSettings.TextComponent != TextComponent.UI_Toolkit_Text)
                {
                    reason = FcuLocKey.log_import_failed_incompatible.Localize(
                        $"{nameof(UIFramework)}.{UIFramework.UITK}", $"{nameof(TextComponent)}.{monoBeh.Settings.TextFontsSettings.TextComponent}");
                    result = false;
                }
                else if (monoBeh.UsingSVG())
                {
                    reason = FcuLocKey.log_import_failed_incompatible.Localize(
                        $"{nameof(UIFramework)}.{UIFramework.UITK}", $"{nameof(ImageFormat)}.{monoBeh.Settings.ImageSpritesSettings.ImageFormat}");
                    result = false;
                }
                else if (monoBeh.Settings.LocalizationSettings.LocalizationComponent ==
                         LocalizationComponent.I2Localization)
                {
                    reason = FcuLocKey.log_import_failed_incompatible.Localize(
                        $"{nameof(UIFramework)}.{UIFramework.UITK}",
                        $"{nameof(LocalizationComponent)}.{monoBeh.Settings.LocalizationSettings.LocalizationComponent}");
                    result = false;
                }
            }
            else
            {
                if (monoBeh.UsingUI_Toolkit_Image())
                {
                    reason = FcuLocKey.log_import_failed_incompatible.Localize(
                        $"{nameof(UIFramework)}.{monoBeh.Settings.MainSettings.UIFramework}",
                        $"{nameof(LocalizationComponent)}.{monoBeh.Settings.ImageSpritesSettings.ImageComponent}");
                    result = false;
                }
                else if (monoBeh.UsingUI_Toolkit_Text())
                {
                    reason = FcuLocKey.log_import_failed_incompatible.Localize(
                        $"{nameof(UIFramework)}.{monoBeh.Settings.MainSettings.UIFramework}",
                        $"{nameof(LocalizationComponent)}.{monoBeh.Settings.TextFontsSettings.TextComponent}");
                    result = false;
                }
                else if (monoBeh.UsingUIBlock2D())
                {
                    if (!monoBeh.IsNova())
                    {
                        reason = FcuLocKey.log_import_failed_enable_required.Localize(
                            $"{nameof(UIFramework)}.{UIFramework.NOVA}",
                            $"{nameof(ImageComponent)}.{ImageComponent.UIBlock2D}");
                        result = false;
                    }
                    else if (monoBeh.UsingSVG())
                    {
                        reason = FcuLocKey.log_import_failed_incompatible.Localize(
                            $"{nameof(ImageComponent)}.{ImageComponent.UIBlock2D}",
                            $"{nameof(ImageFormat)}.{ImageFormat.SVG}");
                        result = false;
                    }
                }
                else if (monoBeh.UsingSvgImage())
                {
                    if (!monoBeh.IsUGUI())
                    {
                        reason = FcuLocKey.log_import_failed_enable_required.Localize(
                            $"{nameof(UIFramework)}.{UIFramework.UGUI}",
                            $"{nameof(ImageComponent)}.{ImageComponent.SvgImage}");
                        result = false;
                    }
                    else if (!monoBeh.UsingSVG())
                    {
                        reason = FcuLocKey.log_import_failed_enable_required.Localize(
                            $"{nameof(ImageFormat)}.{ImageFormat.SVG}",
                            $"{nameof(ImageComponent)}.{ImageComponent.SvgImage}");
                        result = false;
                    }
                }
                else if (!monoBeh.UsingSvgImage())
                {
                    if (monoBeh.UsingSVG())
                    {
                        reason = FcuLocKey.log_import_failed_unsupported.Localize(
                            $"{nameof(ImageComponent)}.{monoBeh.Settings.ImageSpritesSettings.ImageComponent}",
                            $"{nameof(ImageFormat)}.{ImageFormat.SVG}");
                        result = false;
                    }
                }
            }

            return result.ToBoolNullTrue();
        }
    }

    public enum ImportStatus
    {
        ImportSuccess,
        Stopped,
        RateLimit,
        CantAuthorize,
        Exception,
        Technical,
        ProjectDownloadSuccess,

        ImportStepSuccess,
        ImportStepFailed
    }

    public struct ImportResult
    {
        public ImportStatus Status { get; set; }
        public string Message { get; set; }
        public int NextStep { get; set; }

        public static ImportResult Return(ImportStatus status, string message, int nextStep)
        {
            return new ImportResult
            {
                Status = status,
                Message = message, 
                NextStep = nextStep
            };
        }
    }
}