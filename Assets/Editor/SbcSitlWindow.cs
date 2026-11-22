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
    public sealed class SbcSitlWindow : EditorWindow
    {
        private SbcDevice _device;
        private SbcVirtualMachineState _vmState = SbcVirtualMachineState.Unknown;
        private string _message = "Select an SbcDevice in the open scene.";
        private Task _pendingOperation;
        private readonly System.Collections.Generic.List<SbcVirtualMachineInfo> _machines = new System.Collections.Generic.List<SbcVirtualMachineInfo>();
        private readonly List<SbcHostEndpointOption> _hostEndpoints = new List<SbcHostEndpointOption>();
        private string _hostEndpointStatus = string.Empty;

        [MenuItem("RSMA/SBC SITL")]
        public static void ShowWindow()
        {
            var window = GetWindow<SbcSitlWindow>("SBC SITL");
            window.minSize = new Vector2(460f, 360f);
            window.RefreshHostEndpoints();
        }

        private void OnEnable()
        {
            RefreshHostEndpoints();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SBC SITL", EditorStyles.boldLabel);
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
                DrawHostEndpoint(connection.FindPropertyRelative("endpoint"));
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
            var selectedIndex = EditorGUILayout.Popup("RSMA host endpoint", currentIndex, labels);
            endpoint.stringValue = _hostEndpoints[selectedIndex].Endpoint;
            EditorGUILayout.LabelField("Network interface", _hostEndpoints[selectedIndex].InterfaceName, EditorStyles.miniLabel);

            if (GUILayout.Button("Refresh host addresses"))
                RefreshHostEndpoints(endpoint.stringValue);

            if (!string.IsNullOrEmpty(_hostEndpointStatus))
                EditorGUILayout.HelpBox(_hostEndpointStatus, MessageType.Info);
        }

        private void RefreshHostEndpoints(string currentEndpoint = null)
        {
            var endpoint = currentEndpoint ?? (_device == null ? string.Empty : _device.Connection.endpoint);
            _hostEndpoints.Clear();
            _hostEndpoints.AddRange(GetHostEndpointOptions(endpoint));
            _hostEndpointStatus = _hostEndpoints.Count == 0
                ? "No IPv4 host addresses were detected. Enter the RSMA host endpoint manually."
                : $"Detected {_hostEndpoints.Count} IPv4 host address(es).";
        }

        public sealed class SbcHostEndpointOption
        {
            public string InterfaceName { get; }
            public string Address { get; }
            public string Endpoint => $"tcp://{Address}";
            public string Label => Address;

            public SbcHostEndpointOption(string interfaceName, string address)
            {
                InterfaceName = interfaceName;
                Address = address;
            }
        }

        public static IReadOnlyList<SbcHostEndpointOption> GetHostEndpointOptions(string currentEndpoint = null)
        {
            var options = new List<SbcHostEndpointOption>();
            try
            {
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var properties = networkInterface.GetIPProperties();
                    foreach (var address in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork ||
                            IPAddress.IsLoopback(address.Address) ||
                            address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                            continue;

                        options.Add(new SbcHostEndpointOption(networkInterface.Name, address.Address.ToString()));
                    }
                }
            }
            catch (NetworkInformationException)
            {
            }

            if (options.Count == 0)
            {
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var address in host.AddressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                            options.Add(new SbcHostEndpointOption("Host DNS", address.ToString()));
                    }
                }
                catch
                {
                }
            }

            var currentHost = TryGetEndpointHost(currentEndpoint);
            if (!string.IsNullOrEmpty(currentHost) &&
                !options.Any(option => string.Equals(option.Address, currentHost, StringComparison.OrdinalIgnoreCase)))
                options.Insert(0, new SbcHostEndpointOption("Current/manual", currentHost));

            return options
                .GroupBy(option => option.Endpoint, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(option => option.InterfaceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(option => option.Address, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string TryGetEndpointHost(string endpoint)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return uri.Host;
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