using System.IO;
using RSMA.SBC;
using UnityEditor;
using UnityEngine;

namespace RSMA.EditorTools
{
    public sealed class SbcVirtualBoxSettingsWindow : EditorWindow
    {
        private SbcVirtualBoxGlobalSettings _settings;
        private string _message;

        [MenuItem("RSMA/SBC VirtualBox Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<SbcVirtualBoxSettingsWindow>("SBC VirtualBox Settings");
            window.minSize = new Vector2(460f, 240f);
            window.LoadSettings();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnGUI()
        {
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Global VirtualBox settings are editable only in EditMode.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Global VirtualBox settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These values are shared by all SBC devices and are stored in ProjectSettings.", MessageType.Info);

            _settings.vboxManagePath = EditorGUILayout.TextField("VBoxManage.exe", _settings.vboxManagePath);
            _settings.defaultGuestProfile = (SbcGuestProfile)EditorGUILayout.EnumPopup("Default guest OS", _settings.defaultGuestProfile);
            _settings.defaultBridgeAdapterName = EditorGUILayout.TextField("Default bridge adapter", _settings.defaultBridgeAdapterName);
            _settings.defaultStartupTimeoutSeconds = EditorGUILayout.IntField("Default startup timeout", _settings.defaultStartupTimeoutSeconds);
            _settings.defaultShutdownTimeoutSeconds = EditorGUILayout.IntField("Default shutdown timeout", _settings.defaultShutdownTimeoutSeconds);
            _settings.defaultCommandTimeoutSeconds = EditorGUILayout.IntField("Default command timeout", _settings.defaultCommandTimeoutSeconds);
            _settings.enableSitlLogs = EditorGUILayout.Toggle("Enable SBC SITL logs", _settings.enableSitlLogs);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Choose VBoxManage.exe"))
            {
                var path = EditorUtility.OpenFilePanel("Select VBoxManage.exe", string.Empty, "exe");
                if (!string.IsNullOrEmpty(path))
                    _settings.vboxManagePath = path;
            }
            if (GUILayout.Button("Save settings"))
                SaveSettings();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Info);
        }

        private void LoadSettings()
        {
            _settings = SbcVirtualBoxGlobalSettings.Load();
            _message = File.Exists(SbcVirtualBoxGlobalSettings.SettingsPath)
                ? "Settings loaded from ProjectSettings."
                : "No settings file found; defaults are in use.";
        }

        private void SaveSettings()
        {
            _settings.defaultStartupTimeoutSeconds = Mathf.Max(1, _settings.defaultStartupTimeoutSeconds);
            _settings.defaultShutdownTimeoutSeconds = Mathf.Max(1, _settings.defaultShutdownTimeoutSeconds);
            _settings.defaultCommandTimeoutSeconds = Mathf.Max(1, _settings.defaultCommandTimeoutSeconds);
            var directory = Path.GetDirectoryName(SbcVirtualBoxGlobalSettings.SettingsPath);
            Directory.CreateDirectory(directory);
            File.WriteAllText(SbcVirtualBoxGlobalSettings.SettingsPath, JsonUtility.ToJson(_settings, true));
            SbcSitlLog.SetEnabled(_settings.enableSitlLogs);
            AssetDatabase.Refresh();
            _message = "Settings saved.";
        }
    }
}