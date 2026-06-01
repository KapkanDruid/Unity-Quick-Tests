using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityQuickTests.Editor
{
    internal sealed class QuickTestTargetResolver : IQuickTestTargetResolver
    {
        public static readonly QuickTestTargetResolver Instance = new QuickTestTargetResolver();

        private QuickTestTargetResolver()
        {
        }

        public IReadOnlyList<object> FindTargets(Type targetType)
        {
            if (targetType == null)
                return Array.Empty<object>();

            if (typeof(Object).IsAssignableFrom(targetType))
                return UnityObjectQuickTestTargetResolver.Instance.FindTargets(targetType);

            return QuickTestInstanceRegistry.FindTargets(targetType);
        }
    }
}
