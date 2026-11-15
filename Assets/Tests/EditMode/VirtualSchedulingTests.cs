using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RSMA.Microcontroller.Virtual;

namespace RSMA.Microcontroller.Virtual.Tests
{
    public sealed class VirtualSchedulingTests
    {
        [Test]
        public void HostClockPolicyHandlesPauseTimeScaleAndLargeHostTick()
        {
            var policy = new VirtualSchedulingPolicy
            {
                HostClockPolicy = VirtualHostClockPolicy.UnscaledDeltaTime,
                MaximumHostDeltaMilliseconds = 100
            };
            var diagnostics = new List<string>();

            Assert.AreEqual(100, policy.ComputeHostDeltaMilliseconds(16, 250, false, diagnostics));
            Assert.AreEqual(0, policy.ComputeHostDeltaMilliseconds(16, 250, true, diagnostics));
            Assert.AreEqual(1, diagnostics.Count);

            policy.HostClockPolicy = VirtualHostClockPolicy.Paused;
            Assert.AreEqual(0, policy.ComputeHostDeltaMilliseconds(16, 16, false));
        }

        [Test]
        public void PeriodicSchedulerUsesSimulatedTimeAndDeterministicCatchUpOrder()
        {
            var scheduler = new VirtualPeriodicScheduler();
            var callbacks = new List<string>();
            scheduler.SchedulePeriodic(10, 10, timestamp => callbacks.Add("a:" + timestamp));
            scheduler.SchedulePeriodic(10, 10, timestamp => callbacks.Add("b:" + timestamp));

            Assert.AreEqual(4, scheduler.AdvanceTo(20));
            CollectionAssert.AreEqual(new[] { "a:10", "b:10", "a:20", "b:20" }, callbacks);
            Assert.AreEqual(4, scheduler.CallbackCount);
        }

        [Test]
        public void InterruptControllerAppliesPriorityDebounceAndOverflowPolicy()
        {
            var policy = new VirtualSchedulingPolicy
            {
                InterruptQueueCapacity = 4,
                InterruptDebounceMilliseconds = 10
            };
            policy.SetInterruptPriority(2, 5);
            var source = new FakeEdgeSource();
            var controller = new VirtualInterruptController(policy);
            controller.Connect(source);
            controller.Attach(1, (byte)VirtualGpioEdge.Rising, 10, priority: 1);
            controller.Attach(2, (byte)VirtualGpioEdge.Rising, 20, priority: 5);
            controller.Enable();

            source.Raise(1, 0, 1);
            source.Raise(2, 0, 2);
            Assert.IsTrue(controller.TryDequeue(out var request));
            Assert.AreEqual(2, request.VirtualPin);

            source.Raise(1, 5, 3);
            source.Raise(1, 12, 4);
            Assert.IsTrue(controller.TryDequeue(out request));
            Assert.AreEqual(1, request.VirtualPin);
            Assert.AreEqual(0, request.TimestampMilliseconds);
            Assert.IsTrue(controller.SchedulingDiagnostics.Any(item => item.Code == "IRQ_DEBOUNCED"));
        }

        [Test]
        public void InterruptQueueReportsOverflowWithoutThrowingFromInputCallback()
        {
            var policy = new VirtualSchedulingPolicy { InterruptQueueCapacity = 1 };
            var source = new FakeEdgeSource();
            var controller = new VirtualInterruptController(policy);
            controller.Connect(source);
            controller.Attach(1, (byte)VirtualGpioEdge.Rising, 10);
            controller.Enable();

            source.Raise(1, 0, 1);
            source.Raise(1, 1, 2);
            Assert.AreEqual(1UL, controller.DroppedInterruptCount);
            Assert.IsTrue(controller.SchedulingDiagnostics.Any(item => item.Code == "IRQ_QUEUE_OVERFLOW"));
        }

        private sealed class FakeEdgeSource : IVirtualGpioEdgeSource
        {
            public event Action<VirtualGpioInputEvent> InputEdge;

            public void Raise(byte pin, double timestamp, ulong sequence)
            {
                InputEdge?.Invoke(new VirtualGpioInputEvent(
                    pin, false, true, timestamp, sequence, VirtualGpioEdge.Rising));
            }
        }
    }
}