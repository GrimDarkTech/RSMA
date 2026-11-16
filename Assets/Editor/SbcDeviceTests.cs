using System;
using System.Collections;
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

namespace RSMA.Tests
{
    public class SbcDeviceTests
    {
        [Test]
        public void ConfigurationValidatesPortsAndTopics()
        {
            var gameObject = new GameObject("SBC test");
            try
            {
                var device = gameObject.AddComponent<SbcDevice>();
                device.ValidateConfiguration();
                Assert.AreEqual("sbc-01/sensor/velocity", device.VelocityTopic);
                Assert.AreEqual("sbc-01/command/arm", device.ArmCommandTopic);
                Assert.AreEqual(SbcSessionState.Stopped, device.State);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InvalidConfigurationDoesNotStartSession()
        {
            var gameObject = new GameObject("SBC invalid test");
            try
            {
                var device = gameObject.AddComponent<SbcDevice>();
                device.Connection.commandPort = device.Connection.publishPort;
                device.StartSession();
                Assert.AreEqual(SbcSessionState.Faulted, device.State);
                StringAssert.Contains("ports must be different", device.LastError);
                Assert.IsFalse(NetMQServer.IsRunning);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                NetMQServer.Stop();
            }
        }

        [UnityTest]
        public IEnumerator SessionPublishesVelocityAndDispatchesArmCommandOnUpdate()
        {
            var gameObject = new GameObject("SBC integration test");
            SubscriberSocket subscriber = null;
            try
            {
                var device = gameObject.AddComponent<SbcDevice>();
                var commandPort = GetFreePort();
                var publishPort = GetFreePort();
                var subscribePort = GetFreePort();
                device.Connection.commandPort = commandPort;
                device.Connection.publishPort = publishPort;
                device.Connection.subscribePort = subscribePort;
                device.StartSession();
                Assert.AreEqual(SbcSessionState.Running, device.State);
                Assert.IsTrue(WaitForPort(commandPort), "SBC command endpoint did not start.");
                Assert.IsTrue(WaitForPort(publishPort), "SBC publish endpoint did not start.");
                Assert.IsTrue(WaitForPort(subscribePort), "SBC subscribe endpoint did not start.");

                subscriber = new SubscriberSocket();
                subscriber.Options.Linger = TimeSpan.Zero;
                subscriber.Connect($"tcp://127.0.0.1:{publishPort}");
                subscriber.Subscribe(device.VelocityTopic);
                yield return new UnityEngine.WaitForSeconds(0.25f);

                var velocity = new RobotVelocity
                {
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    linearVelocity = 1.25f,
                    angularVelocity = -0.5f
                };
                device.PublishVelocity(velocity);

                var received = false;
                var deadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < deadline && !received)
                {
                    var message = new NetMQMessage();
                    if (subscriber.TryReceiveMultipartMessage(TimeSpan.Zero, ref message))
                    {
                        Assert.AreEqual(3, message.FrameCount);
                        Assert.AreEqual(device.VelocityTopic, message[0].ConvertToString());
                        var header = TopicHeader.DecodeV1(message[1].ToByteArray());
                        Assert.AreEqual(UdtpV1Codec.RobotVelocityTypeId, header.topicTypeId);
                        Assert.AreEqual(velocity.timestamp, UdtpV1Codec.TryDecode(header.topicTypeId, message[2].ToByteArray(), out var decoded)
                            ? ((RobotVelocity)decoded).timestamp
                            : -1);
                        received = true;
                    }
                    yield return null;
                }
                Assert.IsTrue(received, "SBC velocity topic was not published by the Unity server.");

                var armCommandReceived = false;
                device.ArmCommandReceived += _ => armCommandReceived = true;
                var command = new ArmCommand { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), arm = 1 };
                DataBroker.UpdateFromNetwork(device.ArmCommandTopic,
                    new TopicHeader
                    {
                        timestamp = command.timestamp,
                        payloadSize = 16,
                        topicTypeId = UdtpV1Codec.ArmCommandTypeId
                    }.EncodeV1(),
                    UdtpV1Codec.Encode(command));

                device.ProcessPendingCommands();
                Assert.IsTrue(armCommandReceived);
                Assert.AreEqual(command.timestamp, device.LastArmCommand.timestamp);
            }
            finally
            {
                subscriber?.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
                NetMQServer.Stop();
            }
        }

        private static int GetFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
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