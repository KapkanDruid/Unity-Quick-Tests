using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace UnityQuickTests.Codegen.Editor
{
    public sealed class QuickTestILPostProcessor : ILPostProcessor
    {
        private const string RuntimeAssemblyName = "UnityQuickTests.Runtime";
        private const string RegistryTypeName = "UnityQuickTests.QuickTestInstanceRegistry";
        private const string HotkeyAttributeName = "UnityQuickTests.QuickTestHotkeyAttribute";
        private const string ScheduleAttributeName = "UnityQuickTests.QuickTestScheduleAttribute";
        private const string UnityObjectTypeName = "UnityEngine.Object";
        private const string CompilerGeneratedAttributeName = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

        public override ILPostProcessor GetInstance()
        {
            return this;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return QuickTestILPostProcessorFilter.ShouldProcessAssembly(
                compiledAssembly?.Name,
                compiledAssembly?.References,
                compiledAssembly?.Defines
            );
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
                return null;

            var diagnostics = new List<DiagnosticMessage>();

            try
            {
                return ProcessInternal(compiledAssembly, diagnostics);
            }
            catch (Exception exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticType.Error,
                    $"Unity Quick Tests IL post-processing failed for {compiledAssembly.Name}: {exception}"
                ));

                return new ILPostProcessResult(null, diagnostics);
            }
        }

        private static ILPostProcessResult ProcessInternal(
            ICompiledAssembly compiledAssembly,
            List<DiagnosticMessage> diagnostics)
        {
            using (var resolver = CreateAssemblyResolver(compiledAssembly))
            using (var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData))
            using (var pdbStream = CreatePdbStream(compiledAssembly))
            {
                bool hasSymbols = pdbStream != null;
                var readerParameters = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadingMode = ReadingMode.Immediate,
                    ReadSymbols = hasSymbols,
                    SymbolReaderProvider = hasSymbols ? new PortablePdbReaderProvider() : null,
                    SymbolStream = pdbStream
                };

                AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(peStream, readerParameters);
                bool changed = InjectRegistrations(assembly.MainModule, diagnostics);

                if (!changed)
                    return diagnostics.Count == 0 ? null : new ILPostProcessResult(null, diagnostics);

                using (var outputPeStream = new MemoryStream())
                using (var outputPdbStream = hasSymbols ? new MemoryStream() : null)
                {
                    var writerParameters = new WriterParameters
                    {
                        WriteSymbols = hasSymbols,
                        SymbolWriterProvider = hasSymbols ? new PortablePdbWriterProvider() : null,
                        SymbolStream = outputPdbStream
                    };

                    assembly.Write(outputPeStream, writerParameters);

                    return new ILPostProcessResult(
                        new InMemoryAssembly(
                            outputPeStream.ToArray(),
                            hasSymbols ? outputPdbStream.ToArray() : compiledAssembly.InMemoryAssembly.PdbData
                        ),
                        diagnostics
                    );
                }
            }
        }

        private static DefaultAssemblyResolver CreateAssemblyResolver(ICompiledAssembly compiledAssembly)
        {
            var resolver = new DefaultAssemblyResolver();

            foreach (string reference in compiledAssembly.References ?? Array.Empty<string>())
            {
                string directory = Path.GetDirectoryName(reference);

                if (!string.IsNullOrEmpty(directory))
                {
                    resolver.AddSearchDirectory(directory);
                }
            }

            return resolver;
        }

        private static MemoryStream CreatePdbStream(ICompiledAssembly compiledAssembly)
        {
            byte[] pdbData = compiledAssembly.InMemoryAssembly.PdbData;
            return pdbData == null || pdbData.Length == 0 ? null : new MemoryStream(pdbData);
        }

        private static bool InjectRegistrations(ModuleDefinition module, List<DiagnosticMessage> diagnostics)
        {
            TypeDefinition[] candidateTypes = GetAllTypes(module.Types)
                .Where(HasSupportedInstanceQuickTestMethod)
                .ToArray();

            if (candidateTypes.Length == 0)
                return false;

            TypeDefinition[] injectableTypes = candidateTypes
                .Where(type => CanInject(type, diagnostics))
                .ToArray();

            if (injectableTypes.Length == 0)
                return false;

            if (!TryCreateRegisterMethodReference(module, diagnostics, out MethodReference registerMethod))
                return false;

            bool changed = false;

            foreach (TypeDefinition type in injectableTypes)
            {
                changed |= InjectIntoType(type, registerMethod, diagnostics);
            }

            return changed;
        }

        private static IEnumerable<TypeDefinition> GetAllTypes(IEnumerable<TypeDefinition> types)
        {
            foreach (TypeDefinition type in types)
            {
                yield return type;

                foreach (TypeDefinition nestedType in GetAllTypes(type.NestedTypes))
                {
                    yield return nestedType;
                }
            }
        }

        private static bool HasSupportedInstanceQuickTestMethod(TypeDefinition type)
        {
            return type.Methods.Any(method =>
                !method.IsStatic &&
                !method.IsConstructor &&
                !method.HasGenericParameters &&
                method.ReturnType.FullName == type.Module.TypeSystem.Void.FullName &&
                method.Parameters.Count == 0 &&
                HasQuickTestAttribute(method)
            );
        }

        private static bool HasQuickTestAttribute(MethodDefinition method)
        {
            return method.CustomAttributes.Any(attribute =>
                attribute.AttributeType.FullName == HotkeyAttributeName ||
                attribute.AttributeType.FullName == ScheduleAttributeName
            );
        }

        private static bool CanInject(TypeDefinition type, List<DiagnosticMessage> diagnostics)
        {
            if (!type.IsClass)
                return Skip(type, diagnostics, "target type is not a class");

            if (type.HasGenericParameters)
                return Skip(type, diagnostics, "generic target types are not supported");

            if (type.IsValueType)
                return Skip(type, diagnostics, "value type targets are not supported");

            if (type.IsAbstract && type.IsSealed)
                return Skip(type, diagnostics, "static target types are not supported");

            if (type.IsAbstract)
                return Skip(type, diagnostics, "abstract target types are not supported");

            if (IsCompilerGenerated(type))
                return Skip(type, diagnostics, "compiler-generated target types are not supported");

            if (InheritsFrom(type, UnityObjectTypeName))
                return Skip(type, diagnostics, "UnityEngine.Object targets use Unity object lookup");

            return true;
        }

        private static bool Skip(TypeDefinition type, List<DiagnosticMessage> diagnostics, string reason)
        {
            diagnostics.Add(CreateDiagnostic(
                DiagnosticType.Warning,
                $"Unity Quick Tests skipped IL registration for {type.FullName}: {reason}."
            ));

            return false;
        }

        private static bool IsCompilerGenerated(TypeDefinition type)
        {
            return type.Name.IndexOf('<') >= 0 ||
                type.CustomAttributes.Any(attribute =>
                    attribute.AttributeType.FullName == CompilerGeneratedAttributeName
                );
        }

        private static bool InheritsFrom(TypeDefinition type, string baseTypeName)
        {
            TypeReference baseType = type.BaseType;

            while (baseType != null)
            {
                if (baseType.FullName == baseTypeName)
                    return true;

                TypeDefinition resolvedBaseType;

                try
                {
                    resolvedBaseType = baseType.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    return false;
                }

                if (resolvedBaseType == null)
                    return false;

                baseType = resolvedBaseType.BaseType;
            }

            return false;
        }

        private static bool InjectIntoType(
            TypeDefinition type,
            MethodReference registerMethod,
            List<DiagnosticMessage> diagnostics)
        {
            bool changed = false;

            foreach (MethodDefinition constructor in type.Methods.Where(method => method.IsConstructor && !method.IsStatic))
            {
                if (!constructor.HasBody)
                {
                    diagnostics.Add(CreateDiagnostic(
                        DiagnosticType.Warning,
                        $"Unity Quick Tests skipped constructor registration for {type.FullName}: constructor has no body."
                    ));

                    continue;
                }

                if (CallsThisConstructor(constructor, type))
                    continue;

                InjectRegisterCall(constructor, registerMethod);
                changed = true;
            }

            return changed;
        }

        private static bool CallsThisConstructor(MethodDefinition constructor, TypeDefinition type)
        {
            return constructor.Body.Instructions.Any(instruction =>
                instruction.OpCode == OpCodes.Call &&
                instruction.Operand is MethodReference method &&
                method.Name == ".ctor" &&
                method.DeclaringType.FullName == type.FullName
            );
        }

        private static void InjectRegisterCall(MethodDefinition constructor, MethodReference registerMethod)
        {
            ILProcessor processor = constructor.Body.GetILProcessor();
            Instruction[] returnInstructions = constructor.Body.Instructions
                .Where(instruction => instruction.OpCode == OpCodes.Ret)
                .ToArray();

            foreach (Instruction returnInstruction in returnInstructions)
            {
                processor.InsertBefore(returnInstruction, processor.Create(OpCodes.Ldarg_0));
                processor.InsertBefore(returnInstruction, processor.Create(OpCodes.Call, registerMethod));
            }
        }

        private static bool TryCreateRegisterMethodReference(
            ModuleDefinition module,
            List<DiagnosticMessage> diagnostics,
            out MethodReference methodReference)
        {
            methodReference = null;

            AssemblyNameReference runtimeAssembly = module.AssemblyReferences.FirstOrDefault(reference =>
                reference.Name == RuntimeAssemblyName
            );

            if (runtimeAssembly == null)
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticType.Warning,
                    $"Unity Quick Tests skipped IL registration for {module.Name}: missing {RuntimeAssemblyName} assembly reference."
                ));

                return false;
            }

            var registryType = new TypeReference(
                "UnityQuickTests",
                "QuickTestInstanceRegistry",
                module,
                runtimeAssembly
            );
            var registerMethod = new MethodReference(
                "Register",
                module.TypeSystem.Void,
                registryType
            )
            {
                HasThis = false
            };

            registerMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));

            methodReference = module.ImportReference(registerMethod);
            return true;
        }

        private static DiagnosticMessage CreateDiagnostic(DiagnosticType diagnosticType, string message)
        {
            return new DiagnosticMessage
            {
                DiagnosticType = diagnosticType,
                MessageData = message
            };
        }
    }

    internal static class QuickTestILPostProcessorFilter
    {
        private const string RuntimeAssemblyName = "UnityQuickTests.Runtime";
        private const string UnityEditorDefine = "UNITY_EDITOR";

        private static readonly string[] ExcludedAssemblyPrefixes =
        {
            "UnityQuickTests.",
            "Unity.",
            "UnityEngine.",
            "UnityEditor.",
            "System.",
            "Microsoft.",
            "Mono.",
            "nunit.",
            "Newtonsoft.",
            "Bee.",
            "NiceIO"
        };

        internal static bool ShouldProcessAssembly(
            string assemblyName,
            IEnumerable<string> references,
            IEnumerable<string> defines)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            if (ExcludedAssemblyPrefixes.Any(prefix =>
                assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (defines == null || !defines.Contains(UnityEditorDefine))
                return false;

            return references != null && references.Any(IsRuntimeReference);
        }

        private static bool IsRuntimeReference(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return false;

            return string.Equals(
                Path.GetFileNameWithoutExtension(reference),
                RuntimeAssemblyName,
                StringComparison.Ordinal
            );
        }
    }
}
