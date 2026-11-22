using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using RSMA.SBC;
using UnityEditor;
using UnityEngine;

namespace RSMA.EditorTools
{
    public sealed class SbcHitlWindow : EditorWindow
    {
        private SbcDevice _device;
        private string _message = "Select an SbcDevice configured for HITL.";
        private Task _pendingOperation;
        private readonly List<SbcSitlWindow.SbcHostEndpointOption> _hostEndpoints = new List<SbcSitlWindow.SbcHostEndpointOption>();
        private string _hostEndpointStatus = string.Empty;

        [MenuItem("RSMA/SBC HITL")]
        public static void ShowWindow()
        {
            var window = GetWindow<SbcHitlWindow>("SBC HITL");
            window.minSize = new Vector2(440f, 300f);
            window.RefreshHostEndpoints();
        }

        private void OnEnable()
        {
            RefreshHostEndpoints();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SBC HITL", EditorStyles.boldLabel);
            var selectedDevice = EditorGUILayout.ObjectField("SBC device", _device, typeof(SbcDevice), true) as SbcDevice;
            if (selectedDevice != _device)
            {
                _device = selectedDevice;
                RefreshHostEndpoints();
            }

            if (_device == null)
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
                return;
            }

            DrawEditModeConfiguration();
            if (Application.isPlaying)
                DrawRuntimeControls();
            else
                EditorGUILayout.HelpBox("The physical SBC is powered and shut down by the user. PlayMode only opens/closes the RSMA network session.", MessageType.Info);

            if (!string.IsNullOrEmpty(_device.LastError))
                EditorGUILayout.HelpBox(_device.LastError, MessageType.Error);
            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Info);

            if (_pendingOperation != null)
                Repaint();
        }

        private void DrawEditModeConfiguration()
        {
            var serializedDevice = new SerializedObject(_device);
            serializedDevice.Update();
            var connection = serializedDevice.FindProperty("connection");
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.LabelField("HITL device configuration (EditMode)", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedDevice.FindProperty("nodeId"), new GUIContent("Node ID"));
                EditorGUILayout.PropertyField(serializedDevice.FindProperty("startOnEnable"), new GUIContent("Start network session in PlayMode"));
                DrawHostEndpoint(connection.FindPropertyRelative("endpoint"));
                EditorGUILayout.PropertyField(connection.FindPropertyRelative("commandPort"), new GUIContent("Command port"));
                EditorGUILayout.PropertyField(connection.FindPropertyRelative("publishPort"), new GUIContent("Publish port"));
                EditorGUILayout.PropertyField(connection.FindPropertyRelative("subscribePort"), new GUIContent("Subscribe port"));

                if (EditorGUI.EndChangeCheck())
                    Undo.RecordObject(_device, "Edit SBC HITL configuration");
                serializedDevice.ApplyModifiedProperties();

                if (_device.Mode != SbcSimulationMode.Hitl)
                {
                    EditorGUILayout.HelpBox("This component is currently configured for SITL. Switch its Mode to HITL in the Inspector before using this window.", MessageType.Warning);
                    if (GUILayout.Button("Set mode to HITL"))
                    {
                        Undo.RecordObject(_device, "Set SBC mode to HITL");
                        _device.SetMode(SbcSimulationMode.Hitl);
                        EditorUtility.SetDirty(_device);
                    }
                }
            }
        }

        private void DrawHostEndpoint(SerializedProperty endpoint)
        {
            if (_hostEndpoints.Count == 0)
            {
                EditorGUILayout.PropertyField(endpoint, new GUIContent("RSMA host endpoint"));
                if (GUILayout.Button("Refresh host addresses"))
                    RefreshHostEndpoints(endpoint.stringValue);
                if (!string.IsNullOrEmpty(_hostEndpointStatus))
                    EditorGUILayout.HelpBox(_hostEndpointStatus, MessageType.Info);
                return;
            }

            var currentIndex = _hostEndpoints.FindIndex(option =>
                string.Equals(option.Endpoint, endpoint.stringValue, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
                currentIndex = 0;
            var labels = _hostEndpoints.Select(option => option.Label).ToArray();
            var selected = EditorGUILayout.Popup("RSMA host endpoint", currentIndex, labels);
            endpoint.stringValue = _hostEndpoints[selected].Endpoint;
            EditorGUILayout.LabelField("Network interface", _hostEndpoints[selected].InterfaceName, EditorStyles.miniLabel);

            if (GUILayout.Button("Refresh host addresses"))
                RefreshHostEndpoints(endpoint.stringValue);
            if (!string.IsNullOrEmpty(_hostEndpointStatus))
                EditorGUILayout.HelpBox(_hostEndpointStatus, MessageType.Info);
        }

        private void RefreshHostEndpoints(string currentEndpoint = null)
        {
            var endpoint = currentEndpoint ?? (_device == null ? string.Empty : _device.Connection.endpoint);
            _hostEndpoints.Clear();
            _hostEndpoints.AddRange(SbcSitlWindow.GetHostEndpointOptions(endpoint));
            _hostEndpointStatus = _hostEndpoints.Count == 0
                ? "No IPv4 host addresses were detected. Enter the RSMA host endpoint manually."
                : $"Detected {_hostEndpoints.Count} IPv4 host address(es).";
        }

        private void DrawRuntimeControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Session", _device.State.ToString());
            using (new EditorGUI.DisabledScope(_pendingOperation != null || _device.Mode != SbcSimulationMode.Hitl))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Connect to host"))
                    BeginOperation(StartSessionAsync());
                if (GUILayout.Button("Disconnect"))
                {
                    _device.StopHitlSession();
                    _message = "Unity network session stopped. The physical SBC remains powered on.";
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private async Task StartSessionAsync()
        {
            await _device.StartHitlSessionAsync();
            _message = "Unity network session started. Connect the powered-on SBC to the configured host endpoint.";
        }

        private void BeginOperation(Task operation)
        {
            _message = "Operation in progress...";
            _pendingOperation = operation;
            operation.ContinueWith(completed =>
            {
                _pendingOperation = null;
                if (completed.IsFaulted)
                    _message = completed.Exception?.GetBaseException().Message ?? "HITL session operation failed.";
                Repaint();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}