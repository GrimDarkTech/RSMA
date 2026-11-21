using System;
using System.Threading.Tasks;
using RSMA.SBC;
using SFB;
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

        [MenuItem("RSMA/SBC SITL")]
        public static void ShowWindow()
        {
            var window = GetWindow<SbcSitlWindow>("SBC SITL");
            window.minSize = new Vector2(420f, 280f);
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

            var sitl = _device.Sitl;
            var connection = _device.Connection;
            EditorGUILayout.LabelField("VirtualBox", EditorStyles.boldLabel);
            sitl.virtualMachineName = EditorGUILayout.TextField("VM name", sitl.virtualMachineName);
            sitl.bridgeAdapterName = EditorGUILayout.TextField("Bridge adapter", sitl.bridgeAdapterName);
            sitl.vboxManagePath = EditorGUILayout.TextField("VBoxManage path", sitl.vboxManagePath);

            EditorGUILayout.LabelField("RSMA LAN endpoint", EditorStyles.boldLabel);
            connection.endpoint = EditorGUILayout.TextField("Host address", connection.endpoint);
            EditorGUILayout.BeginHorizontal();
            connection.commandPort = EditorGUILayout.IntField("Command", connection.commandPort);
            connection.publishPort = EditorGUILayout.IntField("Publish", connection.publishPort);
            connection.subscribePort = EditorGUILayout.IntField("Subscribe", connection.subscribePort);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Session", _device.State.ToString());
            EditorGUILayout.LabelField("SITL", _device.SitlState.ToString());
            EditorGUILayout.LabelField("VM", _vmState.ToString());

            if (!string.IsNullOrEmpty(_device.LastError))
                EditorGUILayout.HelpBox(_device.LastError, MessageType.Error);
            if (!string.IsNullOrEmpty(_device.LastSitlError))
                EditorGUILayout.HelpBox(_device.LastSitlError, MessageType.Error);
            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Info);

            var validConfiguration = TryValidateConfiguration(out var validationError);
            if (!validConfiguration)
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);

            using (new EditorGUI.DisabledScope(_pendingOperation != null || !Application.isPlaying || !validConfiguration))
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

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Choose VBoxManage.exe"))
                {
                    var paths = StandaloneFileBrowser.OpenFilePanel(
                        "Select VBoxManage.exe",
                        string.Empty,
                        "exe",
                        false);
                    if (paths != null && paths.Length > 0)
                    {
                        _device.Sitl.vboxManagePath = paths[0];
                        _message = "VBoxManage path updated.";
                    }
                }
            }

            if (_pendingOperation != null)
                Repaint();
        }

        private bool TryValidateConfiguration(out string error)
        {
            try
            {
                _device.ValidateConfiguration();
                _device.Sitl.Validate();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
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