using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace RSMA.SBC
{
    [Serializable]
    public sealed class SbcVirtualBoxGlobalSettings
    {
        public const string FileName = "RSMA-SbcVirtualBoxSettings.json";

        public string vboxManagePath = string.Empty;
        public string defaultBridgeAdapterName = "";
        public int defaultVirtualCpuCount = 2;
        public int defaultMemoryMegabytes = 4096;
        public int defaultCpuExecutionCapPercent = 100;
        public int defaultStartupTimeoutSeconds = 60;
        public int defaultShutdownTimeoutSeconds = 30;
        public int defaultCommandTimeoutSeconds = 10;
        public bool enableSitlLogs = true;

        public static string SettingsPath => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "ProjectSettings",
            FileName);

        public static SbcVirtualBoxGlobalSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonUtility.FromJson<SbcVirtualBoxGlobalSettings>(json)
                        ?? new SbcVirtualBoxGlobalSettings();
                    if (json.IndexOf("\"enableSitlLogs\"", StringComparison.Ordinal) < 0)
                        settings.enableSitlLogs = true;
                    SbcSitlLog.SetEnabled(settings.enableSitlLogs);
                    return settings;
                }
            }
            catch
            {
                SbcSitlLog.SetEnabled(true);
            }
            var defaults = new SbcVirtualBoxGlobalSettings();
            SbcSitlLog.SetEnabled(defaults.enableSitlLogs);
            return defaults;
        }

        public string ResolveExecutablePath()
        {
            if (!string.IsNullOrWhiteSpace(vboxManagePath))
                return vboxManagePath;
            return Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer
                ? @"C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
                : "VBoxManage";
        }
    }

    public static class SbcSitlLog
    {
        private static bool? _enabled;

        public static bool IsEnabled => _enabled ?? (_enabled = SbcVirtualBoxGlobalSettings.Load().enableSitlLogs).Value;

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public static void Log(string message, UnityEngine.Object context = null)
        {
            if (IsEnabled) UnityEngine.Debug.Log(message, context);
        }

        public static void Warning(string message, UnityEngine.Object context = null)
        {
            if (IsEnabled) UnityEngine.Debug.LogWarning(message, context);
        }

        public static void Error(string message, UnityEngine.Object context = null)
        {
            if (IsEnabled) UnityEngine.Debug.LogError(message, context);
        }
    }

    public enum SbcVirtualBoxDisplayMode
    {
        Headless,
        Gui
    }

    public enum SbcVirtualMachineState
    {
        Unknown,
        PoweredOff,
        Running,
        Saved,
        Aborted
    }

    public enum SbcSitlOperationState
    {
        Stopped,
        Starting,
        VmRunning,
        Stopping,
        Faulted
    }

    [Serializable]
    public sealed class SbcSitlConfiguration
    {
        [Tooltip("Registered VirtualBox machine name, not the display model name in the Unity scene.")]
        public string virtualMachineName = "RSMA-Ubuntu-SITL";

        [Min(1)]
        public int virtualCpuCount = 2;

        [Min(512)]
        public int memoryMegabytes = 4096;

        [Range(1, 100)]
        public int cpuExecutionCapPercent = 100;

        [Tooltip("VirtualBox window mode used by startvm. Headless runs without a VM window; GUI opens the VirtualBox display window.")]
        public SbcVirtualBoxDisplayMode displayMode = SbcVirtualBoxDisplayMode.Headless;

        [Tooltip("Host network interface bridged to the SITL VM. Leave empty and pick a host adapter in RSMA > SBC SITL, or apply global defaults.")]
        public string bridgeAdapterName = "";

        [Min(1)]
        public int startupTimeoutSeconds = 60;

        [Min(1)]
        public int shutdownTimeoutSeconds = 30;

        [Min(1)]
        public int commandTimeoutSeconds = 10;

        public void ApplyGlobalDefaults(SbcVirtualBoxGlobalSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (!string.IsNullOrWhiteSpace(settings.defaultBridgeAdapterName))
                bridgeAdapterName = settings.defaultBridgeAdapterName;
            virtualCpuCount = Math.Max(1, settings.defaultVirtualCpuCount);
            memoryMegabytes = Math.Max(512, settings.defaultMemoryMegabytes);
            cpuExecutionCapPercent = Math.Max(1, Math.Min(100, settings.defaultCpuExecutionCapPercent));
            startupTimeoutSeconds = Math.Max(1, settings.defaultStartupTimeoutSeconds);
            shutdownTimeoutSeconds = Math.Max(1, settings.defaultShutdownTimeoutSeconds);
            commandTimeoutSeconds = Math.Max(1, settings.defaultCommandTimeoutSeconds);
        }

        public void ValidateIdentity()
        {
            if (string.IsNullOrWhiteSpace(virtualMachineName))
                throw new ArgumentException("VirtualBox VM name must be non-empty.", nameof(virtualMachineName));
            if (virtualMachineName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("VirtualBox VM name contains an invalid character.", nameof(virtualMachineName));
        }

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(SbcVirtualBoxDisplayMode), displayMode))
                throw new ArgumentOutOfRangeException(nameof(displayMode));
            ValidateIdentity();
            if (string.IsNullOrWhiteSpace(bridgeAdapterName) || bridgeAdapterName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("A VirtualBox bridge adapter name is required. Select a host adapter in RSMA > SBC SITL.", nameof(bridgeAdapterName));
            if (startupTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(startupTimeoutSeconds));
            if (shutdownTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(shutdownTimeoutSeconds));
            if (commandTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));
            if (virtualCpuCount < 1)
                throw new ArgumentOutOfRangeException(nameof(virtualCpuCount));
            if (memoryMegabytes < 512)
                throw new ArgumentOutOfRangeException(nameof(memoryMegabytes));
            if (cpuExecutionCapPercent < 1 || cpuExecutionCapPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(cpuExecutionCapPercent));
        }
    }

    public sealed class SbcVirtualBoxCommandResult
    {
        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }

        public bool Succeeded => ExitCode == 0;

        public SbcVirtualBoxCommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
        }
    }

    public sealed class SbcVirtualMachineInfo
    {
        public string Name { get; }
        public string Id { get; }

        public SbcVirtualMachineInfo(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    public sealed class SbcBridgedInterfaceInfo
    {
        public string Name { get; }
        public string Guid { get; }
        public bool IsUp { get; }
        public bool IsWireless { get; }

        public SbcBridgedInterfaceInfo(string name, string guid, bool isUp, bool isWireless)
        {
            Name = name ?? string.Empty;
            Guid = guid ?? string.Empty;
            IsUp = isUp;
            IsWireless = isWireless;
        }
    }

    public interface ISbcVirtualBoxCommandRunner
    {
        Task<SbcVirtualBoxCommandResult> RunAsync(string executable, string arguments, CancellationToken cancellationToken);
    }

    public sealed class SystemSbcVirtualBoxCommandRunner : ISbcVirtualBoxCommandRunner
    {
        public async Task<SbcVirtualBoxCommandResult> RunAsync(
            string executable,
            string arguments,
            CancellationToken cancellationToken)
        {
            SbcSitlLog.Log($"[SBC SITL][VBoxManage] Executing '{executable}' with arguments: {arguments}");
            cancellationToken.ThrowIfCancellationRequested();
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                try
                {
                    process.Start();
                }
                catch (Exception exception)
                {
                    SbcSitlLog.Error($"[SBC SITL][VBoxManage] Could not start '{executable}': {exception}");
                    throw;
                }
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                    }
                }))
                {
                    try
                    {
                        await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
                    }
                    catch
                    {
                        if (!process.HasExited)
                            process.Kill();
                        throw;
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                var result = new SbcVirtualBoxCommandResult(
                    process.ExitCode,
                    await outputTask.ConfigureAwait(false),
                    await errorTask.ConfigureAwait(false));
                if (result.Succeeded)
                    SbcSitlLog.Log($"[SBC SITL][VBoxManage] ExitCode=0; stdout: {result.StandardOutput.Trim()}");
                else
                    SbcSitlLog.Error($"[SBC SITL][VBoxManage] ExitCode={result.ExitCode}; stderr: {result.StandardError.Trim()}; stdout: {result.StandardOutput.Trim()}");
                return result;
            }
        }
    }

    public sealed class SbcVirtualBoxController
    {
        private readonly SbcSitlConfiguration _configuration;
        private readonly ISbcVirtualBoxCommandRunner _runner;

        public SbcVirtualBoxController(SbcSitlConfiguration configuration, ISbcVirtualBoxCommandRunner runner = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _runner = runner ?? new SystemSbcVirtualBoxCommandRunner();
        }

        public Task<IReadOnlyList<SbcVirtualMachineInfo>> ListVirtualMachinesAsync(CancellationToken cancellationToken = default)
        {
            return ListVirtualMachinesCoreAsync(cancellationToken);
        }

        public async Task<SbcVirtualMachineInfo> ImportVirtualMachineAsync(
            string machineFilePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(machineFilePath))
                throw new ArgumentException("A VirtualBox machine file is required.", nameof(machineFilePath));
            if (!File.Exists(machineFilePath))
                throw new FileNotFoundException("VirtualBox machine file was not found.", machineFilePath);
            if (!string.Equals(Path.GetExtension(machineFilePath), ".vbox", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("VirtualBox machine file must have a .vbox extension.", nameof(machineFilePath));

            var result = await RunVBoxManageAsync($"registervm {Quote(machineFilePath)}", cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("registervm", result);

            var machines = await ListVirtualMachinesCoreAsync(cancellationToken).ConfigureAwait(false);
            var importedName = Path.GetFileNameWithoutExtension(machineFilePath);
            foreach (var machine in machines)
            {
                if (string.Equals(machine.Name, importedName, StringComparison.OrdinalIgnoreCase))
                    return machine;
            }
            throw new InvalidOperationException($"VirtualBox registered '{machineFilePath}' but it was not returned by list vms.");
        }

        public Task<SbcVirtualMachineState> GetStateAsync(CancellationToken cancellationToken = default)
        {
            return QueryStateAsync(cancellationToken);
        }

        private async Task<SbcVirtualMachineState> QueryStateAsync(CancellationToken cancellationToken)
        {
            _configuration.ValidateIdentity();
            var result = await RunVBoxManageAsync(
                $"showvminfo {Quote(_configuration.virtualMachineName)} --machinereadable",
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("showvminfo", result);
            return ParseState(result.StandardOutput);
        }

        private async Task<IReadOnlyList<SbcVirtualMachineInfo>> ListVirtualMachinesCoreAsync(CancellationToken cancellationToken)
        {
            var result = await RunVBoxManageAsync("list vms", cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("list vms", result);

            return ParseMachineList(result.StandardOutput);
        }

        public static IReadOnlyList<SbcVirtualMachineInfo> ParseMachineList(string output)
        {
            var machines = new List<SbcVirtualMachineInfo>();
            using (var reader = new StringReader(output ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var firstQuote = line.IndexOf('"');
                    var lastQuote = line.LastIndexOf('"');
                    if (firstQuote < 0 || lastQuote <= firstQuote)
                        continue;
                    var name = line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                    var openBrace = line.IndexOf('{', lastQuote);
                    var closeBrace = line.IndexOf('}', openBrace + 1);
                    var id = openBrace >= 0 && closeBrace > openBrace
                        ? line.Substring(openBrace + 1, closeBrace - openBrace - 1)
                        : string.Empty;
                    machines.Add(new SbcVirtualMachineInfo(name, id));
                }
            }
            return machines;
        }

        public Task<IReadOnlyList<SbcBridgedInterfaceInfo>> ListBridgedInterfacesAsync(CancellationToken cancellationToken = default)
        {
            return ListBridgedInterfacesCoreAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<SbcBridgedInterfaceInfo>> ListBridgedInterfacesCoreAsync(CancellationToken cancellationToken)
        {
            var result = await RunVBoxManageAsync("list bridgedifs", cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("list bridgedifs", result);
            return ParseBridgedInterfaces(result.StandardOutput);
        }

        public static IReadOnlyList<SbcBridgedInterfaceInfo> ParseBridgedInterfaces(string output)
        {
            var interfaces = new List<SbcBridgedInterfaceInfo>();
            if (string.IsNullOrWhiteSpace(output))
                return interfaces;

            string name = null;
            string guid = null;
            string status = null;
            string wireless = null;

            void AddCurrent()
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    interfaces.Add(new SbcBridgedInterfaceInfo(
                        name,
                        guid,
                        string.Equals(status, "Up", StringComparison.OrdinalIgnoreCase),
                        string.Equals(wireless, "Yes", StringComparison.OrdinalIgnoreCase)));
                }
                name = null;
                guid = null;
                status = null;
                wireless = null;
            }

            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        AddCurrent();
                        continue;
                    }

                    var separator = line.IndexOf(':');
                    if (separator <= 0)
                        continue;

                    var key = line.Substring(0, separator).Trim();
                    var value = line.Substring(separator + 1).Trim();
                    switch (key)
                    {
                        case "Name": name = value; break;
                        case "GUID": guid = value; break;
                        case "Status": status = value; break;
                        case "Wireless": wireless = value; break;
                    }
                }
            }

            AddCurrent();
            return interfaces;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _configuration.Validate();
            var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
            if (state == SbcVirtualMachineState.Running)
                return;
            if (state != SbcVirtualMachineState.PoweredOff && state != SbcVirtualMachineState.Saved)
                throw new InvalidOperationException($"VirtualBox VM '{_configuration.virtualMachineName}' is in state {state} and cannot be started.");

            if (state == SbcVirtualMachineState.Saved)
            {
                var discardResult = await RunVBoxManageAsync(
                    $"discardstate {Quote(_configuration.virtualMachineName)}",
                    cancellationToken).ConfigureAwait(false);
                if (!discardResult.Succeeded)
                    throw CreateCommandException("discardstate", discardResult);
            }

            var networkResult = await RunVBoxManageAsync(
                $"modifyvm {Quote(_configuration.virtualMachineName)} --nic1 bridged --bridgeadapter1 {Quote(_configuration.bridgeAdapterName)} --cpus {_configuration.virtualCpuCount} --memory {_configuration.memoryMegabytes} --cpuexecutioncap {_configuration.cpuExecutionCapPercent}",
                cancellationToken).ConfigureAwait(false);
            if (!networkResult.Succeeded)
                throw CreateCommandException("configure bridged adapter", networkResult);

            var result = await RunVBoxManageAsync(
                $"startvm {Quote(_configuration.virtualMachineName)} --type {(_configuration.displayMode == SbcVirtualBoxDisplayMode.Gui ? "gui" : "headless")}",
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("startvm", result);

            await WaitForStateAsync(SbcVirtualMachineState.Running,
                _configuration.startupTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _configuration.ValidateIdentity();
            var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
            if (state == SbcVirtualMachineState.PoweredOff || state == SbcVirtualMachineState.Saved)
                return;
            if (state != SbcVirtualMachineState.Running)
                throw new InvalidOperationException($"VirtualBox VM '{_configuration.virtualMachineName}' is in state {state} and cannot be stopped cleanly.");

            var result = await RunVBoxManageAsync(
                $"controlvm {Quote(_configuration.virtualMachineName)} acpipowerbutton",
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("controlvm", result);

            await WaitForStateAsync(SbcVirtualMachineState.PoweredOff,
                _configuration.shutdownTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        }

        private async Task WaitForStateAsync(
            SbcVirtualMachineState expected,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            using (var waitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                try
                {
                    while (stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
                    {
                        waitTimeout.Token.ThrowIfCancellationRequested();
                        var remaining = TimeSpan.FromSeconds(timeoutSeconds) - stopwatch.Elapsed;
                        var stateTask = GetStateAsync(waitTimeout.Token);
                        var completed = await Task.WhenAny(stateTask, Task.Delay(remaining)).ConfigureAwait(false);
                        if (completed != stateTask)
                        {
                            waitTimeout.Cancel();
                            ObserveTaskFailure(stateTask);
                            SbcSitlLog.Error($"[SBC SITL] Timed out after {timeoutSeconds}s waiting for VM '{_configuration.virtualMachineName}' to become {expected}.");
                            throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for VirtualBox VM '{_configuration.virtualMachineName}' to enter {expected} state.");
                        }

                        var state = await stateTask.ConfigureAwait(false);
                        if (state == expected)
                            return;
                        if (state == SbcVirtualMachineState.Aborted)
                            throw new InvalidOperationException($"VirtualBox VM '{_configuration.virtualMachineName}' entered Aborted state while waiting for {expected}.");

                        var delay = remaining < TimeSpan.FromMilliseconds(250)
                            ? remaining
                            : TimeSpan.FromMilliseconds(250);
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, waitTimeout.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for VirtualBox VM '{_configuration.virtualMachineName}' to enter {expected} state.");
                }
            }

            throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for VirtualBox VM '{_configuration.virtualMachineName}' to enter {expected} state.");
        }

        private static void ObserveTaskFailure(Task task)
        {
            task.ContinueWith(
                completedTask =>
                {
                    var ignored = completedTask.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        public static SbcVirtualMachineState ParseState(string machineReadableOutput)
        {
            var marker = "VMState=\"";
            var start = machineReadableOutput == null ? -1 : machineReadableOutput.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return SbcVirtualMachineState.Unknown;
            start += marker.Length;
            var end = machineReadableOutput.IndexOf('"', start);
            var value = end > start ? machineReadableOutput.Substring(start, end - start) : string.Empty;
            switch (value.ToLowerInvariant())
            {
                case "poweroff": return SbcVirtualMachineState.PoweredOff;
                case "running": return SbcVirtualMachineState.Running;
                case "saved": return SbcVirtualMachineState.Saved;
                case "aborted": return SbcVirtualMachineState.Aborted;
                default: return SbcVirtualMachineState.Unknown;
            }
        }

        internal string BuildExecutablePath()
        {
            return SbcVirtualBoxGlobalSettings.Load().ResolveExecutablePath();
        }

        private Task<SbcVirtualBoxCommandResult> RunVBoxManageAsync(string arguments, CancellationToken cancellationToken)
        {
            return RunWithTimeoutAsync(arguments, cancellationToken);
        }

        private async Task<SbcVirtualBoxCommandResult> RunWithTimeoutAsync(
            string arguments,
            CancellationToken cancellationToken)
        {
            using (var commandTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                try
                {
                    var executablePath = BuildExecutablePath();
                    var commandTask = Task.Run(() => _runner.RunAsync(
                        executablePath, arguments, commandTimeout.Token));
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_configuration.commandTimeoutSeconds));
                    var completed = await Task.WhenAny(commandTask, timeoutTask).ConfigureAwait(false);
                    if (completed != commandTask)
                    {
                        commandTimeout.Cancel();
                        ObserveTaskFailure(commandTask);
                        SbcSitlLog.Error($"[SBC SITL] VBoxManage timeout after {_configuration.commandTimeoutSeconds}s: '{executablePath}' {arguments}");
                        throw new TimeoutException($"VBoxManage command exceeded {_configuration.commandTimeoutSeconds}s: {arguments}");
                    }

                    return await commandTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    SbcSitlLog.Error($"[SBC SITL] VBoxManage operation canceled by its command timeout: {arguments}");
                    throw new TimeoutException($"VBoxManage command exceeded {_configuration.commandTimeoutSeconds}s: {arguments}");
                }
                catch (Exception exception)
                {
                    SbcSitlLog.Error($"[SBC SITL] VBoxManage operation failed for '{arguments}': {exception}");
                    throw;
                }
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static Exception CreateCommandException(string operation, SbcVirtualBoxCommandResult result)
        {
            return new InvalidOperationException($"VirtualBox {operation} failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }
    }
}