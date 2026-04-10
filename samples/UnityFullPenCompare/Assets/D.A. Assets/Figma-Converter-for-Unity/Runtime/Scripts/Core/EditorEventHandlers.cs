using System;

namespace DA_Assets.FCU
{
    [Serializable]
    public class EditorEventHandlers : FcuBase
    {
        public static void CreateFcu_OnClick() =>
            AssetTools.CreateFcuOnScene();

        public void Auth_OnClick() =>
            monoBeh.Authorizer.Auth();

        public void DownloadProject_OnClick() =>
          _ = monoBeh.ProjectDownloader.DownloadProject();

        public void ImportSelectedFrames_OnClick() =>
            _ = monoBeh.ProjectImporter.StartImport();

        public void GenerateScripts_OnClick(ScriptGeneratorSelectionContext context) =>
            monoBeh.ScriptGenerator.GenerateScripts(context);

        public void SerializeObjects_OnClick(ScriptGeneratorSelectionContext context) =>
            monoBeh.ScriptGenerator.Serialize(context);

        public void CreatePrefabs_OnClick() =>
            monoBeh.PrefabCreator.CreatePrefabs();

        public void DestroySyncHelpers_OnClick() =>
            monoBeh.SyncHelpers.DestroySyncHelpers();

        public void SetFcuToSyncHelpers_OnClick() =>
            monoBeh.SyncHelpers.SetFcuToAllSyncHelpers();

        public void StopImport_OnClick() =>
            monoBeh.AssetTools.CancelToken();
    }
}
