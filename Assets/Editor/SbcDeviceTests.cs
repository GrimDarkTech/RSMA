using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        public void SitlConfigurationSupportsUbuntuProfilesAndVirtualBoxStates()
        {
            var configuration = new SbcSitlConfiguration
            {
                guestProfile = SbcGuestProfile.Ubuntu2204,
                virtualMachineName = "RSMA-Ubuntu-SITL"
            };
            configuration.Validate();
            Assert.AreEqual("Ubuntu 22.04", configuration.GuestImageLabel);
            Assert.AreEqual(SbcVirtualMachineState.PoweredOff,
                SbcVirtualBoxController.ParseState("VMState=\"poweroff\"\n"));
            Assert.AreEqual(SbcVirtualMachineState.Running,
                SbcVirtualBoxController.ParseState("VMState=\"running\"\n"));
            configuration.guestProfile = SbcGuestProfile.Ubuntu2404;
            Assert.AreEqual("Ubuntu 24.04", configuration.GuestImageLabel);
        }

        [Test]
        public void SitlConfigurationRejectsInvalidVmNameAndAddress()
        {
            var configuration = new SbcSitlConfiguration { virtualMachineName = "" };
            Assert.Throws<ArgumentException>(() => configuration.Validate());
            configuration.virtualMachineName = "RSMA-Ubuntu-SITL";
            configuration.guestToHostAddress = "not-an-ip";
            Assert.Throws<ArgumentException>(() => configuration.Validate());
        }

        [Test]
        public void SitlRejectsUnimplementedBridgedNetworkMode()
        {
            var configuration = new SbcSitlConfiguration
            {
                networkMode = SbcVirtualBoxNetworkMode.Bridged
            };
            Assert.Throws<NotSupportedException>(() => configuration.Validate());
        }

        [Test]
        public async Task VirtualBoxStartWaitsUntilGuestIsRunning()
        {
            var configuration = new SbcSitlConfiguration { virtualMachineName = "RSMA SITL" };
            var runner = new FakeVirtualBoxRunner(
                Result("VMState=\"poweroff\"\n"),
                Result("VM started"),
                Result("VMState=\"running\"\n"));
            var controller = new SbcVirtualBoxController(configuration, runner);

            await controller.StartAsync();

            Assert.AreEqual(3, runner.Arguments.Count);
            StringAssert.Contains("startvm \"RSMA SITL\" --type headless", runner.Arguments[1]);
        }

        [Test]
        public async Task VirtualBoxStopWaitsUntilGuestIsPoweredOff()
        {
            var configuration = new SbcSitlConfiguration { virtualMachineName = "RSMA-Ubuntu-SITL" };
            var runner = new FakeVirtualBoxRunner(
                Result("VMState=\"running\"\n"),
                Result("Power button sent"),
                Result("VMState=\"poweroff\"\n"));
            var controller = new SbcVirtualBoxController(configuration, runner);

            await controller.StopAsync();

            Assert.AreEqual(3, runner.Arguments.Count);
            StringAssert.Contains("controlvm \"RSMA-Ubuntu-SITL\" acpipowerbutton", runner.Arguments[1]);
        }

        [Test]
        public void VirtualBoxStartTimesOutIfVmNeverReachesRunning()
        {
            var configuration = new SbcSitlConfiguration
            {
                virtualMachineName = "RSMA-Ubuntu-SITL",
                startupTimeoutSeconds = 1,
                commandTimeoutSeconds = 1
            };
            var runner = new FakeVirtualBoxRunner(
                Result("VMState=\"poweroff\"\n"),
                Result("VM started"),
                Result("VMState=\"poweroff\"\n"),
                Result("VMState=\"poweroff\"\n"),
                Result("VMState=\"poweroff\"\n"),
                Result("VMState=\"poweroff\"\n"));
            var controller = new SbcVirtualBoxController(configuration, runner);

            Assert.ThrowsAsync<TimeoutException>(async () => await controller.StartAsync());
        }

        [Test]
        public void VirtualBoxStartupTimeoutIsValidated()
        {
            var configuration = new SbcSitlConfiguration { startupTimeoutSeconds = 0 };
            Assert.Throws<ArgumentOutOfRangeException>(() => configuration.Validate());
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
                NetMQServer.Stop();
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
                var nextPublish = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup < deadline && !received)
                {
                    if (Time.realtimeSinceStartup >= nextPublish)
                    {
                        device.PublishVelocity(velocity);
                        nextPublish = Time.realtimeSinceStartup + 0.1f;
                    }
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

        [Test]
        public void SceneTelemetryAppliesArmCommandState()
        {
            var gameObject = new GameObject("SBC telemetry test");
            try
            {
                NetMQServer.Stop();
                var device = gameObject.AddComponent<SbcDevice>();
                var telemetry = gameObject.AddComponent<SbcSceneTelemetry>();
                telemetry.EnsureSubscription();
                var changed = false;
                telemetry.ArmStateChanged += value => changed = value;

                var command = new ArmCommand { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), arm = 1 };
                device.Connection.commandPort = GetFreePort();
                device.Connection.publishPort = GetFreePort();
                device.Connection.subscribePort = GetFreePort();
                device.StartSession();
                Assert.AreEqual(SbcSessionState.Running, device.State);
                Assert.IsTrue(WaitForPort(device.Connection.commandPort));
                Assert.IsTrue(WaitForPort(device.Connection.publishPort));
                Assert.IsTrue(WaitForPort(device.Connection.subscribePort));
                DataBroker.UpdateFromNetwork(device.ArmCommandTopic,
                    new TopicHeader { timestamp = 123, payloadSize = 16, topicTypeId = UdtpV1Codec.ArmCommandTypeId }.EncodeV1(),
                    UdtpV1Codec.Encode(command));
                device.ProcessPendingCommands();

                Assert.IsTrue(telemetry.IsArmed);
                Assert.IsTrue(changed);
            }
            finally
            {
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

        private static SbcVirtualBoxCommandResult Result(string output, int exitCode = 0, string error = "") =>
            new SbcVirtualBoxCommandResult(exitCode, output, error);

        private sealed class FakeVirtualBoxRunner : ISbcVirtualBoxCommandRunner
        {
            private readonly Queue<SbcVirtualBoxCommandResult> _results;

            public readonly List<string> Arguments = new List<string>();

            public FakeVirtualBoxRunner(params SbcVirtualBoxCommandResult[] results)
            {
                _results = new Queue<SbcVirtualBoxCommandResult>(results);
            }

            public Task<SbcVirtualBoxCommandResult> RunAsync(
                string executable,
                string arguments,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Arguments.Add(arguments);
                return Task.FromResult(_results.Count == 0
                    ? Result("VMState=\"poweroff\"\n")
                    : _results.Dequeue());
            }
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