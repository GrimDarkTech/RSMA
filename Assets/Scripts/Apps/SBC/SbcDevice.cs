using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RSMA.NetMQ;
using RSMA.uDTP;
using RSMA.uDTP.Topics;
using UnityEngine;

namespace RSMA.SBC
{
    public enum SbcSimulationMode
    {
        Sitl,
        Hitl
    }

    public enum SbcSessionState
    {
        Stopped,
        Starting,
        Running,
        Faulted
    }

    [Serializable]
    public sealed class SbcEndpointConfiguration
    {
        [Tooltip("Address advertised to the SITL/HITL client. Unity binds the configured ports through NetMqServer.")]
        public string endpoint = "tcp://192.168.2.91";

        [Min(1)]
        public int commandPort = 5555;

        [Min(1)]
        public int publishPort = 5556;

        [Min(1)]
        public int subscribePort = 5560;

        [Min(1)]
        public int requestTimeoutMilliseconds = 2000;

        public void Validate()
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(uri.Host) || uri.AbsolutePath != "/" ||
                uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new ArgumentException("SBC endpoint must be a tcp URI such as tcp://192.168.2.91.", nameof(endpoint));
            if (string.Equals(uri.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Host, "::", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("SBC client endpoint must be a reachable host address, not a wildcard bind address.", nameof(endpoint));

            ValidatePort(commandPort, nameof(commandPort));
            ValidatePort(publishPort, nameof(publishPort));
            ValidatePort(subscribePort, nameof(subscribePort));
            if (new HashSet<int> { commandPort, publishPort, subscribePort }.Count != 3)
                throw new ArgumentException("SBC command, publish and subscribe ports must be different.");
            if (requestTimeoutMilliseconds < 1)
                throw new ArgumentException("SBC request timeout must be positive.", nameof(requestTimeoutMilliseconds));
        }

        private static void ValidatePort(int port, string name)
        {
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(name, port, "SBC port must be between 1 and 65535.");
        }
    }

    /// <summary>
    /// Scene-side SBC digital twin. Transport remains owned by the shared RSMA NetMQ server.
    /// </summary>
    public sealed class SbcDevice : MonoBehaviour
    {
        [SerializeField]
        private string nodeId = "sbc-01";

        [SerializeField]
        private SbcSimulationMode mode = SbcSimulationMode.Sitl;

        [SerializeField]
        private SbcEndpointConfiguration connection = new SbcEndpointConfiguration();

        private const string VelocityTopicSuffix = "sensor/velocity";
        private const string ArmCommandTopicSuffix = "command/arm";

        [SerializeField]
        private bool startOnEnable;

        [SerializeField]
        private bool autoStartSitl;

        [SerializeField]
        private SbcSitlConfiguration sitl = new SbcSitlConfiguration();

        private bool _attachedToServer;
        private bool _ownsServerSession;
        private bool _hasAppliedArmCommand;
        private long _lastAppliedArmTimestamp;
        private SbcSessionState _state = SbcSessionState.Stopped;
        private ArmCommand _lastArmCommand;
        private SbcSitlOperationState _sitlState = SbcSitlOperationState.Stopped;
        private CancellationTokenSource _sitlLifecycleCancellation;
        private Task _sitlLifecycleTask;
        private Task _sitlLifecycleStopTask;

        public string NodeId => nodeId;
        public SbcSimulationMode Mode => mode;
        public SbcEndpointConfiguration Connection => connection;
        public SbcSitlConfiguration Sitl => sitl;
        public SbcSessionState State => _state;
        public SbcSitlOperationState SitlState => _sitlState;
        public bool AutoStartSitl => autoStartSitl;
        public bool StartSessionOnEnable => startOnEnable;
        public string LastError { get; private set; } = string.Empty;
        public string LastSitlError { get; private set; } = string.Empty;
        public ArmCommand LastArmCommand => _lastArmCommand;
        public string VelocityTopic => BuildTopic(VelocityTopicSuffix);
        public string ArmCommandTopic => BuildTopic(ArmCommandTopicSuffix);

        public event Action<SbcSessionState> StateChanged;
        public event Action<SbcSitlOperationState> SitlStateChanged;
        public event Action<ArmCommand> ArmCommandReceived;
        public event Action ConnectionLost;

        public void SetAutoStartSitl(bool enabled)
        {
            autoStartSitl = enabled;
        }

        public void SetStartSessionOnEnable(bool enabled)
        {
            startOnEnable = enabled;
        }

