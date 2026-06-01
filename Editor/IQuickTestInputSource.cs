using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal interface IQuickTestInputSource
    {
        bool GetKey(KeyCode key);
    }
}
