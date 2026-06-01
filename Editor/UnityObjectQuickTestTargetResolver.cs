using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityQuickTests.Editor
{
    internal sealed class UnityObjectQuickTestTargetResolver : IQuickTestTargetResolver
    {
        public static readonly UnityObjectQuickTestTargetResolver Instance = new UnityObjectQuickTestTargetResolver();

        private UnityObjectQuickTestTargetResolver()
        {
        }

        public IReadOnlyList<object> FindTargets(Type targetType)
        {
            if (targetType == null || !typeof(Object).IsAssignableFrom(targetType))
                return Array.Empty<object>();

            Object[] targets = typeof(Component).IsAssignableFrom(targetType)
                ? Object.FindObjectsByType(targetType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                : Resources.FindObjectsOfTypeAll(targetType);

            return targets
                .Where(target => target != null)
                .Where(target => !typeof(UnityEditor.Editor).IsAssignableFrom(target.GetType()))
                .Cast<object>()
                .ToArray();
        }
    }
}