        public void SetMode(SbcSimulationMode value)
        {
            mode = value;
            if (mode == SbcSimulationMode.Hitl)
                autoStartSitl = false;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            SbcSitlLog.Log($"[SBC SITL] OnEnable device='{name}', scene='{gameObject.scene.name}', mode={mode}, autoStartSitl={autoStartSitl}, startOnEnable={startOnEnable}.", this);
            if (autoStartSitl && mode != SbcSimulationMode.Sitl)
                SbcSitlLog.Error($"[SBC SITL] Auto-start is enabled on '{name}', but mode is {mode}; SITL startup will be rejected.", this);
            if (startOnEnable)
            {
                SbcSitlLog.Log($"[SBC SITL] Starting NetMQ-only session for '{name}' because startOnEnable is enabled.", this);
                StartSession();
            }
            else
            {
                SbcSitlLog.Log($"[SBC SITL] NetMQ-only start skipped for '{name}': startOnEnable is disabled.", this);
            }
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            if (mode != SbcSimulationMode.Sitl)
            {
                if (autoStartSitl)
                    SbcSitlLog.Warning($"[SBC HITL] Ignoring stale SITL auto-start flag on HITL device '{name}'. VirtualBox is not used in HITL mode.", this);
                return;
            }

            if (!autoStartSitl)
            {
                SbcSitlLog.Warning($"[SBC SITL] Auto-start skipped for device '{name}' in scene '{gameObject.scene.name}': Auto-start/stop VM is disabled on this SbcDevice component. Enable it in RSMA > SBC SITL while in EditMode, then save the scene.", this);
                return;
            }

            _sitlLifecycleCancellation = new CancellationTokenSource();
            _sitlLifecycleTask = StartAutoSitlAsync(_sitlLifecycleCancellation.Token);
        }

        private async Task StartAutoSitlAsync(CancellationToken cancellationToken)
        {
            try
            {
                await StartSitlAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                SbcSitlLog.Error($"[SBC SITL] Automatic startup failed for '{name}' in scene '{gameObject.scene.name}': {exception}", this);
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || !autoStartSitl)
                return;

            RequestAutoStop();
        }

        private void Update()
        {
            ProcessPendingCommands();
        }

        public void ProcessPendingCommands()
        {
            if (_state == SbcSessionState.Faulted && !_attachedToServer && NetMQServer.IsRunning &&
                IsServerEndpointReachable(connection))
            {
                LastError = string.Empty;
                _attachedToServer = true;
                SetState(SbcSessionState.Running);
            }

            if (_state != SbcSessionState.Running || !_attachedToServer)
                return;

            if (!NetMQServer.IsRunning)
            {
                _attachedToServer = false;
                LastError = "RSMA NetMQServer stopped while the SBC session was active.";
                SetState(SbcSessionState.Faulted);
                NotifyConnectionLost();
                return;
            }

            var command = DataBroker.GetState<ArmCommand>(ArmCommandTopic);
            if (command.timestamp > 0 && (!_hasAppliedArmCommand || command.timestamp > _lastAppliedArmTimestamp))
            {
                _hasAppliedArmCommand = true;
                _lastAppliedArmTimestamp = command.timestamp;
                _lastArmCommand = command;
                ArmCommandReceived?.Invoke(command);
            }
        }

        private void OnDestroy()
        {
            RequestAutoStop();
            StopSession();
        }

        private void RequestAutoStop()
        {
            if (mode != SbcSimulationMode.Sitl || !autoStartSitl || _sitlLifecycleStopTask != null)
                return;
            SbcSitlLog.Log($"[SBC SITL] Auto-stop requested for device '{name}', state={_sitlState}.", this);
            _sitlLifecycleCancellation?.Cancel();
            _sitlLifecycleStopTask = StopAutoStartedSitlAsync();
        }

        private async Task StopAutoStartedSitlAsync()
        {
            try
            {
                if (_sitlLifecycleTask != null)
                    await _sitlLifecycleTask;
            }
            catch (Exception exception)
            {
                SbcSitlLog.Warning($"[SBC SITL] Auto-start task ended with an error before auto-stop: {exception}", this);
            }

            if (_sitlLifecycleTask != null)
            {
                try
                {
                    await StopSitlAsync();
                    SbcSitlLog.Log($"[SBC SITL] Automatic VM shutdown completed for '{name}'.", this);
                }
                catch (Exception exception)
                {
                    LastSitlError = exception.Message;
                    SbcSitlLog.Error($"[SBC SITL] Automatic VM shutdown failed for '{name}': {exception}", this);
                }
            }
        }

