using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal sealed class QuickTestMethod
    {
        private readonly IQuickTestTargetResolver _targetResolver;
        private bool _hasLoggedMissingTarget;
        private bool _hasLoggedNullTarget;

        public MethodInfo Method { get; }
        public string DisplayName { get; }
        public string MethodSignature { get; }
        public string TargetDescription { get; }
        public string DeclaringTypeName { get; }
        public string TargetScopeDescription { get; }
        public string SupportStatusDescription { get; }

        public QuickTestMethod(MethodInfo method)
            : this(method, QuickTestTargetResolver.Instance)
        {
        }

        internal QuickTestMethod(MethodInfo method, IQuickTestTargetResolver targetResolver)
        {
            Method = method;
            DeclaringTypeName = method.DeclaringType?.FullName ?? "<unknown>";
            DisplayName = $"{DeclaringTypeName}.{method.Name}";
            MethodSignature = BuildMethodSignature(method);
            TargetDescription = GetTargetDescription(method);
            TargetScopeDescription = GetTargetScopeDescription(method);
            SupportStatusDescription = GetSupportStatusDescription(method);
            _targetResolver = targetResolver ?? QuickTestTargetResolver.Instance;
        }

        public void Invoke()
        {
            if (Method.GetParameters().Length > 0)
            {
                QuickTestWarningSettings.LogWarning(
                    $"[UnityQuickTests] {DisplayName} was triggered but cannot be invoked: methods must be parameterless."
                );
                return;
            }

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
            bool hasInvokedTarget = false;
            bool hasNullTarget = false;

            foreach (object target in targets)
            {
                if (!IsLiveTarget(target))
                {
                    hasNullTarget = true;
                    continue;
                }

                hasInvokedTarget = true;
                InvokeTarget(target);
            }

            if (hasNullTarget)
            {
                LogNullTargetOnce();
            }
            else
            {
                _hasLoggedNullTarget = false;
            }

            if (!hasInvokedTarget && !hasNullTarget)
            {
                LogMissingTargetOnce();
            }
        }

        internal bool HasAvailableTarget()
        {
            if (Method.IsStatic)
                return true;

            IReadOnlyList<object> targets = _targetResolver.FindTargets(Method.DeclaringType) ?? Array.Empty<object>();
            return targets.Any(IsLiveTarget);
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

        private static bool IsLiveTarget(object target)
        {
            if (target == null)
                return false;

            if (target is UnityEngine.Object unityObject)
                return unityObject != null;

            return true;
        }

        private void LogMissingTargetOnce()
        {
            if (_hasLoggedMissingTarget)
                return;

            QuickTestWarningSettings.LogWarning(
                $"[UnityQuickTests] {DisplayName} was triggered but no live {TargetDescription} target was found. " +
                $"Target scope: {TargetScopeDescription}. This warning is suppressed until a matching target appears."
            );
            _hasLoggedMissingTarget = true;
        }

        private void LogNullTargetOnce()
        {
            if (_hasLoggedNullTarget)
                return;

            QuickTestWarningSettings.LogWarning(
                $"[UnityQuickTests] {DisplayName} was triggered but a resolved {TargetDescription} target was null. " +
                $"Target scope: {TargetScopeDescription}. The null target was skipped."
            );
            _hasLoggedNullTarget = true;
        }

        private static string BuildMethodSignature(MethodInfo method)
        {
            string parameters = string.Join(", ", method
                .GetParameters()
                .Select(parameter => $"{GetFriendlyTypeName(parameter.ParameterType)} {parameter.Name}")
            );

            return $"{method.DeclaringType?.FullName ?? "<unknown>"}.{method.Name}({parameters})";
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == null)
                return "<unknown>";

            if (!type.IsGenericType)
                return type.Name;

            string typeName = type.Name;
            int backtickIndex = typeName.IndexOf('`');
            if (backtickIndex >= 0)
            {
                typeName = typeName.Substring(0, backtickIndex);
            }

            string genericArguments = string.Join(", ", type
                .GetGenericArguments()
                .Select(GetFriendlyTypeName)
            );

            return $"{typeName}<{genericArguments}>";
        }

        private static string GetTargetDescription(MethodInfo method)
        {
            if (method.IsStatic)
                return "static";

            Type declaringType = method.DeclaringType;

            if (declaringType != null && typeof(UnityEngine.Object).IsAssignableFrom(declaringType))
                return "Unity object";

            return "registered instance";
        }

        private static string GetTargetScopeDescription(MethodInfo method)
        {
            if (method.IsStatic)
                return "static method";

            Type declaringType = method.DeclaringType;

            if (declaringType == null)
                return "unknown target";

            if (typeof(Component).IsAssignableFrom(declaringType))
                return "loaded scene Component instances";

            if (typeof(EditorWindow).IsAssignableFrom(declaringType))
                return "loaded EditorWindow instances";

            if (typeof(ScriptableObject).IsAssignableFrom(declaringType))
                return "loaded ScriptableObject instances";

            if (typeof(UnityEngine.Object).IsAssignableFrom(declaringType))
                return "loaded UnityEngine.Object instances";

            return "weak-registered plain C# instances";
        }

        private static string GetSupportStatusDescription(MethodInfo method)
        {
            if (method.IsStatic)
                return "supported: direct invocation";

            Type declaringType = method.DeclaringType;

            if (declaringType == null)
                return "unsupported: declaring type could not be resolved";

            if (typeof(Component).IsAssignableFrom(declaringType))
                return "supported: scene lookup includes inactive loaded instances";

            if (typeof(EditorWindow).IsAssignableFrom(declaringType))
                return "supported: loaded editor windows only";

            if (typeof(ScriptableObject).IsAssignableFrom(declaringType))
                return "supported: loaded objects only; AssetDatabase asset loading is intentionally disabled";

            if (typeof(UnityEngine.Object).IsAssignableFrom(declaringType))
                return "supported: loaded UnityEngine.Object lookup only";

            return "supported: ILPP auto-registration or manual QuickTestInstanceRegistry.Register";
        }
    }
}
