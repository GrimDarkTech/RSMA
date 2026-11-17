using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        Nat,
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

    [Serializable]
    public sealed class SbcSitlConfiguration
    {
        public SbcGuestProfile guestProfile = SbcGuestProfile.Ubuntu2404;

        [Tooltip("Registered VirtualBox machine name, not the display model name in the Unity scene.")]
        public string virtualMachineName = "RSMA-Ubuntu-SITL";

        [Tooltip("Path to VBoxManage.exe. Leave empty to resolve it from the default VirtualBox installation.")]
        public string vboxManagePath = string.Empty;

        public SbcVirtualBoxNetworkMode networkMode = SbcVirtualBoxNetworkMode.Nat;

        [Tooltip("VirtualBox NAT gateway address used by the guest to reach the host.")]
        public string guestToHostAddress = "10.0.2.2";

        [Min(1)]
        public int startupTimeoutSeconds = 60;

        [Min(1)]
        public int shutdownTimeoutSeconds = 30;

        public string GuestImageLabel => guestProfile == SbcGuestProfile.Ubuntu2204
            ? "Ubuntu 22.04"
            : "Ubuntu 24.04";

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(virtualMachineName))
                throw new ArgumentException("VirtualBox VM name must be non-empty.", nameof(virtualMachineName));
            if (virtualMachineName.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("VirtualBox VM name contains an invalid character.", nameof(virtualMachineName));
            if (!System.Net.IPAddress.TryParse(guestToHostAddress, out _))
                throw new ArgumentException("SITL guestToHostAddress must be an IPv4/IPv6 address.", nameof(guestToHostAddress));
            if (startupTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(startupTimeoutSeconds));
            if (shutdownTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(shutdownTimeoutSeconds));
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
                await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);
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

        public async Task<SbcVirtualMachineState> GetStateAsync(CancellationToken cancellationToken = default)
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

            var result = await RunVBoxManageAsync(
                $"startvm {Quote(_configuration.virtualMachineName)} --type headless",
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw CreateCommandException("startvm", result);
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
            return _runner.RunAsync(BuildExecutablePath(), arguments, cancellationToken);
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