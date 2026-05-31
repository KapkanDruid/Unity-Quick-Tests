using System;

namespace UnityQuickTests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class QuickTestScheduleAttribute : Attribute
    {
        public double Interval { get; }
        public QuickTestScheduleUnit Unit { get; }
        public QuickTestRepeatMode RepeatMode { get; }

        public QuickTestScheduleAttribute(
            int interval,
            QuickTestScheduleUnit unit,
            QuickTestRepeatMode repeatMode = QuickTestRepeatMode.Once)
            : this((double)interval, unit, repeatMode)
        {
        }

        public QuickTestScheduleAttribute(
            double interval,
            QuickTestScheduleUnit unit,
            QuickTestRepeatMode repeatMode = QuickTestRepeatMode.Once)
        {
            if (interval <= 0d)
                throw new ArgumentOutOfRangeException(nameof(interval), "Quick test schedule interval must be positive.");

            Interval = interval;
            Unit = unit;
            RepeatMode = repeatMode;
        }
    }
}
