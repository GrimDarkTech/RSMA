using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetMQ;
using NetMQ.Sockets;
using NUnit.Framework;
using RSMA.NetMQ;
using RSMA.SBC;
using RSMA.uDTP;
using RSMA.uDTP.CDR;
using RSMA.uDTP.Topics;
using UnityEngine;
using UnityEngine.TestTools;
using Pose = RSMA.uDTP.Topics.Pose;

namespace RSMA.Tests
{
    public class NetMqServerPlayModeTests
    {
        private GameObject _sbcArmTestObject;
        private PublisherSocket _sbcArmPublisher;
        private PublisherSocket _serverPublisher;
        private SubscriberSocket _serverSubscriber;
        private RequestSocket _serverCommand;

        [UnitySetUp]
        public IEnumerator EnterPlayMode()
        {
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator ExitPlayMode()
        {
            yield return DisposeSocketWithoutBlocking(_sbcArmPublisher);
            _sbcArmPublisher = null;
            yield return DisposeSocketWithoutBlocking(_serverPublisher);
            _serverPublisher = null;
            yield return DisposeSocketWithoutBlocking(_serverSubscriber);
            _serverSubscriber = null;
            yield return DisposeSocketWithoutBlocking(_serverCommand);
            _serverCommand = null;

            if (NetMQServer.IsRunning)
                yield return StopServerWithoutBlocking();

            if (_sbcArmTestObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_sbcArmTestObject);
                _sbcArmTestObject = null;
            }

            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ServerExchangesV1TopicsAndLegacyCameraFrames()
        {
            var commandPort = GetFreePort();
            var pubPort = GetFreePort();
            var subPort = GetFreePort();
            Assert.That(new HashSet<int> { commandPort, pubPort, subPort }.Count, Is.EqualTo(3));

            try
            {
                NetMQServer.Run(commandPort, pubPort, subPort);
                Assert.IsTrue(WaitForPort(commandPort), "Unity command socket did not start.");
                Assert.IsTrue(WaitForPort(pubPort), "Unity publisher socket did not start.");
                Assert.IsTrue(WaitForPort(subPort), "Unity subscriber socket did not start.");

                _serverCommand = new RequestSocket();
                _serverCommand.Options.Linger = TimeSpan.Zero;
                _serverCommand.Connect($"tcp://127.0.0.1:{commandPort}");
                Assert.AreEqual("OK: Server is running", SendCommand(_serverCommand, "GetServerStatus"));

                _serverSubscriber = new SubscriberSocket();
                _serverSubscriber.Options.Linger = TimeSpan.Zero;
                _serverSubscriber.Connect($"tcp://127.0.0.1:{pubPort}");
                _serverSubscriber.Subscribe("");

                _serverPublisher = new PublisherSocket();
                _serverPublisher.Options.Linger = TimeSpan.Zero;
                _serverPublisher.Connect($"tcp://127.0.0.1:{subPort}");
                yield return new UnityEngine.WaitForSeconds(0.25f);

                var velocity = new RobotVelocity
                {
                    timestamp = 1700000000123,
                    linearVelocity = 1.5f,
                    angularVelocity = -2.25f
                };
                var pose = new Pose
                {
                    timestamp = 1700000000123,
                    position = new UnityEngine.Vector3(1f, -2f, 0.5f),
                    rotation = new UnityEngine.Quaternion(0f, 0f, 0.70710677f, 0.70710677f)
                };
                var scan = CreateSharedScan();

                DataBroker.Publish("e2e/RobotVelocity", velocity);
                DataBroker.Publish("e2e/Pose", pose);
                DataBroker.Publish("e2e/LaserScan128", scan);

                var expected = new Dictionary<string, byte[]>
                {
                    ["e2e/RobotVelocity"] = UdtpV1Codec.Encode(velocity),
                    ["e2e/Pose"] = UdtpV1Codec.Encode(pose),
                    ["e2e/LaserScan128"] = UdtpV1Codec.Encode(scan)
                };
                var expectedIds = new Dictionary<string, int>
                {
                    ["e2e/RobotVelocity"] = UdtpV1Codec.RobotVelocityTypeId,
                    ["e2e/Pose"] = UdtpV1Codec.PoseTypeId,
                    ["e2e/LaserScan128"] = UdtpV1Codec.LaserScan128TypeId
                };
                yield return ReceivePublishedVectors(_serverSubscriber, expected, expectedIds);

                var topics = SendCommand(_serverCommand, "GetTopics");
                StringAssert.Contains("e2e/RobotVelocity", topics);
                StringAssert.Contains("e2e/Pose", topics);
                StringAssert.Contains("e2e/LaserScan128", topics);
                SendFrame(_serverPublisher, "e2e/in/RobotVelocity", 1001, UdtpV1Codec.Encode(velocity), 11);
                SendFrame(_serverPublisher, "e2e/in/Pose", 1002, UdtpV1Codec.Encode(pose), 12);
                SendFrame(_serverPublisher, "e2e/in/LaserScan128", 1003, UdtpV1Codec.Encode(scan), 13);
                SendFrame(_serverPublisher, "e2e/in/Camera", 2, new byte[] { 1, 2, 3, 4 }, 14);
                SendFrame(_serverPublisher, "e2e/in/Unknown", 42, new byte[] { 9, 8, 7, 6 }, 14);
                SendFrame(_serverPublisher, "e2e/in/RobotVelocity", 1001, new byte[] { 1, 2, 3 }, 15);

                yield return WaitForIncomingState(velocity, pose, scan);

                var camera = DataBroker.GetRawState("e2e/in/Camera") as byte[];
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, camera);
                Assert.IsNull(DataBroker.GetRawState("e2e/in/Unknown"));
            }
        }

