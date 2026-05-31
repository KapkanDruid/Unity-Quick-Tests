using System;
using System.Reflection;
using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal sealed class QuickTestMethod
    {
        public MethodInfo Method { get; }
        public string DisplayName { get; }

        public QuickTestMethod(MethodInfo method)
        {
            Method = method;
            DisplayName = $"{method.DeclaringType?.FullName}.{method.Name}";
        }

        public void Invoke()
        {
            try
            {
                Method.Invoke(null, null);
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
    }
}
