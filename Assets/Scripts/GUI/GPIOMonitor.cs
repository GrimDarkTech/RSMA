using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class GPIOMonitor : EditorWindow
{

    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("RSMA/GPIO Monitor")]
    public static void ShowExample()
    {
        GPIOMonitor wnd = GetWindow<GPIOMonitor>();
        wnd.titleContent = new GUIContent("RSMA GPIO Monitor");

        wnd.minSize = new Vector2(450, 200);
        wnd.maxSize = new Vector2(1920, 720);
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
    }
}
