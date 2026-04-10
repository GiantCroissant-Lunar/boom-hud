using DA_Assets.Extensions;
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Helper class for layout update operations during import.
    /// Extracted to avoid circular dependencies between strategies and importers.
    /// </summary>
    public static class LayoutUpdaterHelper
    {
        public static async Task<List<FObject>> ShowLayoutUpdaterWindow(
            FigmaConverterUnity monoBeh,
            SyncHelper[] syncHelpers,
            List<FObject> currentPage,
            CancellationToken token)
        {
            Debug.Log(FcuLocKey.log_import_show_difference_checker.Localize());

            monoBeh.SyncHelpers.RestoreRootFrames(syncHelpers);

            LayoutUpdaterInput lui = await monoBeh.LayoutUpdateDataCreator
                .Create(currentPage, syncHelpers.ToList(), token);

            LayoutUpdaterOutput luo = default;

            await monoBeh.AssetTools.ReselectFcu(token);
            monoBeh.EditorDelegateHolder.ShowDifferenceChecker(lui, _ => luo = _);

            while (luo.IsDefault())
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token);
            }

            List<FObject> tempPage = new List<FObject>();

            foreach (string item in luo.ToImport)
            {
                token.ThrowIfCancellationRequested();

                foreach (FObject fobject in currentPage)
                {
                    token.ThrowIfCancellationRequested();

                    if (item == fobject.Id)
                    {
                        tempPage.Add(fobject);
                    }
                }
            }

            await monoBeh.CanvasDrawer.GameObjectDrawer.DestroyMissing(luo.ToRemove, token);

            return tempPage;
        }
    }
}
