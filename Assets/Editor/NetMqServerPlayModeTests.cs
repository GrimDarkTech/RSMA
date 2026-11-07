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
        [UnitySetUp]
        public IEnumerator EnterPlayMode()
        {
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator ExitPlayMode()
        {
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

            RequestSocket command = null;
            SubscriberSocket subscriber = null;
            PublisherSocket publisher = null;
            try
            {
                NetMQServer.Run(commandPort, pubPort, subPort);
                Assert.IsTrue(WaitForPort(commandPort), "Unity command socket did not start.");
                Assert.IsTrue(WaitForPort(pubPort), "Unity publisher socket did not start.");
                Assert.IsTrue(WaitForPort(subPort), "Unity subscriber socket did not start.");

                command = new RequestSocket();
                command.Options.Linger = TimeSpan.Zero;
                command.Connect($"tcp://127.0.0.1:{commandPort}");
                Assert.AreEqual("OK: Server is running", SendCommand(command, "GetServerStatus"));

                subscriber = new SubscriberSocket();
                subscriber.Options.Linger = TimeSpan.Zero;
                subscriber.Connect($"tcp://127.0.0.1:{pubPort}");
                subscriber.Subscribe("");

                publisher = new PublisherSocket();
                publisher.Options.Linger = TimeSpan.Zero;
                publisher.Connect($"tcp://127.0.0.1:{subPort}");
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
                yield return ReceivePublishedVectors(subscriber, expected, expectedIds);

                var topics = SendCommand(command, "GetTopics");
                StringAssert.Contains("e2e/RobotVelocity", topics);
                StringAssert.Contains("e2e/Pose", topics);
                StringAssert.Contains("e2e/LaserScan128", topics);

                SendFrame(publisher, "e2e/in/RobotVelocity", 1001, UdtpV1Codec.Encode(velocity), 10);
                SendFrame(publisher, "e2e/in/Pose", 1002, UdtpV1Codec.Encode(pose), 11);
                SendFrame(publisher, "e2e/in/LaserScan128", 1003, UdtpV1Codec.Encode(scan), 12);
                SendFrame(publisher, "e2e/in/Camera", UdtpV1Codec.LegacyCameraTypeId, new byte[] { 1, 2, 3, 4 }, 13);

                yield return WaitForIncomingState(velocity, pose, scan);

                var camera = DataBroker.GetRawState("e2e/in/Camera") as byte[];
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, camera);
            }
            finally
            {
                publisher?.Dispose();
                subscriber?.Dispose();
                command?.Dispose();
                NetMQServer.Stop();
            }
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