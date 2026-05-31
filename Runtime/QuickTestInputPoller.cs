#if UNITY_EDITOR
using System;
using UnityEngine;

namespace UnityQuickTests
{
    public sealed class QuickTestInputPoller : MonoBehaviour
    {
        public static event Action Updated;

        private void Update()
        {
            Updated?.Invoke();
        }
    }
}
#endif
