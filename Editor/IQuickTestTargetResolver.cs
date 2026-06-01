using System;
using System.Collections.Generic;

namespace UnityQuickTests.Editor
{
    internal interface IQuickTestTargetResolver
    {
        IReadOnlyList<object> FindTargets(Type targetType);
    }
}
