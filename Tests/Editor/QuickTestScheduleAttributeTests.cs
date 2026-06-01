using System;
using NUnit.Framework;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestScheduleAttributeTests
    {
        [Test]
        public void Constructor_StoresValues()
        {
            var attribute = new QuickTestScheduleAttribute(
                2.5,
                QuickTestScheduleUnit.Seconds,
                QuickTestRepeatMode.Repeat
            );

            Assert.That(attribute.Interval, Is.EqualTo(2.5));
            Assert.That(attribute.Unit, Is.EqualTo(QuickTestScheduleUnit.Seconds));
            Assert.That(attribute.RepeatMode, Is.EqualTo(QuickTestRepeatMode.Repeat));
        }

        [Test]
        public void Constructor_DefaultsToOnce()
        {
            var attribute = new QuickTestScheduleAttribute(1, QuickTestScheduleUnit.Frames);

            Assert.That(attribute.RepeatMode, Is.EqualTo(QuickTestRepeatMode.Once));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_RejectsNonPositiveIntInterval(int interval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new QuickTestScheduleAttribute(interval, QuickTestScheduleUnit.Frames)
            );
        }

        [TestCase(0d)]
        [TestCase(-0.01d)]
        public void Constructor_RejectsNonPositiveDoubleInterval(double interval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new QuickTestScheduleAttribute(interval, QuickTestScheduleUnit.Seconds)
            );
        }
    }
}
