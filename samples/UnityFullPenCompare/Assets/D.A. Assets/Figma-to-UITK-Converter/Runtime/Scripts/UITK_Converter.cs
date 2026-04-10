#if FCU_EXISTS
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Threading;

#if ULB_EXISTS
using DA_Assets.ULB;
#endif

#pragma warning disable IDE0003

namespace DA_Assets.FCU
{
    [Serializable]
    public class UITK_Converter : FcuBase, IUitkConverter
    {
        public override void Init(FigmaConverterUnity monoBeh)
        {
            base.Init(monoBeh);

            this.UxmlCreator.Init(monoBeh);
            this.BaseStyleBuilder.Init(monoBeh);
#if ULB_EXISTS
            this.ComponentDrawer.Init(monoBeh);
#endif
        }

        public async Task Convert(FObject virtualPage, List<FObject> currPage, CancellationToken token)
        {
            BaseStyleBuilder.ClearStyles();

            // Clear the hash-based template registry before each import.
            this.UxmlCreator.ComponentTemplateWriter.Clear();

            await Task.Run(() =>
            {
                this.UxmlCreator.Draw(virtualPage);

            }, token);

            monoBeh.CurrentProject.SetRootFrames(currPage, token);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

#if ULB_EXISTS
            if (monoBeh.Settings.UITK_Settings.UitkLinkingMode != UitkLinkingMode.None)
            {
                monoBeh.CanvasDrawer.LocalizationDrawer.LocalizationDictionary.Clear();
                this.ComponentDrawer.Draw(virtualPage, currPage);
                monoBeh.CanvasDrawer.LocalizationDrawer.SaveAndConnectTable(token);
            }
#endif
        }

        [SerializeField] public UxmlCreator UxmlCreator = new UxmlCreator();
        [SerializeField] public BaseStyleBuilder BaseStyleBuilder = new BaseStyleBuilder();
#if ULB_EXISTS
        [SerializeField] public ComponentDrawer ComponentDrawer = new ComponentDrawer();
#endif
    }
}
#endif