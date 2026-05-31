using System;
using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal sealed class QuickTestScheduleBinding
    {
        private readonly QuickTestMethod _method;
        private readonly QuickTestScheduleAttribute _attribute;
        private int _nextFrame;
        private double _nextTime;

        public bool IsCompleted { get; private set; }
        public string Description { get; }
        public string MethodName => _method.DisplayName;

        public QuickTestScheduleBinding(
            QuickTestMethod method,
            QuickTestScheduleAttribute attribute,
            int currentFrame,
            double currentTime)
        {
            _method = method;
            _attribute = attribute;
            Description = BuildDescription(attribute);

            ScheduleNext(currentFrame, currentTime);
        }

        public void Tick(int currentFrame, double currentTime)
        {
            if (IsCompleted || !IsDue(currentFrame, currentTime))
                return;

            _method.Invoke();

            if (_attribute.RepeatMode == QuickTestRepeatMode.Once)
            {
                IsCompleted = true;
                return;
            }

            ScheduleNext(currentFrame, currentTime);
        }

        private bool IsDue(int currentFrame, double currentTime)
        {
            switch (_attribute.Unit)
            {
                case QuickTestScheduleUnit.Frames:
                    return currentFrame >= _nextFrame;

                case QuickTestScheduleUnit.Seconds:
                    return currentTime >= _nextTime;

                default:
                    Debug.LogWarning($"[UnityQuickTests] Unsupported schedule unit: {_attribute.Unit}.");
                    IsCompleted = true;
                    return false;
            }
        }

        private void ScheduleNext(int currentFrame, double currentTime)
        {
            switch (_attribute.Unit)
            {
                case QuickTestScheduleUnit.Frames:
                    _nextFrame = currentFrame + Math.Max(1, (int)Math.Ceiling(_attribute.Interval));
                    return;

                case QuickTestScheduleUnit.Seconds:
                    _nextTime = currentTime + _attribute.Interval;
                    return;

                default:
                    IsCompleted = true;
                    return;
            }
        }

        private static string BuildDescription(QuickTestScheduleAttribute attribute)
        {
            string interval = attribute.Unit == QuickTestScheduleUnit.Frames
                ? $"{Math.Ceiling(attribute.Interval)} frame(s)"
                : $"{attribute.Interval:0.###} second(s)";

            return attribute.RepeatMode == QuickTestRepeatMode.Once
                ? $"Once after {interval}"
                : $"Repeat every {interval}";
        }
    }
}
