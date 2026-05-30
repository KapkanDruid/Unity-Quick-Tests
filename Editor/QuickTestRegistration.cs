using System.Collections.Generic;

namespace QuickEditorTests.Editor
{
    internal readonly struct QuickTestRegistration
    {
        public QuickTestMethod Method { get; }
        public IReadOnlyList<QuickTestHotkeyAttribute> HotkeyAttributes { get; }
        public IReadOnlyList<QuickTestScheduleAttribute> ScheduleAttributes { get; }

        public QuickTestRegistration(
            QuickTestMethod method,
            IReadOnlyList<QuickTestHotkeyAttribute> hotkeyAttributes,
            IReadOnlyList<QuickTestScheduleAttribute> scheduleAttributes)
        {
            Method = method;
            HotkeyAttributes = hotkeyAttributes;
            ScheduleAttributes = scheduleAttributes;
        }
    }
}
