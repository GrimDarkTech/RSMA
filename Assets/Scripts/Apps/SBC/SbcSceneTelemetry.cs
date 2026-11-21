using System;
using RSMA.uDTP.Topics;
using UnityEngine;

namespace RSMA.SBC
{
    /// <summary>
    /// Bridges scene motion and SBC command state to the typed SbcDevice topics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SbcSceneTelemetry : MonoBehaviour
    {
        [SerializeField]
        private SbcDevice device;

        [SerializeField]
        [Min(0.01f)]
        private float publishIntervalSeconds = 0.1f;

        [SerializeField]
        private bool publishOnEnable;

        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private float _nextPublishTime;
        private bool _subscribed;
        private bool _armed;

        public SbcDevice Device => device;
        public bool IsArmed => _armed;
        public float PublishIntervalSeconds => publishIntervalSeconds;

        public event Action<bool> ArmStateChanged;

        private void Awake()
        {
            EnsureSubscription();
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        private void OnEnable()
        {
            EnsureSubscription();
            if (publishOnEnable)
                _nextPublishTime = Time.time;
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            EnsureSubscription();
            if (device == null || device.State != SbcSessionState.Running || Time.time < _nextPublishTime)
                return;

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var position = transform.position;
            var rotationDelta = transform.rotation * Quaternion.Inverse(_lastRotation);
            rotationDelta.ToAngleAxis(out var angle, out var axis);
            if (angle > 180f)
                angle -= 360f;
            var velocity = new RobotVelocity
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                linearVelocity = Vector3.Distance(position, _lastPosition) / deltaTime,
                angularVelocity = angle * Mathf.Deg2Rad / deltaTime
            };
            device.PublishVelocity(velocity);
            _lastPosition = position;
            _lastRotation = transform.rotation;
            _nextPublishTime = Time.time + publishIntervalSeconds;
        }

        private void Subscribe()
        {
            if (!_subscribed && device != null)
            {
                device.ArmCommandReceived += OnArmCommandReceived;
                device.ConnectionLost += OnConnectionLost;
                _subscribed = true;
            }
        }

        public void EnsureSubscription()
        {
            if (device == null)
                device = GetComponent<SbcDevice>();
            Subscribe();
        }

        private void Unsubscribe()
        {
            if (_subscribed && device != null)
            {
                device.ArmCommandReceived -= OnArmCommandReceived;
                device.ConnectionLost -= OnConnectionLost;
            }
            _subscribed = false;
        }

        private void OnConnectionLost()
        {
            SetArmed(false);
        }

        private void OnArmCommandReceived(ArmCommand command)
        {
            SetArmed(command.arm != 0);
        }

        private void SetArmed(bool value)
        {
            if (_armed == value)
                return;
            _armed = value;
            ArmStateChanged?.Invoke(value);
        }
    }
}