using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class GPIOMonitor : EditorWindow
{
    private RSMAGPIO gpio;

    private bool isOpened = true;

    private Vector2 scrollPos;



    [MenuItem("RSMA/GPIO Monitor")]
    public static void ShowExample()
    {
        GPIOMonitor wnd = GetWindow<GPIOMonitor>();
        wnd.titleContent = new GUIContent("RSMA GPIO Monitor");

        wnd.minSize = new Vector2(450, 200);
        wnd.maxSize = new Vector2(1920, 720);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("GPIO");
        gpio = EditorGUILayout.ObjectField(gpio, typeof(RSMAGPIO), true) as RSMAGPIO;

        EditorGUILayout.EndHorizontal();

        HeaderDraw();
    }

    private void HeaderDraw()
    {
        isOpened = EditorGUILayout.BeginFoldoutHeaderGroup(isOpened, "Ports");

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (isOpened && gpio != null)
        {
            var ports = gpio.GetPortList();
            if (ports.Count > 0)
            {
                foreach (var port in ports)
                {
                    GUILayout.Label(port.name);
                    EditorGUILayout.BeginVertical();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.Label("Pin name");
                    GUILayout.Label("Pin value");
                    GUILayout.Label("Pin mode");
                    GUILayout.Label("Pin pull mode");

                    EditorGUILayout.EndHorizontal();
                    foreach (var pin in port.pins)
                    {
                        EditorGUILayout.BeginHorizontal();

                        GUILayout.Space(10);
                        pin.name = EditorGUILayout.TextField(pin.name);
                        pin.value = EditorGUILayout.FloatField(pin.value);
                        EditorGUILayout.TextField(pin.pinMode.ToString());
                        EditorGUILayout.TextField(pin.pinPullMode.ToString());

                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

}
