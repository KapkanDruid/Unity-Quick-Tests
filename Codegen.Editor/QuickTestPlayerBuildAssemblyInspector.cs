using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UnityQuickTests.Codegen.Editor
{
    internal static class QuickTestPlayerBuildAssemblyInspector
    {
        private const string RegistryTypeName = "UnityQuickTests.QuickTestInstanceRegistry";
        private const string RegisterMethodName = "Register";

        internal static bool ContainsType(string assemblyPath, string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                throw new ArgumentException("Type name must be provided.", nameof(fullTypeName));

            using (AssemblyDefinition assembly = ReadAssembly(assemblyPath))
            {
                return GetAllTypes(assembly.MainModule.Types)
                    .Any(type => type.FullName == fullTypeName);
            }
        }

        internal static string[] FindRegistryRegisterCallSites(string assemblyPath)
        {
            var callSites = new List<string>();

            using (AssemblyDefinition assembly = ReadAssembly(assemblyPath))
            {
                foreach (TypeDefinition type in GetAllTypes(assembly.MainModule.Types))
                {
                    foreach (MethodDefinition method in type.Methods.Where(method => method.HasBody))
                    {
                        if (method.Body.Instructions.Any(IsRegistryRegisterCall))
                        {
                            callSites.Add($"{assembly.Name.Name}::{method.FullName}");
                        }
                    }
                }
            }

            return callSites.ToArray();
        }

        private static AssemblyDefinition ReadAssembly(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                throw new ArgumentException("Assembly path must be provided.", nameof(assemblyPath));

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException("Assembly file was not found.", assemblyPath);

            var readerParameters = new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,
                ReadSymbols = false
            };

            return AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
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

        private static bool IsRegistryRegisterCall(Instruction instruction)
        {
            if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                return false;

            var method = instruction.Operand as MethodReference;

            return method != null &&
                method.Name == RegisterMethodName &&
                method.DeclaringType != null &&
                method.DeclaringType.FullName == RegistryTypeName &&
                method.Parameters.Count == 1;
        }
    }
}
