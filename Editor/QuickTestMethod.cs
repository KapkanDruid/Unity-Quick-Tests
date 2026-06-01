using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal sealed class QuickTestMethod
    {
        private readonly IQuickTestTargetResolver _targetResolver;
        private bool _hasLoggedMissingTarget;

        public MethodInfo Method { get; }
        public string DisplayName { get; }
        public string TargetDescription { get; }

        public QuickTestMethod(MethodInfo method)
            : this(method, UnityObjectQuickTestTargetResolver.Instance)
        {
        }

        internal QuickTestMethod(MethodInfo method, IQuickTestTargetResolver targetResolver)
        {
            Method = method;
            DisplayName = $"{method.DeclaringType?.FullName}.{method.Name}";
            TargetDescription = method.IsStatic ? "static" : "Unity object";
            _targetResolver = targetResolver ?? UnityObjectQuickTestTargetResolver.Instance;
        }

        public void Invoke()
        {
            if (Method.IsStatic)
            {
                InvokeTarget(null);
                return;
            }

            IReadOnlyList<object> targets = _targetResolver.FindTargets(Method.DeclaringType) ?? Array.Empty<object>();

            if (targets.Count == 0)
            {
                LogMissingTargetOnce();
                return;
            }

            _hasLoggedMissingTarget = false;

            foreach (object target in targets)
            {
                InvokeTarget(target);
            }
        }

        private void InvokeTarget(object target)
        {
            try
            {
                Method.Invoke(target, null);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void LogMissingTargetOnce()
        {
            if (_hasLoggedMissingTarget)
                return;

            Debug.LogWarning($"[UnityQuickTests] {DisplayName} was triggered but no live Unity object target was found.");
            _hasLoggedMissingTarget = true;
        }
    }
}
