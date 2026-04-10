#if FCU_EXISTS && ULB_EXISTS
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using DA_Assets.ULB;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#if DALOC_EXISTS
using DA_Assets.DAL;
#endif

namespace DA_Assets.FCU
{
    [Serializable]
    public class ComponentDrawer : FcuBase
    {
        private void DrawGameObjects(FObject parent)
        {
            foreach (FObject fobject in parent.Children)
            {
                if (fobject.Data.IsEmpty)
                    continue;

                SyncHelper syncHelper = MonoBehExtensions.CreateEmptyGameObject().AddComponent<SyncHelper>();
                fobject.SetData(syncHelper, monoBeh);
                fobject.Data.GameObject.name = fobject.Data.Names.ObjectName;
                fobject.Data.GameObject.transform.SetParent(parent.Data.GameObject.transform);
                monoBeh.Events.OnObjectInstantiate?.Invoke(monoBeh, fobject);

                if (fobject.IsMask.ToBoolNullFalse())
                    continue;

                if (fobject.Children.IsEmpty())
                    continue;

                DrawGameObjects(fobject);
            }

        }

        public void Draw(FObject virtualPage, List<FObject> fobjects)
        {
            DrawGameObjects(virtualPage);

            foreach (FObject fobject in fobjects)
            {
                if (fobject.Data.GameObject == null)
                {
                    Debug.LogWarning(FuitkLocKey.log_gameobject_missing.Localize(fobject.Data.NameHierarchy));
                    continue;
                }

                SyncHelper syncHelper = fobject.Data.GameObject.GetComponent<SyncHelper>();

                if (syncHelper == null)
                {
                    Debug.LogError(FuitkLocKey.log_sync_helper_missing.Localize(fobject.Data.NameHierarchy));
                    continue;
                }

                if (fobject.ContainsTag(FcuTag.Frame))
                {
#if UNITY_EDITOR && UNITY_2021_3_OR_NEWER
                    syncHelper.Data.GameObject.TryAddComponent(out UIDocument uiDocument);
                    syncHelper.Data.UIDocument = uiDocument;

                    VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(fobject.Data.Names.UxmlPath);
                    uiDocument.visualTreeAsset = visualTree;
#endif
                }
                else
                {
                    if (fobject.ContainsTag(FcuTag.Text))
                    {
                        syncHelper.Data.GameObject.TryAddComponent(out UitkLabel uitkLinker);

                        if (monoBeh.Settings.LocalizationSettings.LocalizationComponent == LocalizationComponent.DALocalizator)
                        {
                            string locKey = fobject.Data.Names.LocKey;
                            string text = fobject.GetText();

                            if (!locKey.IsEmpty() && !text.IsEmpty())
                            {
                                monoBeh.CanvasDrawer.LocalizationDrawer.LocalizationDictionary.TryAddValue(locKey, text);

#if DALOC_EXISTS
                                syncHelper.Data.GameObject.TryAddComponent(out UitkLocalizator uitkLocalizator);
                                uitkLocalizator.Key = locKey;
#endif
                            }
                        }
                    }
                    else if (fobject.ContainsTag(FcuTag.Button))
                    {
                        syncHelper.Data.GameObject.TryAddComponent(out UitkButton uitkLinker);
                    }
                    else
                    {
                        syncHelper.Data.GameObject.TryAddComponent(out UitkVisualElement uitkLinker);
                    }

                    fobject.Data.GameObject.TryGetComponent(out UitkLinkerBase @base);
#if UNITY_2021_3_OR_NEWER
                    @base.UIDocument = fobject.Data.RootFrame.UIDocument;
#endif
                    @base.LinkingMode = monoBeh.Settings.UITK_Settings.UitkLinkingMode;

                    Link(@base, syncHelper.Data);
                }
            }
        }

        private void Link(UitkLinkerBase uitkLinker, SyncData syncHelper)
        {
            if (monoBeh.Settings.UITK_Settings.UitkLinkingMode == UitkLinkingMode.Guid)
            {
                uitkLinker.Guid = syncHelper.Names.UitkGuid;
                uitkLinker.Guids = syncHelper.Hierarchy.Select(x => x.Guid).ToArray();
            }
            else
            {
                uitkLinker.Name = syncHelper.Hierarchy.Last().Name;
                uitkLinker.Names = syncHelper.Hierarchy.Select(x => new ElementIndexName
                {
                    Name = x.Name,
                    Index = x.Index
                }).ToArray();
            }
        }
    }
}
#endif
