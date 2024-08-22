using System.IO;
using UnityEngine;

#if UNITY_EDITOR

using UnityEditor;

public class FirmwareCreator: EditorWindow
{
    public static string path;
    private static string filename = "NewFirmware";

    [MenuItem("RSMA/Firmware Creator")]
    public static void ShowWindow()
    {
        GetWindow<FirmwareCreator>("Firmware Creator");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(20);
        GUILayout.Label("Firmware Name");
        filename = EditorGUILayout.TextField(filename);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label("File path: " + path);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Create Firmware"))
        {
            string filepath = Application.dataPath.Replace("Assets", "") + path + "/" + filename + ".cs";
            if (!File.Exists(filepath))
            {
                StreamWriter sw = new StreamWriter(filepath);

                string FirmwareText = "using System.Collections;\r\nusing System.Collections.Generic;\r\nusing UnityEngine;\r\npublic class " + filename + " : MonoBehaviour, IMicrocontollerProgram\r\n{\r\n    public RSMAGPIO GPIO { get; set; }\r\n\r\n    public  RSMADataTransferMaster dataBus { get; set; }\r\n    public IEnumerator MainProgramm()\r\n    {\r\n        yield return new WaitForSeconds(0.1f);\r\n    }\r\n}";
                sw.Write(FirmwareText);
                sw.Close();

                AssetDatabase.Refresh();

                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resoursces/Icons/Firmware.png");

                if (icon != null)
                {
                    var firmwareScript = AssetImporter.GetAtPath(path) as MonoImporter;

                    if(firmwareScript != null)
                    {
                        firmwareScript.SetIcon(icon);
                    }
                }

                AssetDatabase.Refresh();

                
            }
            else
            {
                Debug.Log("File already exists: " + filepath);
            }
        }
    }
}

public class ProjectWindowContextMenu : Editor
{
    [MenuItem("Assets/RSMA/Create Firmware File")]
    private static void ShowPopupWindow()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (!string.IsNullOrEmpty(path))
        {
            FirmwareCreator.ShowWindow();
            FirmwareCreator.path = path;
        }
    }
}

#endif