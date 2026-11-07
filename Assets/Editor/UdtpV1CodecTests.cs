using System;
using NUnit.Framework;
using RSMA.uDTP;
using RSMA.uDTP.CDR;
using RSMA.uDTP.Topics;

namespace RSMA.Tests
{
    public class UdtpV1CodecTests
    {
        [Test]
        public void RobotVelocityMatchesSharedPayloadVector()
        {
            var value = new RobotVelocity { timestamp = 1700000000123, linearVelocity = 1.5f, angularVelocity = -2.25f };
            Assert.AreEqual("7b68e5cf8b0100000000c03f000010c0", Hex(UdtpV1Codec.Encode(value)));
        }

        [Test]
        public void PoseMatchesSharedPayloadVectorAndZeroTailPadding()
        {
            var value = new Pose { timestamp = 1700000000123, position = new UnityEngine.Vector3(1f, -2f, 0.5f), rotation = new UnityEngine.Quaternion(0f, 0f, 0.70710677f, 0.70710677f) };
            var bytes = UdtpV1Codec.Encode(value);
            Assert.AreEqual("7b68e5cf8b0100000000803f000000c00000003f0000000000000000f304353ff304353f00000000", Hex(bytes));
            Assert.IsTrue(UdtpV1Codec.TryDecode(1002, bytes, out var decoded));
            Assert.AreEqual(value.timestamp, ((Pose)decoded).timestamp);
        }

        [Test]
        public void LaserScan128HasExpectedVectorSizeAndRoundTrips()
        {
            var value = new LaserScan128 { timestamp = 1700000000123, angleMin = -1.57079637f, angleMax = 1.57079637f, angleIncrement = 0.02473616f, rangeMin = 0.1f, rangeMax = 30f };
            for (var i = 0; i < 128; i++) value.SetRange(i, i == 0 ? 0.25f : i + 0.5f);
            var bytes = UdtpV1Codec.Encode(value);
            Assert.AreEqual(540, bytes.Length);
            Assert.IsTrue(UdtpV1Codec.TryDecode(1003, bytes, out var decoded));
            Assert.AreEqual(value.GetRange(127), ((LaserScan128)decoded).GetRange(127));
        }

        [Test]
        public void HeaderMatchesTwentyByteLittleEndianVector()
        {
            var header = new TopicHeader { timestamp = 1700000000123, sequence = 16909060, payloadSize = 16, topicTypeId = 1001 };
            var bytes = header.EncodeV1();
            Assert.AreEqual(20, bytes.Length);
            Assert.AreEqual("7b68e5cf8b0100000403020110000000e9030000", Hex(bytes));
            Assert.AreEqual(1001, TopicHeader.DecodeV1(bytes).topicTypeId);
        }

        [Test]
        public void TypedPoseAndLegacyCameraRawPayloadAreDispatchedSeparately()
        {
            var pose = new Pose { timestamp = 123, position = UnityEngine.Vector3.one, rotation = UnityEngine.Quaternion.identity };
            DataBroker.UpdateFromNetwork("test/pose", new TopicHeader { timestamp = 123, sequence = 1, payloadSize = 40, topicTypeId = 1002 }.EncodeV1(), UdtpV1Codec.Encode(pose));
            Assert.AreEqual(123, DataBroker.GetState<Pose>("test/pose").timestamp);
            Assert.AreEqual(default(RobotVelocity).timestamp, DataBroker.GetState<RobotVelocity>("test/pose").timestamp);
            var camera = new byte[] { 1, 2, 3, 4 };
            DataBroker.UpdateFromNetwork("test/camera", new TopicHeader { timestamp = 123, sequence = 2, payloadSize = 4, topicTypeId = 2 }.EncodeV1(), camera);
            CollectionAssert.AreEqual(camera, (byte[])DataBroker.GetRawState("test/camera"));
        }

        [Test]
        public void RejectsMalformedHeadersPayloadsAndPosePadding()
        {
            Assert.Throws<FormatException>(() => TopicHeader.DecodeV1(new byte[19]));
            Assert.Throws<FormatException>(() => DataBroker.UpdateFromNetwork("test/bad", new TopicHeader { payloadSize = 16, topicTypeId = 1001 }.EncodeV1(), new byte[15]));
            var poseBytes = new byte[40];
            poseBytes[36] = 1;
            Assert.IsFalse(UdtpV1Codec.TryDecode(1002, poseBytes, out _));
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}