        public void StartSession()
        {
            if (_state == SbcSessionState.Running)
                return;

            var serverWasRunning = NetMQServer.IsRunning;
            try
            {
                ValidateConfiguration();
                LastError = string.Empty;
                SetState(SbcSessionState.Starting);
                NetMQServer.Run(connection.commandPort, connection.publishPort, connection.subscribePort);
                WaitForServerEndpoint(connection);
                _attachedToServer = NetMQServer.IsRunning;
                _ownsServerSession = _attachedToServer && !serverWasRunning;
                SetState(SbcSessionState.Running);
            }
            catch (Exception exception)
            {
                if (!serverWasRunning && NetMQServer.IsRunning)
                    NetMQServer.StopInBackground();
                _attachedToServer = false;
                _ownsServerSession = false;
                LastError = exception.Message;
                SetState(SbcSessionState.Faulted);
            }
        }

        public async Task StartHitlSessionAsync(CancellationToken cancellationToken = default)
        {
            if (mode != SbcSimulationMode.Hitl)
                throw new InvalidOperationException("HITL session can only be started when the SBC mode is Hitl.");
            ValidateConfiguration();
            LastError = string.Empty;
            SetState(SbcSessionState.Starting);
            var serverWasRunning = NetMQServer.IsRunning;
            try
            {
                NetMQServer.Run(connection.commandPort, connection.publishPort, connection.subscribePort);
                await WaitForServerEndpointAsync(connection, cancellationToken);
                _attachedToServer = NetMQServer.IsRunning;
                _ownsServerSession = _attachedToServer && !serverWasRunning;
                SetState(SbcSessionState.Running);
            }
            catch (Exception exception)
            {
                if (!serverWasRunning && NetMQServer.IsRunning)
                    await NetMQServer.StopInBackground();
                _attachedToServer = false;
                _ownsServerSession = false;
                LastError = exception.Message;
                SetState(SbcSessionState.Faulted);
                throw;
            }
        }

        public Task StartSitlAsync(CancellationToken cancellationToken = default)
        {
            if (mode != SbcSimulationMode.Sitl)
                throw new InvalidOperationException("SITL VM operations are unavailable when the SBC mode is Hitl.");
            return StartSitlAsyncCore(cancellationToken);
        }

        public void StopHitlSession()
        {
            if (mode != SbcSimulationMode.Hitl)
                throw new InvalidOperationException("HITL session can only be stopped when the SBC mode is Hitl.");
            StopSession();
        }

        public void StopSession()
        {
            if (_ownsServerSession)
            {
                NetMQServer.StopInBackground();
            }
            _attachedToServer = false;
            _ownsServerSession = false;
            NotifyConnectionLost();

            if (_state != SbcSessionState.Stopped)
                SetState(SbcSessionState.Stopped);
        }

        private void NotifyConnectionLost()
        {
            if (_hasAppliedArmCommand)
                ConnectionLost?.Invoke();
            DataBroker.ClearTopicState(ArmCommandTopic);
            _hasAppliedArmCommand = false;
            _lastAppliedArmTimestamp = 0;
            _lastArmCommand = default;
        }

        private async Task StartSitlAsyncCore(CancellationToken cancellationToken = default)
        {
            SbcSitlLog.Log($"[SBC SITL] StartSitlAsync entered for '{name}': mode={mode}, VM='{sitl.virtualMachineName}', global VBoxManage='{SbcVirtualBoxGlobalSettings.Load().ResolveExecutablePath()}'.", this);
            if (mode != SbcSimulationMode.Sitl)
                throw new InvalidOperationException("SITL can only be started when the SBC mode is Sitl.");
            sitl.Validate();
            LastSitlError = string.Empty;
            SetSitlState(SbcSitlOperationState.Starting);
            SbcSitlLog.Log($"[SBC SITL] Starting VM '{sitl.virtualMachineName}' for device '{name}'. VBoxManage='{SbcVirtualBoxGlobalSettings.Load().ResolveExecutablePath()}', bridge='{sitl.bridgeAdapterName}'.", this);
            try
            {
                StartSession();
                if (_state != SbcSessionState.Running)
                    throw new InvalidOperationException(string.IsNullOrEmpty(LastError)
                        ? "RSMA NetMQ session did not start."
                        : LastError);

                await new SbcVirtualBoxController(sitl).StartAsync(cancellationToken);
                SetSitlState(SbcSitlOperationState.VmRunning);
                SbcSitlLog.Log($"[SBC SITL] VM '{sitl.virtualMachineName}' reached Running state.", this);
            }
            catch (Exception exception)
            {
                LastSitlError = exception.Message;
                SetSitlState(SbcSitlOperationState.Faulted);
                SbcSitlLog.Error($"[SBC SITL] Auto/manual start failed for VM '{sitl.virtualMachineName}': {exception}", this);
                StopSession();
                throw;
            }
        }

