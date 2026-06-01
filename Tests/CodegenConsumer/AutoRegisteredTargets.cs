using UnityEngine;
using UnityQuickTests;

namespace QuickTestCodegen.Consumer
{
    public sealed class AutoRegisteredPlainTarget
    {
        public static int InvocationCount { get; set; }

        [QuickTestHotkey(KeyCode.T)]
        public void Run()
        {
            InvocationCount++;
        }
    }

    public sealed class ChainedConstructorTarget
    {
        public static int InvocationCount { get; set; }

        public ChainedConstructorTarget()
            : this(7)
        {
        }

        public ChainedConstructorTarget(int value)
        {
            Value = value;
        }

        public int Value { get; }

        [QuickTestHotkey(KeyCode.T)]
        public void Run()
        {
            InvocationCount++;
        }
    }

    public sealed class ManualFallbackTarget
    {
        public static int InvocationCount { get; set; }

        public ManualFallbackTarget()
        {
            QuickTestInstanceRegistry.Register(this);
        }

        [QuickTestHotkey(KeyCode.T)]
        public void Run()
        {
            InvocationCount++;
        }
    }
}