        [UnityTest]
        public IEnumerator SbcArmCommandReachesSceneTelemetryAndServerLossDisarms()
        {
            if (NetMQServer.IsRunning)
                yield return StopServerWithoutBlocking();

            _sbcArmTestObject = new GameObject("SBC ArmCommand PlayMode test");
            var device = _sbcArmTestObject.AddComponent<SbcDevice>();
            device.Connection.endpoint = "tcp://127.0.0.1";
            var ports = new[] { GetFreePort(), GetFreePort(), GetFreePort() };
            device.Connection.commandPort = ports[0];
            device.Connection.publishPort = ports[1];
            device.Connection.subscribePort = ports[2];
            var telemetry = _sbcArmTestObject.AddComponent<SbcSceneTelemetry>();
            var mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var receivedOnMainThread = false;
            telemetry.ArmStateChanged += armed =>
            {
                if (armed)
                    receivedOnMainThread = System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId;
            };

            device.StartSession();
            Assert.AreEqual(SbcSessionState.Running, device.State);
            _sbcArmPublisher = new PublisherSocket();
            _sbcArmPublisher.Options.Linger = TimeSpan.Zero;
            _sbcArmPublisher.Connect($"tcp://127.0.0.1:{device.Connection.subscribePort}");
            yield return new WaitForSeconds(0.25f);
            var command = new ArmCommand
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                arm = 1
            };
            _sbcArmPublisher.SendMultipartMessage(CreateArmFrame(device.ArmCommandTopic, command));

            var receiveDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < receiveDeadline && !telemetry.IsArmed)
                yield return null;

            Assert.IsTrue(telemetry.IsArmed, "Typed ArmCommand was not applied by SbcSceneTelemetry.");
            Assert.IsTrue(receivedOnMainThread, "ArmCommand scene handler must execute on Unity main thread.");

            yield return StopServerWithoutBlocking();
            Assert.AreEqual(SbcSessionState.Faulted, device.State);
            Assert.IsFalse(telemetry.IsArmed, "Server loss must transition scene telemetry to disarmed.");
            Assert.IsNotEmpty(device.LastError);

