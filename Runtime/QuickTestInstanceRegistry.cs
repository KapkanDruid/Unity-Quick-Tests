using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace UnityQuickTests
{
    public static class QuickTestInstanceRegistry
    {
#if UNITY_EDITOR
        private static readonly List<WeakReference> Targets = new List<WeakReference>();
#endif

        [Conditional("UNITY_EDITOR")]
        public static void Register(object target)
        {
#if UNITY_EDITOR
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            PruneDeadReferences();

            if (Targets.Any(reference => ReferenceEquals(reference.Target, target)))
                return;

            Targets.Add(new WeakReference(target));
#endif
        }

        [Conditional("UNITY_EDITOR")]
        public static void Unregister(object target)
        {
#if UNITY_EDITOR
            if (target == null)
                return;

            Targets.RemoveAll(reference =>
            {
                object registeredTarget = reference.Target;
                return registeredTarget == null || ReferenceEquals(registeredTarget, target);
            });
#endif
        }

#if UNITY_EDITOR
        internal static IReadOnlyList<object> FindTargets(Type targetType)
        {
            if (targetType == null)
                return Array.Empty<object>();

            PruneDeadReferences();

            return Targets
                .Select(reference => reference.Target)
                .Where(target => target != null)
                .Where(target => targetType.IsInstanceOfType(target))
                .ToArray();
        }

        internal static void Clear()
        {
            Targets.Clear();
        }

        private static void PruneDeadReferences()
        {
            Targets.RemoveAll(reference => reference.Target == null);
        }
#endif
    }
}
