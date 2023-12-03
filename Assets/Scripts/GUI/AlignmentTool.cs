using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class AlignmentTool : EditorWindow
{
    private GameObject mainObject;
    private GameObject targetObject;

    private Vector3 offset;

    private Vector3 rotation;


    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("RSMA/Alignment Tool")]
    public static void ShowExample()
    {
        AlignmentTool wnd = GetWindow<AlignmentTool>();
        wnd.titleContent = new GUIContent("RSMA Alignment Tool");

        wnd.minSize = new Vector2(450, 200);
        wnd.maxSize = new Vector2(1920, 720);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Main Object");
        mainObject = EditorGUILayout.ObjectField(mainObject, typeof(GameObject), true) as GameObject;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Target Object");
        targetObject = EditorGUILayout.ObjectField(targetObject, typeof(GameObject), true) as GameObject;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        offset = EditorGUILayout.Vector3Field("Offset", offset);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        rotation = EditorGUILayout.Vector3Field("Rotation", rotation);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Align objects"))
        {
            targetObject.transform.position = mainObject.transform.position + offset.x * mainObject.transform.right + offset.y * mainObject.transform.up + offset.z * mainObject.transform.forward;
            targetObject.transform.rotation = mainObject.transform.rotation;
            targetObject.transform.Rotate(rotation);
        }
    }
}
