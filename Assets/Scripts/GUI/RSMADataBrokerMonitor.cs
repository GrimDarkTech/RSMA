using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
public class RSMADataBrokerMonitor : EditorWindow
{
    private Vector2 scrollPos;
    private bool autoRefresh = true;
    private string searchFilter = "";

    private Dictionary<string, bool> topicFoldouts = new Dictionary<string, bool>();

    [MenuItem("RSMA/DataBroker Monitor")]
    public static void ShowWindow()
    {
        RSMADataBrokerMonitor wnd = GetWindow<RSMADataBrokerMonitor>();
        wnd.titleContent = new GUIContent("RSMA DataBroker Monitor", EditorGUIUtility.IconContent("Console.InfoIcon").image);
        wnd.minSize = new Vector2(500, 300);
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (autoRefresh && Application.isPlaying)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Запустите Play Mode для мониторинга топиков DataBroker в реальном времени.", MessageType.Info);
            return;
        }

        var topics = DataBroker.GetActiveTopics();

        if (topics == null || topics.Count == 0)
        {
            EditorGUILayout.HelpBox("Активные топики uDTP не найдены или DataBroker не получает данные из сети.", MessageType.Warning);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var topicName in topics)
        {
            if (!string.IsNullOrEmpty(searchFilter) && !topicName.ToLower().Contains(searchFilter.ToLower()))
                continue;

            DrawTopicCard(topicName);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        autoRefresh = GUILayout.Toggle(autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(90));

        GUILayout.Space(10);
        GUILayout.Label("Search:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Clear Cache", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            topicFoldouts.Clear();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTopicCard(string topicName)
    {
        if (!topicFoldouts.ContainsKey(topicName))
            topicFoldouts[topicName] = true;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Получаем raw-данные из брокера
        object rawState = DataBroker.GetRawState(topicName);

        // Если пришел байтовый массив от Python, пробуем распаковать его в ActuatorInputs или другую структуру
        if (rawState is byte[] rawBytes)
        {
            // Пробуем известную типизацию для ActuatorInputs
            if (topicName.StartsWith("ActuatorInputs") && rawBytes.Length >= Marshal.SizeOf<ActuatorInputs>())
            {
                rawState = DataBroker.GetStateAsType(topicName, typeof(ActuatorInputs));
            }
        }

        EditorGUILayout.BeginHorizontal();

        topicFoldouts[topicName] = EditorGUILayout.Foldout(topicFoldouts[topicName], $"Topic: {topicName}", true, EditorStyles.foldoutHeader);

        if (rawState != null)
        {
            Type dataType = rawState.GetType();
            if (dataType.IsArray && dataType.GetElementType() == typeof(byte))
            {
                int len = ((byte[])rawState).Length;
                GUILayout.Label($"[Raw Bytes | {len} Bytes]", EditorStyles.miniBoldLabel, GUILayout.Width(180));
            }
            else
            {
                int dataSize = Marshal.SizeOf(dataType);
                GUILayout.Label($"[{dataType.Name} | {dataSize} Bytes]", EditorStyles.miniBoldLabel, GUILayout.Width(180));
            }
        }
        else
        {
            GUILayout.Label("[No Data / Empty]", EditorStyles.miniLabel, GUILayout.Width(120));
        }

        EditorGUILayout.EndHorizontal();

        if (topicFoldouts[topicName] && rawState != null)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);

            if (rawState is byte[] bytes)
            {
                EditorGUILayout.LabelField("Hex Payload:", BitConverter.ToString(bytes, 0, Mathf.Min(bytes.Length, 32)));
            }
            else
            {
                DrawObjectFields(rawState);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawObjectFields(object obj)
    {
        if (obj == null) return;

        Type type = obj.GetType();
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            object value = field.GetValue(obj);
            string fieldName = field.Name;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(fieldName, GUILayout.Width(150));

            if (value == null)
            {
                EditorGUILayout.TextField("null");
            }
            else if (field.FieldType == typeof(float))
            {
                EditorGUILayout.FloatField((float)value);
            }
            else if (field.FieldType == typeof(double))
            {
                EditorGUILayout.DoubleField((double)value);
            }
            else if (field.FieldType == typeof(int))
            {
                EditorGUILayout.IntField((int)value);
            }
            else if (field.FieldType == typeof(long))
            {
                EditorGUILayout.LongField((long)value);
            }
            else if (field.FieldType.IsArray)
            {
                Array array = (Array)value;
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"Array[{array.Length}]", EditorStyles.miniLabel);

                for (int i = 0; i < Mathf.Min(array.Length, 16); i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  [{i}]", GUILayout.Width(50));
                    EditorGUILayout.TextField(array.GetValue(i)?.ToString() ?? "0");
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.TextField(value.ToString());
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif