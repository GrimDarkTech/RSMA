using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class Documentation : EditorWindow
{
    [MenuItem("RSMA/Documentation")]
    public static void ShowExample()
    {
        Documentation wnd = GetWindow<Documentation>();
        wnd.titleContent = new GUIContent("Documentation");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(2);
        GUILayout.Label("Documentation repository");

        if (GUILayout.Button("Open in browser"))
        {
            Application.OpenURL("https://github.com/GrimDarkTech/RSMADocs");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(2);
        GUILayout.Label("Manual");

        if (GUILayout.Button("Open in browser"))
        {
            Application.OpenURL("https://github.com/GrimDarkTech/RSMADocs/tree/main/Manual");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();



    }
}
