#if UNITY_EDITOR && MONO_CECIL && UNIMOB_CODEGEN_ENABLED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Mono.Cecil;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using Debug = UnityEngine.Debug;
using UnityAssembly = UnityEditor.Compilation.Assembly;

namespace UniMob.Editor.Weaver
{
    public static class CompilationHook
    {
        private const string UniMobWeavedFlagName = "UniMobWeaved";
        private const string UniMobRuntimeAssemblyName = "UniMob";

        private static readonly Predicate<CompilerMessage> IsErrorMessage = m => m.type == CompilerMessageType.Error;

        [InitializeOnLoadMethod]
        private static void OnInitializeOnLoad()
        {
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;

            if (!SessionState.GetBool(UniMobWeavedFlagName, false))
            {
                SessionState.SetBool(UniMobWeavedFlagName, true);
                WeaveExistingAssemblies(CompilationPipeline.GetAssemblies());
            }
        }

        private static void WeaveExistingAssemblies(UnityAssembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                if (File.Exists(assembly.outputPath))
                {
                    OnCompilationFinished(assembly.outputPath, new CompilerMessage[0]);
                }
            }

            InternalEditorUtility.RequestScriptReload();
        }

        private static UnityAssembly FindUniMobRuntime(UnityAssembly[] assemblies)
        {
            for (var index = 0; index < assemblies.Length; index++)
            {
                if (assemblies[index].name == UniMobRuntimeAssemblyName)
                {
                    return assemblies[index];
                }
            }

            return null;
        }

        private static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            var sw = Stopwatch.StartNew();

            if (Array.FindIndex(messages, IsErrorMessage) != -1)
            {
                return;
            }

            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (assemblyName == null || assemblyName == UniMobRuntimeAssemblyName)
            {
                return;
            }

            if (assemblyPath.Contains("-Editor") || assemblyPath.Contains(".Editor"))
            {
                return;
            }

            var assemblies = CompilationPipeline.GetAssemblies();

            var uniMobRuntime = FindUniMobRuntime(assemblies);
            if (uniMobRuntime == null)
            {
                Debug.LogError("Failed to find UniMob runtime assembly");
                return;
            }

            if (!File.Exists(uniMobRuntime.outputPath))
            {
                return;
            }

            var dependencyPaths = new HashSet<string>
            {
                Path.GetDirectoryName(assemblyPath)
            };

            var shouldWeave = false;

            for (var asmIndex = 0; asmIndex < assemblies.Length; asmIndex++)
            {
                var assembly = assemblies[asmIndex];
                if (assembly.outputPath != assemblyPath) continue;

                for (var i = 0; i < assembly.compiledAssemblyReferences.Length; i++)
                {
                    var referencePath = assembly.compiledAssemblyReferences[i];
                    dependencyPaths.Add(Path.GetDirectoryName(referencePath));
                }

                for (var i = 0; i < assembly.assemblyReferences.Length; i++)
                {
                    var reference = assembly.assemblyReferences[i];
                    if (reference.outputPath == uniMobRuntime.outputPath)
                    {
                        shouldWeave = true;
                    }
                }

                break;
            }

            if (!shouldWeave)
            {
                return;
            }

            Weave(assemblyPath, dependencyPaths);

#if UNIMOB_CODEGEN_LOGGING_ENABLED
            Debug.Log($"Weaved {assemblyPath} in {sw.ElapsedMilliseconds}ms");
#endif
        }

        public static void Weave(string assemblyPath, HashSet<string> dependencies)
        {
            using (var resolver = new DefaultAssemblyResolver())
            using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath,
                new ReaderParameters
                {
                    ReadWrite = true, ReadSymbols = true, AssemblyResolver = resolver
                }))
            {
                resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));
                resolver.AddSearchDirectory(Helpers.UnityEngineDllDirectoryName());
                resolver.AddSearchDirectory(Helpers.GetEngineCoreModuleDirectoryName());

                if (dependencies != null)
                {
                    foreach (var path in dependencies)
                    {
                        resolver.AddSearchDirectory(path);
                    }
                }

                var dirty = new AtomWeaverV2().Weave(assembly);
                if (dirty)
                {
                    assembly.Write(new WriterParameters {WriteSymbols = true});
                }
            }
        }
    }
}
#endif