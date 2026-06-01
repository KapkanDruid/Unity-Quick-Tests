using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuickTestCodegen.Consumer;
using UnityEngine;
using UnityQuickTests.Codegen.Editor;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestILPostProcessorTests
    {
        [SetUp]
        public void SetUp()
        {
            QuickTestInstanceRegistry.Clear();
            AutoRegisteredPlainTarget.InvocationCount = 0;
            ChainedConstructorTarget.InvocationCount = 0;
            ManualFallbackTarget.InvocationCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            QuickTestInstanceRegistry.Clear();
        }

        [Test]
        public void CodeGenAssembly_UsesUnityRecognizedName()
        {
            Assert.That(
                typeof(QuickTestILPostProcessor).Assembly.GetName().Name,
                Is.EqualTo("Unity.UrbanDruids.UnityQuickTests.CodeGen")
            );
        }

        [Test]
        public void ShouldProcessAssembly_RequiresEditorRuntimeConsumerAssembly()
        {
            Assert.That(
                QuickTestILPostProcessorFilter.ShouldProcessAssembly(
                    "Game.Feature",
                    new[] { "C:/Temp/UnityQuickTests.Runtime.dll" },
                    new[] { "UNITY_EDITOR" }),
                Is.True
            );

            Assert.That(
                QuickTestILPostProcessorFilter.ShouldProcessAssembly(
                    "Game.Feature",
                    new[] { "C:/Temp/UnityQuickTests.Runtime.dll" },
                    Array.Empty<string>()),
                Is.False
            );

            Assert.That(
                QuickTestILPostProcessorFilter.ShouldProcessAssembly(
                    "Game.Feature",
                    new[] { "C:/Temp/Other.dll" },
                    new[] { "UNITY_EDITOR" }),
                Is.False
            );
        }

        [Test]
        public void ShouldProcessAssembly_ExcludesPackageAndVendorAssemblies()
        {
            string[] references = { "C:/Temp/UnityQuickTests.Runtime.dll" };
            string[] defines = { "UNITY_EDITOR" };

            Assert.That(QuickTestILPostProcessorFilter.ShouldProcessAssembly("UnityQuickTests.Editor", references, defines), Is.False);
            Assert.That(QuickTestILPostProcessorFilter.ShouldProcessAssembly("Unity.Engine", references, defines), Is.False);
            Assert.That(QuickTestILPostProcessorFilter.ShouldProcessAssembly("System.Runtime", references, defines), Is.False);
            Assert.That(QuickTestILPostProcessorFilter.ShouldProcessAssembly("Microsoft.CSharp", references, defines), Is.False);
            Assert.That(QuickTestILPostProcessorFilter.ShouldProcessAssembly("Newtonsoft.Json", references, defines), Is.False);
        }

        [Test]
        public void ConstructorInjection_RegistersPlainCSharpTarget()
        {
            var target = new AutoRegisteredPlainTarget();

            object[] targets = QuickTestInstanceRegistry
                .FindTargets(typeof(AutoRegisteredPlainTarget))
                .ToArray();

            Assert.That(targets, Is.EqualTo(new object[] { target }));
        }

        [Test]
        public void ConstructorInjection_AllowsQuickTestInvocationWithoutManualRegister()
        {
            new AutoRegisteredPlainTarget();

            CreateMethod(typeof(AutoRegisteredPlainTarget), nameof(AutoRegisteredPlainTarget.Run)).Invoke();

            Assert.That(AutoRegisteredPlainTarget.InvocationCount, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorInjection_ChainedConstructorRegistersSingleTarget()
        {
            var target = new ChainedConstructorTarget();

            object[] targets = QuickTestInstanceRegistry
                .FindTargets(typeof(ChainedConstructorTarget))
                .ToArray();

            Assert.That(target.Value, Is.EqualTo(7));
            Assert.That(targets, Is.EqualTo(new object[] { target }));

            CreateMethod(typeof(ChainedConstructorTarget), nameof(ChainedConstructorTarget.Run)).Invoke();

            Assert.That(ChainedConstructorTarget.InvocationCount, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorInjection_ManualRegisterFallbackDoesNotDuplicateInvocation()
        {
            new ManualFallbackTarget();

            CreateMethod(typeof(ManualFallbackTarget), nameof(ManualFallbackTarget.Run)).Invoke();

            Assert.That(ManualFallbackTarget.InvocationCount, Is.EqualTo(1));
        }

        private static QuickTestMethod CreateMethod(Type targetType, string methodName)
        {
            MethodInfo method = targetType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public
            );

            return new QuickTestMethod(method);
        }
    }
}
