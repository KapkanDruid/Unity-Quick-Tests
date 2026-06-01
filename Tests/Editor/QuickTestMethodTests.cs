using System;
using System.Collections.Generic;
using System.Linq;
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
            QuickTestInstanceRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            QuickTestInstanceRegistry.Clear();
        }

        [Test]
        public void Invoke_CallsStaticMethod()
        {
            CreateStaticMethod(nameof(Increment)).Invoke();

            Assert.That(_invocationCount, Is.EqualTo(1));
        }

        [Test]
        public void Invoke_CallsInstanceMethodOnAllResolvedTargets()
        {
            var targets = new[]
            {
                new InstanceFixture(),
                new InstanceFixture()
            };
            var resolver = new FakeTargetResolver(targets);

            QuickTestMethod method = CreateInstanceMethod(nameof(InstanceFixture.Increment), resolver);

            method.Invoke();

            Assert.That(_invocationCount, Is.EqualTo(2));
            Assert.That(resolver.RequestedTargetTypes.Single(), Is.EqualTo(typeof(InstanceFixture)));
        }

        [Test]
        public void Invoke_CallsPlainCSharpInstanceMethodOnAllRegisteredTargets()
        {
            QuickTestInstanceRegistry.Register(new InstanceFixture());
            QuickTestInstanceRegistry.Register(new InstanceFixture());

            QuickTestMethod method = CreateInstanceMethod(nameof(InstanceFixture.Increment));

            method.Invoke();

            Assert.That(_invocationCount, Is.EqualTo(2));
        }

        [Test]
        public void Invoke_MissingInstanceTarget_LogsWarningOnceUntilTargetAppears()
        {
            var resolver = new FakeTargetResolver();
            QuickTestMethod method = CreateInstanceMethod(nameof(InstanceFixture.Increment), resolver);
            string warning = $"[UnityQuickTests] {method.DisplayName} was triggered but no live registered instance target was found.";

            LogAssert.Expect(LogType.Warning, warning);
            method.Invoke();
            method.Invoke();
            LogAssert.NoUnexpectedReceived();

            resolver.SetTargets(new InstanceFixture());
            method.Invoke();

            Assert.That(_invocationCount, Is.EqualTo(1));

            LogAssert.Expect(LogType.Warning, warning);
            resolver.SetTargets();
            method.Invoke();
        }

        [Test]
        public void Invoke_LogsInnerException()
        {
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: expected failure");

            CreateStaticMethod(nameof(ThrowExpectedFailure)).Invoke();
        }

        private static QuickTestMethod CreateStaticMethod(string name)
        {
            MethodInfo method = typeof(QuickTestMethodTests)
                .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

            return new QuickTestMethod(method);
        }

        private static QuickTestMethod CreateInstanceMethod(string name, IQuickTestTargetResolver resolver)
        {
            MethodInfo method = typeof(InstanceFixture)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.Public);

            return new QuickTestMethod(method, resolver);
        }

        private static QuickTestMethod CreateInstanceMethod(string name)
        {
            MethodInfo method = typeof(InstanceFixture)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.Public);

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

        private sealed class InstanceFixture
        {
            public void Increment()
            {
                _invocationCount++;
            }
        }

        private sealed class FakeTargetResolver : IQuickTestTargetResolver
        {
            private IReadOnlyList<object> _targets;

            public List<Type> RequestedTargetTypes { get; } = new List<Type>();

            public FakeTargetResolver(params object[] targets)
            {
                _targets = targets;
            }

            public IReadOnlyList<object> FindTargets(Type targetType)
            {
                RequestedTargetTypes.Add(targetType);
                return _targets;
            }

            public void SetTargets(params object[] targets)
            {
                _targets = targets;
            }
        }
    }
}
