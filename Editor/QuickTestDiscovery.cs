using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal static class QuickTestDiscovery
    {
        private const BindingFlags MethodFlags =
            BindingFlags.Static |
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        public static IReadOnlyList<QuickTestRegistration> FindRegistrations()
        {
            IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .SelectMany(GetLoadableTypes);

            return FindRegistrations(types);
        }

        internal static IReadOnlyList<QuickTestRegistration> FindRegistrations(IEnumerable<Type> types)
        {
            var registrations = new List<QuickTestRegistration>();

            foreach (Type type in types)
            {
                foreach (MethodInfo method in type.GetMethods(MethodFlags))
                {
                    AddRegistration(registrations, method);
                }
            }

            return registrations;
        }

        private static void AddRegistration(List<QuickTestRegistration> registrations, MethodInfo method)
        {
            IReadOnlyList<QuickTestHotkeyAttribute> hotkeyAttributes;
            IReadOnlyList<QuickTestScheduleAttribute> scheduleAttributes;

            try
            {
                hotkeyAttributes = method.GetCustomAttributes<QuickTestHotkeyAttribute>(false).ToArray();
                scheduleAttributes = method.GetCustomAttributes<QuickTestScheduleAttribute>(false).ToArray();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            if (hotkeyAttributes.Count == 0 && scheduleAttributes.Count == 0)
                return;

            if (!CanInvoke(method))
                return;

            registrations.Add(new QuickTestRegistration(
                new QuickTestMethod(method),
                hotkeyAttributes,
                scheduleAttributes
            ));
        }

        private static bool CanInvoke(MethodInfo method)
        {
            string displayName = $"{method.DeclaringType?.FullName}.{method.Name}";

            if (method.ContainsGenericParameters)
            {
                Debug.LogWarning($"[UnityQuickTests] {displayName} is ignored: generic methods are not supported.");
                return false;
            }

            if (method.GetParameters().Length > 0)
            {
                Debug.LogWarning($"[UnityQuickTests] {displayName} is ignored: methods must be parameterless.");
                return false;
            }

            if (method.ReturnType != typeof(void))
            {
                Debug.LogWarning($"[UnityQuickTests] {displayName} is ignored: only void methods are supported.");
                return false;
            }

            return method.IsStatic || CanInvokeInstanceMethod(method, displayName);
        }

        private static bool CanInvokeInstanceMethod(MethodInfo method, string displayName)
        {
            Type declaringType = method.DeclaringType;

            if (declaringType == null)
            {
                Debug.LogWarning($"[UnityQuickTests] {displayName} is ignored: declaring type could not be resolved.");
                return false;
            }

            if (method.IsAbstract)
            {
                Debug.LogWarning($"[UnityQuickTests] {displayName} is ignored: abstract methods are not supported.");
                return false;
            }

            if (declaringType.IsAbstract)
            {
                Debug.LogWarning($"[UnityQuickTests] {displayName} is ignored: abstract target types are not supported.");
                return false;
            }

            if (declaringType.IsValueType)
            {
                Debug.LogWarning($"[UnityQuickTests] {displayName} is ignored: value type target types are not supported.");
                return false;
            }

            if (typeof(UnityEditor.Editor).IsAssignableFrom(declaringType))
            {
                Debug.LogWarning(
                    $"[UnityQuickTests] {displayName} is ignored: UnityEditor.Editor targets are not supported until their lifecycle is validated."
                );

                return false;
            }

            return true;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Array.Empty<Type>();
            }
        }
    }
}
