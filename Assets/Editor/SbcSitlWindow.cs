using System;
using System.Threading.Tasks;
using RSMA.SBC;
using UnityEditor;
using UnityEngine;

namespace RSMA.EditorTools
{
    public sealed class SbcSitlWindow : EditorWindow
    {
        private SbcDevice _device;
        private SbcVirtualMachineState _vmState = SbcVirtualMachineState.Unknown;
        private string _message = "Select an SbcDevice in the open scene.";
        private Task _pendingOperation;
        private readonly System.Collections.Generic.List<SbcVirtualMachineInfo> _machines = new System.Collections.Generic.List<SbcVirtualMachineInfo>();

        [MenuItem("RSMA/SBC SITL")]
        public static void ShowWindow()
        {
            var window = GetWindow<SbcSitlWindow>("SBC SITL");
            window.minSize = new Vector2(460f, 360f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SBC SITL", EditorStyles.boldLabel);
            _device = EditorGUILayout.ObjectField("SBC device", _device, typeof(SbcDevice), true) as SbcDevice;

            if (_device == null)
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Auto-start SITL: {(_device.AutoStartSitl ? "Enabled" : "Disabled")}", EditorStyles.miniLabel);
            if (Application.isPlaying && !_device.AutoStartSitl)
                EditorGUILayout.HelpBox("Automatic VM startup is disabled on this component. Enable Auto-start/stop VM in EditMode and save the scene.", MessageType.Warning);

            DrawEditModeConfiguration();
            if (Application.isPlaying)
                DrawRuntimeControls();

            if (_pendingOperation != null)
                Repaint();
        }

        private void DrawEditModeConfiguration()
        {
            var serializedDevice = new SerializedObject(_device);
            serializedDevice.Update();
            var sitl = serializedDevice.FindProperty("sitl");
            var connection = serializedDevice.FindProperty("connection");
            var vmName = sitl.FindPropertyRelative("virtualMachineName");
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.LabelField("SBC configuration (EditMode)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(vmName, new GUIContent("Registered VM"));
                EditorGUILayout.PropertyField(sitl.FindPropertyRelative("displayMode"), new GUIContent("VM window mode"));
                EditorGUILayout.PropertyField(sitl.FindPropertyRelative("bridgeAdapterName"), new GUIContent("Bridge adapter"));
                EditorGUILayout.PropertyField(serializedDevice.FindProperty("autoStartSitl"), new GUIContent("Auto-start/stop VM"));
                EditorGUILayout.PropertyField(connection.FindPropertyRelative("endpoint"), new GUIContent("Host address"));
                EditorGUILayout.PropertyField(connection.FindPropertyRelative("commandPort"), new GUIContent("Command port"));
                EditorGUILayout.PropertyField(connection.FindPropertyRelative("publishPort"), new GUIContent("Publish port"));
                EditorGUILayout.PropertyField(connection.FindPropertyRelative("subscribePort"), new GUIContent("Subscribe port"));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh registered VMs"))
                    BeginOperation(RefreshMachineListAsync());
                if (GUILayout.Button("Add VM from .vbox file"))
                    ImportMachine();
                if (GUILayout.Button("Apply global defaults"))
                {
                    serializedDevice.ApplyModifiedProperties();
                    Undo.RecordObject(_device, "Apply SBC SITL defaults");
                    _device.Sitl.ApplyGlobalDefaults(SbcVirtualBoxGlobalSettings.Load());
                    EditorUtility.SetDirty(_device);
                    serializedDevice.Update();
                }
                EditorGUILayout.EndHorizontal();
                if (_machines.Count > 0)
                {
                    var names = _machines.ConvertAll(machine => machine.Name).ToArray();
                    var selected = Array.IndexOf(names, vmName.stringValue);
                    if (selected < 0) selected = 0;
                    vmName.stringValue = names[EditorGUILayout.Popup("Available VM", selected, names)];
                }
                serializedDevice.ApplyModifiedProperties();
            }
        }

        private void DrawRuntimeControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Session", _device.State.ToString());
            EditorGUILayout.LabelField("SITL", _device.SitlState.ToString());
            EditorGUILayout.LabelField("VM", _vmState.ToString());
            if (!string.IsNullOrEmpty(_device.LastError))
                EditorGUILayout.HelpBox(_device.LastError, MessageType.Error);
            if (!string.IsNullOrEmpty(_device.LastSitlError))
                EditorGUILayout.HelpBox(_device.LastSitlError, MessageType.Error);

            using (new EditorGUI.DisabledScope(_pendingOperation != null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh status"))
                    BeginOperation(RefreshStatusAsync());
                if (GUILayout.Button("Start SITL"))
                    BeginOperation(StartAsync());
                if (GUILayout.Button("Stop SITL"))
                    BeginOperation(StopAsync());
                EditorGUILayout.EndHorizontal();
            }
        }

        private async Task RefreshMachineListAsync()
        {
            var machines = await new SbcVirtualBoxController(_device.Sitl).ListVirtualMachinesAsync();
            _machines.Clear();
            _machines.AddRange(machines);
            _message = $"Found {_machines.Count} registered VirtualBox VM(s).";
        }

        private void ImportMachine()
        {
            var path = EditorUtility.OpenFilePanel("Register VirtualBox machine", string.Empty, "vbox");
            if (!string.IsNullOrEmpty(path))
                BeginOperation(ImportMachineAsync(path));
        }

        private async Task ImportMachineAsync(string path)
        {
            var machine = await new SbcVirtualBoxController(_device.Sitl).ImportVirtualMachineAsync(path);
            Undo.RecordObject(_device, "Select imported VirtualBox VM");
            _device.Sitl.virtualMachineName = machine.Name;
            _machines.Add(machine);
            EditorUtility.SetDirty(_device);
            _message = $"Registered and selected VM '{machine.Name}'.";
        }

        private async Task StartAsync()
        {
            await _device.StartSitlAsync();
            _vmState = SbcVirtualMachineState.Running;
            _message = "SITL started.";
        }

        private async Task StopAsync()
        {
            await _device.StopSitlAsync();
            _vmState = SbcVirtualMachineState.PoweredOff;
            _message = "SITL stopped.";
        }

        private async Task RefreshStatusAsync()
        {
            _vmState = await _device.RefreshSitlStateAsync();
            _message = "VM status refreshed.";
        }

        private void BeginOperation(Task operation)
        {
            _message = "Operation in progress...";
            _pendingOperation = operation;
            operation.ContinueWith(completed =>
            {
                _pendingOperation = null;
                if (completed.IsFaulted)
                    _message = completed.Exception?.GetBaseException().Message ?? "SITL operation failed.";
                Repaint();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}