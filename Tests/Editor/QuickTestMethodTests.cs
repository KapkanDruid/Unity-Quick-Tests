using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestMethodTests
    {
        private static int _invocationCount;

        [SetUp]
        public void SetUp()
        {
            _invocationCount = 0;
        }

        [Test]
        public void Invoke_CallsStaticMethod()
        {
            CreateMethod(nameof(Increment)).Invoke();

            Assert.That(_invocationCount, Is.EqualTo(1));
        }

        [Test]
        public void Invoke_LogsInnerException()
        {
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: expected failure");

            CreateMethod(nameof(ThrowExpectedFailure)).Invoke();
        }

        private static QuickTestMethod CreateMethod(string name)
        {
            MethodInfo method = typeof(QuickTestMethodTests)
                .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

            return new QuickTestMethod(method);
        }

        private static void Increment()
        {
            _invocationCount++;
        }

        private static void ThrowExpectedFailure()
        {
            throw new InvalidOperationException("expected failure");
        }
    }
}
