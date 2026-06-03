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
                    new[] { "C:/Temp/UnityQuickTests.Runtime.dll" },
                    new[] { "UNITY_STANDALONE_WIN", "UNITY_INCLUDE_TESTS", "ENABLE_MONO" }),
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
        public void InjectRegistrations_NoQuickTestMetadata_DoesNotRequireRuntimeAssemblyReference()
        {
            MethodInfo injectRegistrations = typeof(QuickTestILPostProcessor).GetMethod(
                "InjectRegistrations",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            object module = CreateEmptyCecilModule(
                injectRegistrations.GetParameters()[0].ParameterType
            );
            object diagnostics = Activator.CreateInstance(
                injectRegistrations.GetParameters()[1].ParameterType
            );

            bool changed = (bool)injectRegistrations.Invoke(
                null,
                new[] { module, diagnostics }
            );
            object[] diagnosticItems = ((System.Collections.IEnumerable)diagnostics)
                .Cast<object>()
                .ToArray();

            Assert.That(changed, Is.False);
            Assert.That(diagnosticItems, Is.Empty);
        }

        [Test]
        public void InjectRegistrations_QuickTestMetadataWithoutRuntimeReference_DoesNotThrow()
        {
            MethodInfo injectRegistrations = typeof(QuickTestILPostProcessor).GetMethod(
                "InjectRegistrations",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            object module = CreateCecilModuleWithQuickTestCandidate(
                injectRegistrations.GetParameters()[0].ParameterType
            );
            object diagnostics = Activator.CreateInstance(
                injectRegistrations.GetParameters()[1].ParameterType
            );

            bool changed = (bool)injectRegistrations.Invoke(
                null,
                new[] { module, diagnostics }
            );
            object[] diagnosticItems = ((System.Collections.IEnumerable)diagnostics)
                .Cast<object>()
                .ToArray();

            Assert.That(changed, Is.False);
            Assert.That(diagnosticItems, Has.Length.EqualTo(1));
            Assert.That(GetDiagnosticMessage(diagnosticItems[0]), Does.Contain("missing UnityQuickTests.Runtime assembly reference"));
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

        private static object CreateEmptyCecilModule(Type moduleDefinitionType)
        {
            Type moduleKindType = moduleDefinitionType.Assembly.GetType(
                "Mono.Cecil.ModuleKind",
                true
            );
            MethodInfo createModule = moduleDefinitionType.GetMethod(
                "CreateModule",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), moduleKindType },
                null
            );

            return createModule.Invoke(
                null,
                new[] { "NoQuickTests", Enum.Parse(moduleKindType, "Dll") }
            );
        }

        private static object CreateCecilModuleWithQuickTestCandidate(Type moduleDefinitionType)
        {
            object module = CreateEmptyCecilModule(moduleDefinitionType);
            Assembly cecilAssembly = moduleDefinitionType.Assembly;
            object typeSystem = moduleDefinitionType.GetProperty("TypeSystem").GetValue(module);
            object voidType = typeSystem.GetType().GetProperty("Void").GetValue(typeSystem);
            object objectType = typeSystem.GetType().GetProperty("Object").GetValue(typeSystem);
            object coreLibrary = typeSystem.GetType().GetProperty("CoreLibrary").GetValue(typeSystem);

            Type typeDefinitionType = cecilAssembly.GetType("Mono.Cecil.TypeDefinition", true);
            Type typeAttributesType = cecilAssembly.GetType("Mono.Cecil.TypeAttributes", true);
            object typeAttributes = CombineEnumFlags(typeAttributesType, "Public", "Class");
            object targetType = Activator.CreateInstance(
                typeDefinitionType,
                "Game",
                "QuickTestTarget",
                typeAttributes,
                objectType
            );

            Type methodDefinitionType = cecilAssembly.GetType("Mono.Cecil.MethodDefinition", true);
            Type methodAttributesType = cecilAssembly.GetType("Mono.Cecil.MethodAttributes", true);
            object methodAttributes = CombineEnumFlags(methodAttributesType, "Public", "HideBySig");
            object method = Activator.CreateInstance(
                methodDefinitionType,
                "Run",
                methodAttributes,
                voidType
            );

            Type typeReferenceType = cecilAssembly.GetType("Mono.Cecil.TypeReference", true);
            object attributeType = Activator.CreateInstance(
                typeReferenceType,
                "UnityQuickTests",
                "QuickTestHotkeyAttribute",
                module,
                coreLibrary
            );

            Type methodReferenceType = cecilAssembly.GetType("Mono.Cecil.MethodReference", true);
            object attributeConstructor = Activator.CreateInstance(
                methodReferenceType,
                ".ctor",
                voidType,
                attributeType
            );
            methodReferenceType.GetProperty("HasThis").SetValue(attributeConstructor, true);

            Type customAttributeType = cecilAssembly.GetType("Mono.Cecil.CustomAttribute", true);
            object attribute = Activator.CreateInstance(customAttributeType, attributeConstructor);

            AddToCecilCollection(methodDefinitionType, method, "CustomAttributes", attribute);
            AddToCecilCollection(typeDefinitionType, targetType, "Methods", method);
            AddToCecilCollection(moduleDefinitionType, module, "Types", targetType);

            return module;
        }

        private static object CombineEnumFlags(Type enumType, params string[] names)
        {
            int value = names
                .Select(name => Convert.ToInt32(Enum.Parse(enumType, name)))
                .Aggregate(0, (combined, current) => combined | current);

            return Enum.ToObject(enumType, value);
        }

        private static void AddToCecilCollection(
            Type ownerType,
            object owner,
            string propertyName,
            object item)
        {
            object collection = ownerType.GetProperty(propertyName).GetValue(owner);
            collection.GetType().GetMethod("Add").Invoke(collection, new[] { item });
        }

        private static string GetDiagnosticMessage(object diagnostic)
        {
            return (string)diagnostic
                .GetType()
                .GetProperty("MessageData")
                .GetValue(diagnostic);
        }
    }
}
