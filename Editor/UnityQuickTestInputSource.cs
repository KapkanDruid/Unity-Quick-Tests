using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal sealed class UnityQuickTestInputSource : IQuickTestInputSource
    {
        public static readonly UnityQuickTestInputSource Instance = new UnityQuickTestInputSource();

        private UnityQuickTestInputSource()
        {
        }

        public bool GetKey(KeyCode key)
        {
            return Input.GetKey(key);
        }
    }
}
