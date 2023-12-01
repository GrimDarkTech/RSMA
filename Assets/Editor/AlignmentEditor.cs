using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class AlignmentEditor : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("RSMA/Alignment Editor")]
    public static void ShowExample()
    {
        AlignmentEditor wnd = GetWindow<AlignmentEditor>();
        wnd.titleContent = new GUIContent("Alignment Editor");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
      //  VisualElement label = new Label("Hello World! From C#");
       // root.Add(label);

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);
    }
}
