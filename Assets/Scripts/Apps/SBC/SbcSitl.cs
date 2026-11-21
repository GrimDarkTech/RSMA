using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace RSMA.SBC
{
    public enum SbcGuestProfile
    {
        Ubuntu2204,
        Ubuntu2404
    }

    public enum SbcVirtualBoxNetworkMode
    {
        Bridged
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
        public SbcGuestProfile guestProfile = SbcGuestProfile.Ubuntu2404;

        [Tooltip("Registered VirtualBox machine name, not the display model name in the Unity scene.")]
        public string virtualMachineName = "RSMA-Ubuntu-SITL";

        [Tooltip("Path to VBoxManage.exe. Leave empty to resolve it from the default VirtualBox installation.")]
        public string vboxManagePath = string.Empty;

        public SbcVirtualBoxNetworkMode networkMode = SbcVirtualBoxNetworkMode.Bridged;

        [Tooltip("Host-only identity of the bridged adapter selected in VirtualBox.")]
        public string bridgeAdapterName = "AIC8800D80 USB WiFi";

        [Min(1)]
        public int startupTimeoutSeconds = 60;

        [Min(1)]
        public int shutdownTimeoutSeconds = 30;

        [Min(1)]
        public int commandTimeoutSeconds = 10;

        public string GuestImageLabel => guestProfile == SbcGuestProfile.Ubuntu2204
            ? "Ubuntu 22.04"
            : "Ubuntu 24.04";

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(SbcGuestProfile), guestProfile))
                throw new ArgumentOutOfRangeException(nameof(guestProfile));
            if (!Enum.IsDefined(typeof(SbcVirtualBoxNetworkMode), networkMode))
                throw new ArgumentOutOfRangeException(nameof(networkMode));
            if (networkMode != SbcVirtualBoxNetworkMode.Bridged)
                throw new NotSupportedException("SITL requires VirtualBox Bridged networking.");
            if (string.IsNullOrWhiteSpace(virtualMachineName))
                throw new ArgumentException("VirtualBox VM name must be non-empty.", nameof(virtualMachineName));
            if (virtualMachineName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("VirtualBox VM name contains an invalid character.", nameof(virtualMachineName));
            if (string.IsNullOrWhiteSpace(bridgeAdapterName) || bridgeAdapterName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("A valid VirtualBox bridge adapter name is required.", nameof(bridgeAdapterName));
            if (startupTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(startupTimeoutSeconds));
            if (shutdownTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(shutdownTimeoutSeconds));
            if (commandTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));
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
                process.Start();
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
                return new SbcVirtualBoxCommandResult(
                    process.ExitCode,
                    await outputTask.ConfigureAwait(false),
                    await errorTask.ConfigureAwait(false));
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

        public string GuestImageLabel => _configuration.GuestImageLabel;

        public Task<SbcVirtualMachineState> GetStateAsync(CancellationToken cancellationToken = default)
        {
            return QueryStateAsync(cancellationToken);
        }

        private async Task<SbcVirtualMachineState> QueryStateAsync(CancellationToken cancellationToken)
        {
            _configuration.Validate();
            var result = await RunVBoxManageAsync(
                $"showvminfo {Quote(_configuration.virtualMachineName)} --machinereadable",
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("showvminfo", result);
            return ParseState(result.StandardOutput);
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
                $"modifyvm {Quote(_configuration.virtualMachineName)} --nic1 bridged --bridgeadapter1 {Quote(_configuration.bridgeAdapterName)}",
                cancellationToken).ConfigureAwait(false);
            if (!networkResult.Succeeded)
                throw CreateCommandException("configure bridged adapter", networkResult);

            var result = await RunVBoxManageAsync(
                $"startvm {Quote(_configuration.virtualMachineName)} --type headless",
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("startvm", result);

            await WaitForStateAsync(SbcVirtualMachineState.Running,
                _configuration.startupTimeoutSeconds, cancellationToken).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _configuration.Validate();
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
            if (!string.IsNullOrWhiteSpace(_configuration.vboxManagePath))
                return _configuration.vboxManagePath;
            return Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer
                ? @"C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"
                : "VBoxManage";
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
                    var commandTask = Task.Run(() => _runner.RunAsync(
                        BuildExecutablePath(), arguments, commandTimeout.Token));
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_configuration.commandTimeoutSeconds));
                    var completed = await Task.WhenAny(commandTask, timeoutTask).ConfigureAwait(false);
                    if (completed != commandTask)
                    {
                        commandTimeout.Cancel();
                        ObserveTaskFailure(commandTask);
                        throw new TimeoutException($"VBoxManage command exceeded {_configuration.commandTimeoutSeconds}s: {arguments}");
                    }

                    return await commandTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"VBoxManage command exceeded {_configuration.commandTimeoutSeconds}s: {arguments}");
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