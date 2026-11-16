using System;
using System.Collections.Generic;
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
        public string endpoint = "tcp://127.0.0.1";

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
                throw new ArgumentException("SBC endpoint must be a tcp URI such as tcp://127.0.0.1.", nameof(endpoint));

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

        [SerializeField]
        private string velocityTopic = "sensor/velocity";

        [SerializeField]
        private string armCommandTopic = "command/arm";

        [SerializeField]
        private bool startOnEnable;

        private bool _attachedToServer;
        private bool _ownsServerSession;
        private bool _hasAppliedArmCommand;
        private long _lastAppliedArmTimestamp;
        private SbcSessionState _state = SbcSessionState.Stopped;
        private ArmCommand _lastArmCommand;

        public string NodeId => nodeId;
        public SbcSimulationMode Mode => mode;
        public SbcEndpointConfiguration Connection => connection;
        public SbcSessionState State => _state;
        public string LastError { get; private set; } = string.Empty;
        public ArmCommand LastArmCommand => _lastArmCommand;
        public string VelocityTopic => BuildTopic(velocityTopic);
        public string ArmCommandTopic => BuildTopic(armCommandTopic);

        public event Action<SbcSessionState> StateChanged;
        public event Action<ArmCommand> ArmCommandReceived;

        private void OnEnable()
        {
            if (startOnEnable)
                StartSession();
        }

        private void Update()
        {
            ProcessPendingCommands();
        }

        public void ProcessPendingCommands()
        {
            if (_state != SbcSessionState.Running || !_attachedToServer || !NetMQServer.IsRunning)
                return;

            var command = DataBroker.GetState<ArmCommand>(ArmCommandTopic);
            if (!_hasAppliedArmCommand || command.timestamp > _lastAppliedArmTimestamp)
            {
                _hasAppliedArmCommand = true;
                _lastAppliedArmTimestamp = command.timestamp;
                _lastArmCommand = command;
                ArmCommandReceived?.Invoke(command);
            }
        }

        private void OnDestroy()
        {
            StopSession();
        }

        public void StartSession()
        {
            if (_state == SbcSessionState.Running)
                return;

            try
            {
                ValidateConfiguration();
                LastError = string.Empty;
                SetState(SbcSessionState.Starting);
                var serverWasRunning = NetMQServer.IsRunning;
                NetMQServer.Run(connection.commandPort, connection.publishPort, connection.subscribePort);
                _attachedToServer = NetMQServer.IsRunning;
                _ownsServerSession = _attachedToServer && !serverWasRunning;
                SetState(SbcSessionState.Running);
            }
            catch (Exception exception)
            {
                _attachedToServer = false;
                _ownsServerSession = false;
                LastError = exception.Message;
                SetState(SbcSessionState.Faulted);
            }
        }

        public void StopSession()
        {
            if (_ownsServerSession)
            {
                NetMQServer.Stop();
            }
            _attachedToServer = false;
            _ownsServerSession = false;

            if (_state != SbcSessionState.Stopped)
                SetState(SbcSessionState.Stopped);
        }

        public void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(nodeId) || nodeId.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0)
                throw new ArgumentException("SBC nodeId must be non-empty and must not contain '/', '\\' or NUL.", nameof(nodeId));

            if (string.IsNullOrWhiteSpace(velocityTopic) || string.IsNullOrWhiteSpace(armCommandTopic))
                throw new ArgumentException("SBC topic suffixes must be non-empty.");

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

        private void SetState(SbcSessionState state)
        {
            _state = state;
            StateChanged?.Invoke(state);
        }
    }
}