            NetMQServer.Run(device.Connection.commandPort, device.Connection.publishPort, device.Connection.subscribePort);
            var reconnectDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < reconnectDeadline && device.State != SbcSessionState.Running)
                yield return null;
            Assert.AreEqual(SbcSessionState.Running, device.State, "SBC session did not reconnect after NetMQServer restart.");
            Assert.IsFalse(telemetry.IsArmed, "Stale ArmCommand must not be replayed after reconnect.");
        }

        private static IEnumerator StopServerWithoutBlocking()
        {
            var stopTask = System.Threading.Tasks.Task.Run(NetMQServer.Stop);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!stopTask.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(stopTask.IsCompleted, "NetMQServer.Stop exceeded the 5-second test timeout.");
            if (stopTask.IsFaulted)
                Assert.Fail($"NetMQServer.Stop failed: {stopTask.Exception?.GetBaseException()}");
        }

        private static IEnumerator DisposeSocketWithoutBlocking(IDisposable socket)
        {
            if (socket == null)
                yield break;

            var disposeTask = System.Threading.Tasks.Task.Run(socket.Dispose);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!disposeTask.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!disposeTask.IsCompleted)
                UnityEngine.Debug.LogError("NetMQ test socket disposal exceeded the 5-second timeout.");
            else if (disposeTask.IsFaulted)
                UnityEngine.Debug.LogError($"NetMQ test socket disposal failed: {disposeTask.Exception?.GetBaseException()}");
        }

        private static NetMQMessage CreateArmFrame(string topic, ArmCommand command)
        {
            var payload = UdtpV1Codec.Encode(command);
            var message = new NetMQMessage();
            message.Append(topic);
            message.Append(new TopicHeader
            {
                timestamp = command.timestamp,
                sequence = 1,
                payloadSize = payload.Length,
                topicTypeId = UdtpV1Codec.ArmCommandTypeId
            }.EncodeV1());
            message.Append(payload);
            return message;
        }

        [UnityTest]
        public IEnumerator ServerCanStopAndStartAgain()
        {
            var commandPort = GetFreePort();
            var pubPort = GetFreePort();
            var subPort = GetFreePort();
            NetMQServer.Run(commandPort, pubPort, subPort);
            Assert.IsTrue(WaitForPort(commandPort), "First server start failed.");
            yield return StopServerWithoutBlocking();
            Assert.IsFalse(NetMQServer.IsRunning);

            NetMQServer.Run(commandPort, pubPort, subPort);
            Assert.IsTrue(WaitForPort(commandPort), "Server did not restart after Stop.");
            Assert.IsTrue(NetMQServer.IsRunning);
            yield return null;
        }

        private static IEnumerator ReceivePublishedVectors(
            SubscriberSocket subscriber,
            IDictionary<string, byte[]> expected,
            IDictionary<string, int> expectedIds)
        {
            var received = new HashSet<string>();
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && received.Count < expected.Count)
            {
                NetMQMessage message = new NetMQMessage();
                if (subscriber.TryReceiveMultipartMessage(TimeSpan.Zero, ref message) && message.FrameCount == 3)
                {
                    var topic = message[0].ConvertToString();
                    if (expected.ContainsKey(topic))
                    {
                        var header = TopicHeader.DecodeV1(message[1].ToByteArray());
                        Assert.AreEqual(expectedIds[topic], header.topicTypeId, topic);
                        CollectionAssert.AreEqual(expected[topic], message[2].ToByteArray(), topic);
                        received.Add(topic);
                    }
                }
                yield return null;
            }

            CollectionAssert.AreEquivalent(expected.Keys, received);
        }

        private static IEnumerator WaitForIncomingState(
            RobotVelocity expectedVelocity,
            Pose expectedPose,
            LaserScan128 expectedScan)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var velocity = DataBroker.GetState<RobotVelocity>("e2e/in/RobotVelocity");
                var pose = DataBroker.GetState<Pose>("e2e/in/Pose");
                var scan = DataBroker.GetState<LaserScan128>("e2e/in/LaserScan128");
                if (velocity.timestamp == expectedVelocity.timestamp &&
                    pose.timestamp == expectedPose.timestamp &&
                    scan.timestamp == expectedScan.timestamp &&
                    Mathf.Approximately(scan.GetRange(0), 1f) &&
                    Mathf.Approximately(scan.GetRange(1), 2f) &&
                    Mathf.Approximately(scan.GetRange(127), 12f))
                    yield break;
                yield return null;
            }

            Assert.Fail("Unity ReceiverLoop did not dispatch all typed v1 frames to DataBroker.");
        }

        private static string SendCommand(RequestSocket command, string value)
        {
            Assert.IsTrue(command.TrySendFrame(TimeSpan.FromSeconds(2), value));
            Assert.IsTrue(command.TryReceiveFrameString(TimeSpan.FromSeconds(2), out string response));
            return response;
        }

        private static void SendFrame(PublisherSocket publisher, string topic, int typeId, byte[] payload, uint sequence)
        {
            var header = new TopicHeader
            {
                timestamp = 1700000000123,
                sequence = sequence,
                payloadSize = payload.Length,
                topicTypeId = typeId
            };
            var message = new NetMQMessage();
            message.Append(topic);
            message.Append(header.EncodeV1());
            message.Append(payload);
            publisher.SendMultipartMessage(message);
        }

        private static LaserScan128 CreateSharedScan()
        {
            var scan = new LaserScan128
            {
                angleMin = -1.57079637f,
                angleMax = 1.57079637f,
                angleIncrement = 0.024543693f,
                rangeMin = 0.1f,
                rangeMax = 30f,
                timestamp = 1700000000123
            };
            for (var index = 0; index < 128; index++)
                scan.SetRange(index, index == 0 ? 1f : index == 1 ? 2f : index == 127 ? 12f : 0f);
            return scan;
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static bool WaitForPort(int port)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                using (var client = new TcpClient())
                {
                    try
                    {
                        var task = client.ConnectAsync(IPAddress.Loopback, port);
                        if (task.Wait(100) && client.Connected)
                            return true;
                    }
                    catch (SocketException)
                    {
                    }
                }
            }
            return false;
        }
    }
}