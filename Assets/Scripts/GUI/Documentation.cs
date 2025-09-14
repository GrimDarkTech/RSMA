using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR

public class Documentation : EditorWindow
{
    [MenuItem("RSMA/Help")]
    public static void ShowExample()
    {
        Documentation wnd = GetWindow<Documentation>();
        wnd.titleContent = new GUIContent("Useful links");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        //RSMA website
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(4);
        GUILayout.Label("RSMA website");
        if (GUILayout.Button("Open in browser"))
        {
            Application.OpenURL("https://grimdark.ru/");
        }

        EditorGUILayout.EndHorizontal();

        //Techcontent
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(4);
        GUILayout.Label("Buy RSMA training course");
        if (GUILayout.Button("Open in browser"))
        {
            Application.OpenURL("https://tech-content.ru/");
        }

        EditorGUILayout.EndHorizontal();

        //Docs
        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("Documentation repository");

        if (GUILayout.Button("Open in browser"))
        {
            Application.OpenURL("https://github.com/GrimDarkTech/RSMADocs/");
        }
        EditorGUILayout.EndHorizontal();

        //Manual
        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("Manual");

        if (GUILayout.Button("Open in browser"))
        {
            Application.OpenURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/Manual/ru/Manual.md");
        }

        EditorGUILayout.EndHorizontal();

        //Scripting
        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("ScriptingAPI");

        if (GUILayout.Button("Open in browser"))
        {
            Application.OpenURL("https://github.com/GrimDarkTech/RSMADocs/blob/main/ScriptingAPI/ru/ScriptingAPI.md");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();



    }
}

#endif