using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using System;

namespace DA_Assets.FCU
{
    [Serializable]
    public class FolderCreator : FcuBase
    {
        public void CreateAll()
        {
            monoBeh.FontLoader.TtfFontsPath.CreateFolderIfNotExists();
            monoBeh.FontLoader.TmpFontsPath.CreateFolderIfNotExists();
            monoBeh.FontLoader.UitkFontAssetsPath.CreateFolderIfNotExists();
            monoBeh.FontLoader.UniTextFontsPath.CreateFolderIfNotExists();

            if (monoBeh.IsUITK())
            {
                monoBeh.Settings.UITK_Settings.UitkOutputPath.CreateFolderIfNotExists();
            }

            monoBeh.Settings.ImageSpritesSettings.SpritesPath.CreateFolderIfNotExists();
            monoBeh.Settings.ScriptGeneratorSettings.OutputPath.CreateFolderIfNotExists();
            monoBeh.Settings.PrefabSettings.PrefabsPath.CreateFolderIfNotExists();
        }
    }
}
