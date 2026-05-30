using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace QuickEditorTests.Editor
{
    internal static class QuickTestDiscovery
    {
        private const BindingFlags MethodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static IReadOnlyList<QuickTestRegistration> FindRegistrations()
        {
            var registrations = new List<QuickTestRegistration>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                foreach (Type type in GetLoadableTypes(assembly))
                {
                    foreach (MethodInfo method in type.GetMethods(MethodFlags))
                    {
                        AddRegistration(registrations, method);
                    }
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

            if (!method.IsStatic)
            {
                Debug.LogWarning($"[QuickEditorTests] {displayName} is ignored: only static methods are supported.");
                return false;
            }

            if (method.ContainsGenericParameters)
            {
                Debug.LogWarning($"[QuickEditorTests] {displayName} is ignored: generic methods are not supported.");
                return false;
            }

            if (method.GetParameters().Length > 0)
            {
                Debug.LogWarning($"[QuickEditorTests] {displayName} is ignored: methods must be parameterless.");
                return false;
            }

            if (method.ReturnType != typeof(void))
            {
                Debug.LogWarning($"[QuickEditorTests] {displayName} is ignored: only void methods are supported.");
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
