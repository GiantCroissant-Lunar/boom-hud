#if false
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Text;

namespace DA_Assets.FCU
{
    internal class ScriptCreator
    {
        private static string _baseClass = @"
using System;
using UnityEngine;
using UnityEngine.UIElements;
using DA_Assets.FCU.UI;

namespace MyNameSpace
{{
    public class {0} : MonoBehaviour
    {{
{1}

{2}
    }}
}}
";
        //0 - class name
        //1 - [SerializeField] private VisualElement uiButton;
        //2 - uiButton = GetElement(""btn-Account"");
        //3 - uiButton.RegisterCallback<MouseUpEvent>(OnClick4);
        /*4 - private void OnClick4(MouseUpEvent evt)
        {{
            Debug.LogError($""clicked"");
        }}*/
        public static string CreateStringScript(string projectName, List<FObject> fobjects)
        {
            string fields = GetFields(fobjects);
            string voids = SetCallbacksVoids(fobjects);

            string script = string.Format(_baseClass, projectName, fields, voids);
            return script;
        }

        private static string GetFields(List<FObject> fobjects)
        {
            StringBuilder elemsSb = new StringBuilder();
            StringBuilder labelsSb = new StringBuilder();

            foreach (FObject fobject in fobjects)
            {
                if (fobject.Data.UitkType == "Label")
                {
                   // labelsSb.AppendLine($"        [SerializeField] {nameof(UitkLabel)} {fobject.Data.FieldName}Label;");
                }
                else
                {
                  //  elemsSb.AppendLine($"        [SerializeField] {nameof(UitkVisualElement)} {fobject.Data.FieldName};");
                }
            }

            return $"{elemsSb}\n{labelsSb}";
        }

        private static string SetCallbacksVoids(List<FObject> fobjects)
        {
            StringBuilder sb = new StringBuilder();

            foreach (FObject fobject in fobjects)
            {
                if (fobject.ContainsTag(FcuTag.Button))
                {
                    string mehtodName = fobject.Data.MethodName;

                    sb.AppendLine($@"
        private void {mehtodName}_OnClick(MouseUpEvent evt)
        {{
            Debug.Log($""{fobject.Data.ObjectName} clicked."");
        }}");
                }
            }

            return sb.ToString();
        }
    }
}
#endif