        public async Task StopSitlAsync(CancellationToken cancellationToken = default)
        {
            if (mode != SbcSimulationMode.Sitl)
                throw new InvalidOperationException("SITL can only be stopped when the SBC mode is Sitl.");
            sitl.ValidateIdentity();
            LastSitlError = string.Empty;
            SetSitlState(SbcSitlOperationState.Stopping);
            SbcSitlLog.Log($"[SBC SITL] Sending graceful shutdown to VM '{sitl.virtualMachineName}'.", this);
            try
            {
                await new SbcVirtualBoxController(sitl).StopAsync(cancellationToken);
                StopSession();
                SetSitlState(SbcSitlOperationState.Stopped);
                SbcSitlLog.Log($"[SBC SITL] VM '{sitl.virtualMachineName}' stopped.", this);
            }
            catch (Exception exception)
            {
                LastSitlError = exception.Message;
                SetSitlState(SbcSitlOperationState.Faulted);
                SbcSitlLog.Error($"[SBC SITL] Stop failed for VM '{sitl.virtualMachineName}': {exception}", this);
                throw;
            }
        }

        public async Task<SbcVirtualMachineState> RefreshSitlStateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var state = await new SbcVirtualBoxController(sitl).GetStateAsync(cancellationToken);
                LastSitlError = string.Empty;
                return state;
            }
            catch (Exception exception)
            {
                LastSitlError = exception.Message;
                throw;
            }
        }

        public void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(nodeId) || nodeId.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0)
                throw new ArgumentException("SBC nodeId must be non-empty and must not contain '/', '\\' or NUL.", nameof(nodeId));

            connection.Validate();
        }

        public void PublishVelocity(RobotVelocity velocity)
        {
            EnsureRunning();
            DataBroker.Publish(VelocityTopic, velocity);
        }

        public IReadOnlyList<string> GetActiveTopics()
        {
            return DataBroker.GetActiveTopics();
        }

        private string BuildTopic(string suffix)
        {
            return $"{nodeId}/{suffix.Trim('/')}";
        }

        private void EnsureRunning()
        {
            if (_state != SbcSessionState.Running || !_attachedToServer || !NetMQServer.IsRunning)
                throw new InvalidOperationException("SBC session is not running.");
        }

        private static void WaitForServerEndpoint(SbcEndpointConfiguration configuration)
        {
            if (!Uri.TryCreate(configuration.endpoint, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("SBC endpoint is invalid.");

            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(configuration.requestTimeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                if (CanConnect(uri.Host, configuration.commandPort) &&
                    CanConnect(uri.Host, configuration.publishPort) &&
                    CanConnect(uri.Host, configuration.subscribePort))
                    return;
                Thread.Sleep(10);
            }

            throw new InvalidOperationException($"RSMA NetMQServer is not reachable at {configuration.endpoint} on ports {configuration.commandPort}, {configuration.publishPort}, {configuration.subscribePort}.");
        }

        private static Task WaitForServerEndpointAsync(SbcEndpointConfiguration configuration, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(configuration.endpoint, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("SBC endpoint is invalid.");

            return WaitForServerEndpointAsync(uri.Host, configuration, cancellationToken);
        }

        private static async Task WaitForServerEndpointAsync(string host, SbcEndpointConfiguration configuration, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(configuration.requestTimeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var reachable = await Task.Run(() =>
                    CanConnect(host, configuration.commandPort) &&
                    CanConnect(host, configuration.publishPort) &&
                    CanConnect(host, configuration.subscribePort), cancellationToken);
                if (reachable)
                    return;
                await Task.Delay(10, cancellationToken);
            }

            throw new InvalidOperationException($"RSMA NetMQServer is not reachable at {configuration.endpoint} on ports {configuration.commandPort}, {configuration.publishPort}, {configuration.subscribePort}.");
        }

        private static bool IsServerEndpointReachable(SbcEndpointConfiguration configuration)
        {
            if (!Uri.TryCreate(configuration.endpoint, UriKind.Absolute, out var uri))
                return false;

            return CanConnect(uri.Host, configuration.commandPort) &&
                CanConnect(uri.Host, configuration.publishPort) &&
                CanConnect(uri.Host, configuration.subscribePort);
        }

        private static bool CanConnect(string host, int port)
        {
            using (var client = new System.Net.Sockets.TcpClient())
            {
                try
                {
                    var task = client.ConnectAsync(host, port);
                    return task.Wait(100) && client.Connected;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    return false;
                }
                catch (AggregateException exception) when (exception.InnerException is System.Net.Sockets.SocketException)
                {
                    return false;
                }
            }
        }

        private void SetState(SbcSessionState state)
        {
            _state = state;
            StateChanged?.Invoke(state);
        }

        private void SetSitlState(SbcSitlOperationState state)
        {
            _sitlState = state;
            SitlStateChanged?.Invoke(state);
        }
    }
}