using System;
using UnityEngine;

namespace QuickEditorTests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class QuickTestHotkeyAttribute : Attribute
    {
        public KeyCode[] Keys { get; }

        public QuickTestHotkeyAttribute(params KeyCode[] keys)
        {
            Keys = keys ?? Array.Empty<KeyCode>();
        }
    }
}
