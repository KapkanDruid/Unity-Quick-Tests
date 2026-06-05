using System.Reflection;
using NUnit.Framework;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestScheduleBindingTests
    {
        private static int _invocationCount;

        [SetUp]
        public void SetUp()
        {
            _invocationCount = 0;
        }

        [Test]
        public void Tick_OnceAfterOneFrame_DoesNotInvokeOnFirstObservedTick()
        {
            var binding = CreateBinding(
                new QuickTestScheduleAttribute(1, QuickTestScheduleUnit.Frames),
                currentFrame: 0,
                currentTime: 0d
            );

            binding.Tick(1, 0d);
            Assert.That(_invocationCount, Is.Zero);

            binding.Tick(2, 0d);

            Assert.That(_invocationCount, Is.EqualTo(1));
            Assert.That(binding.IsCompleted, Is.True);
        }

        [Test]
        public void Tick_OnceAfterFrames_InvokesOnlyOnce()
        {
            var binding = CreateBinding(
                new QuickTestScheduleAttribute(2, QuickTestScheduleUnit.Frames),
                currentFrame: 10,
                currentTime: 5d
            );

            binding.Tick(11, 5d);
            Assert.That(_invocationCount, Is.Zero);

            binding.Tick(12, 5d);
            Assert.That(_invocationCount, Is.EqualTo(1));
            Assert.That(binding.IsCompleted, Is.True);

            binding.Tick(13, 5d);

            Assert.That(_invocationCount, Is.EqualTo(1));
            Assert.That(binding.IsCompleted, Is.True);
        }

        [Test]
        public void Tick_OnceAfterSeconds_DoesNotInvokeOnFirstObservedTick()
        {
            var binding = CreateBinding(
                new QuickTestScheduleAttribute(5, QuickTestScheduleUnit.Seconds),
                currentFrame: 0,
                currentTime: 0d
            );

            binding.Tick(1, 100d);
            Assert.That(_invocationCount, Is.Zero);

            binding.Tick(1, 104.99d);
            Assert.That(_invocationCount, Is.Zero);

            binding.Tick(1, 105d);

            Assert.That(_invocationCount, Is.EqualTo(1));
            Assert.That(binding.IsCompleted, Is.True);
        }

        [Test]
        public void Tick_RepeatingSeconds_ReschedulesFromCurrentTime()
        {
            var binding = CreateBinding(
                new QuickTestScheduleAttribute(1.5, QuickTestScheduleUnit.Seconds, QuickTestRepeatMode.Repeat),
                currentFrame: 0,
                currentTime: 2d
            );

            binding.Tick(0, 3.49d);
            Assert.That(_invocationCount, Is.Zero);

            binding.Tick(0, 3.5d);
            Assert.That(_invocationCount, Is.Zero);

            binding.Tick(0, 4.98d);
            Assert.That(_invocationCount, Is.Zero);

            binding.Tick(0, 4.99d);
            binding.Tick(0, 6.48d);
            binding.Tick(0, 6.49d);

            Assert.That(_invocationCount, Is.EqualTo(2));
            Assert.That(binding.IsCompleted, Is.False);
        }

        private static QuickTestScheduleBinding CreateBinding(
            QuickTestScheduleAttribute attribute,
            int currentFrame,
            double currentTime)
        {
            MethodInfo method = typeof(QuickTestScheduleBindingTests)
                .GetMethod(nameof(Increment), BindingFlags.Static | BindingFlags.NonPublic);

            return new QuickTestScheduleBinding(new QuickTestMethod(method), attribute, currentFrame, currentTime);
        }

        private static void Increment()
        {
            _invocationCount++;
        }
    }
}
