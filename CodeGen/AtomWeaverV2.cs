// ReSharper disable FieldCanBeMadeReadOnly.Local

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using UniMob.Core;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace UniMob.Editor.Weaver
{
    internal class AtomWeaverV2
    {
        private const string ValuePropertyName = nameof(ComputedAtom<int>.Value);
        private const string ConstructorName = ".ctor";
        private const string DirectEvaluateMethodName = nameof(ComputedAtom<int>.DirectEvaluate);
        private const string CompAndInvalidateMethodName = nameof(ComputedAtom<int>.CompareAndInvalidate);
        private const string CreateAtomMethodName = nameof(CodeGenAtom.CreatePooled);
        private const string ThrowIfDisposedMethodName = nameof(LifetimeScopeExtension.ThrowIfDisposed);
        private const string KeepAliveParameterName = nameof(AtomAttribute.KeepAlive);

        private List<DiagnosticMessage> _diagnosticMessages = new List<DiagnosticMessage>();

        private ModuleDefinition _module;

        private TypeReference _atomType;
        private TypeReference _lifetimeScopeInterfaceType;

        private MethodReference _atomCreateMethod;
        private MethodReference _atomGetValueMethod;
        private MethodReference _atomDirectEvalMethod;
        private MethodReference _atomCompAndInvalidateMethod;
        private MethodReference _throwIfDisposedMethod;

        private MethodReference _atomPullCtorMethod;

        private bool _generateDebugNames;

        public List<DiagnosticMessage> Weave(AssemblyDefinition assembly, out bool didAnyChange)
        {
            Prepare(assembly);

            var allProperties = _module
                .GetAllTypes()
                .SelectMany(type => type.Properties);

            didAnyChange = false;

            foreach (var property in allProperties)
            {
                didAnyChange |= Weave(property);
            }

            return _diagnosticMessages;
        }

        private void Prepare(AssemblyDefinition assembly)
        {
            _generateDebugNames = Helpers.GetCustomAttribute<AtomGenerateDebugNamesAttribute>(assembly) != null;
#if UNIMOB_ATOM_GENERATE_DEBUG_NAMES
            _generateDebugNames = true;
#endif

            _module = assembly.MainModule;

            _atomType = _module.ImportReference(typeof(ComputedAtom<>));
            _lifetimeScopeInterfaceType = _module.ImportReference(typeof(ILifetimeScope));

            var atomTypeDef = _atomType.Resolve();
            var atomPullDef = _module.ImportReference(typeof(Func<>)).Resolve();
            var atomFactoryDef = _module.ImportReference(typeof(CodeGenAtom)).Resolve();
            var lifetimeScopeExtensionsDef = _module.ImportReference(typeof(LifetimeScopeExtension)).Resolve();

            _atomGetValueMethod = _module.ImportReference(atomTypeDef.FindProperty(ValuePropertyName).GetMethod);

            _atomCreateMethod = _module.ImportReference(atomFactoryDef.FindMethod(CreateAtomMethodName, 4));
            _atomDirectEvalMethod = _module.ImportReference(atomTypeDef.FindMethod(DirectEvaluateMethodName, 0));
            _atomCompAndInvalidateMethod =
                _module.ImportReference(atomTypeDef.FindMethod(CompAndInvalidateMethodName, 1));

            _atomPullCtorMethod = _module.ImportReference(atomPullDef.FindMethod(ConstructorName, 2));
            _throwIfDisposedMethod =
                _module.ImportReference(lifetimeScopeExtensionsDef.FindMethod(ThrowIfDisposedMethodName, 1));
        }

        public bool Weave(PropertyDefinition property)
        {
            var atomAttribute = Helpers.GetCustomAttribute<AtomAttribute>(property);
            if (atomAttribute == null)
            {
                return false;
            }

            if (property.DeclaringType.HasGenericParameters)
            {
                _diagnosticMessages.Add(UserError.AtomAttributeCannotBeUsedOnGenericClasses(property));
                return false;
            }

            if (!property.DeclaringType.IsClass || property.DeclaringType.IsValueType)
            {
                _diagnosticMessages.Add(UserError.AtomAttributeCanBeUsedOnlyOnClassMembers(property));
                return false;
            }

            if (!property.DeclaringType.IsInterfaceImplemented(_lifetimeScopeInterfaceType))
            {
                _diagnosticMessages.Add(UserError.AtomAttributeCanBeUsedOnlyOnLifetimeScope(property));
                return false;
            }

            if (property.GetMethod == null)
            {
                _diagnosticMessages.Add(UserError.CannotUseAtomAttributeOnSetOnlyProperty(property));
                return false;
            }

            if (property.GetMethod.IsStatic)
            {
                _diagnosticMessages.Add(UserError.CannotUseAtomAttributeOnStaticProperty(property));
                return false;
            }

            if (property.GetMethod.IsAbstract)
            {
                _diagnosticMessages.Add(UserError.CannotUseAtomAttributeOnAbstractProperty(property));
                return false;
            }

            var atomOptions = new AtomOptions
            {
                KeepAlive = atomAttribute.GetArgumentValueOrDefault(KeepAliveParameterName, false),
            };

            property.CustomAttributes.Remove(atomAttribute);

            FixAutoPropertyBackingField(property);

            var atomField = CreateAtomField(property);
            property.DeclaringType.Fields.Add(atomField);

            new AtomGetterMethodWeaver(this, property, atomField, atomOptions).Weave();

            if (property.SetMethod != null)
            {
                new AtomSetterMethodWeaver(this, property, atomField).Weave();
            }

            return true;
        }

        private void FixAutoPropertyBackingField(PropertyDefinition property)
        {
            var il = property.GetMethod.Body.Instructions;
            if (il.Count == 3 &&
                il[0].OpCode == OpCodes.Ldarg_0 &&
                il[2].OpCode == OpCodes.Ret &&
                il[1].OpCode == OpCodes.Ldfld &&
                il[1].Operand is FieldDefinition backingField &&
                backingField.Name.Equals($"<{property.Name}>k__BackingField"))
            {
                var name = $"__{property.Name}__BackingField";
                backingField.Name = Helpers.GenerateUniqueFieldName(property.DeclaringType, name);
            }
        }

        private FieldDefinition CreateAtomField(PropertyDefinition property)
        {
            var name = Helpers.GenerateUniqueFieldName(property.DeclaringType, $"__{property.Name}");
            var atomFieldType = Helpers.MakeGenericType(_atomType, property.PropertyType);
            return new FieldDefinition(name, FieldAttributes.Private, atomFieldType);
        }

        private struct AtomOptions
        {
            public bool KeepAlive;
        }

        private struct AtomGetterMethodWeaver
        {
            private FieldReference _atomField;
            private AtomOptions _options;
            private PropertyDefinition _property;

            private string _atomDebugName;

            private Instruction _nullCheckEndInstruction;
            private Instruction _directEvalEndInstruction;

            private MethodReference _atomCreateMethod;
            private MethodReference _atomPullCtorMethod;
            private MethodReference _tryEnterMethod;
            private MethodReference _atomGetMethod;
            private MethodReference _throwIfDisposedMethod;

            public AtomGetterMethodWeaver(AtomWeaverV2 weaver, PropertyDefinition property, FieldReference atomField,
                AtomOptions options)
            {
                _atomField = atomField;
                _options = options;
                _property = property;

                _atomDebugName = weaver._generateDebugNames
                    ? $"{property.DeclaringType.FullName}::{property.Name}"
                    : null;

                _nullCheckEndInstruction = Instruction.Create(OpCodes.Nop);
                _directEvalEndInstruction = Instruction.Create(OpCodes.Nop);

                var propertyType = property.PropertyType;
                _atomCreateMethod = Helpers.MakeGenericMethod(weaver._atomCreateMethod, propertyType);
                _atomPullCtorMethod = Helpers.MakeHostInstanceGeneric(weaver._atomPullCtorMethod, propertyType);
                _tryEnterMethod = Helpers.MakeHostInstanceGeneric(weaver._atomDirectEvalMethod, propertyType);
                _atomGetMethod = Helpers.MakeHostInstanceGeneric(weaver._atomGetValueMethod, propertyType);
                _throwIfDisposedMethod = weaver._throwIfDisposedMethod;
            }

            public void Weave()
            {
                var body = _property.GetMethod.Body;
                var instructions = body.Instructions;
                var index = 0;

                Prepend(ref index, instructions);

                body.OptimizeMacros();
            }

            private void Prepend(ref int ind, IList<Instruction> il)
            {
                // LifetimeScopeExtension.ThrowIfDisposed(this);
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Call, _throwIfDisposedMethod));

                // if (atom != null) goto nullCheckEnd;
                il.Insert(ind++, Instruction.Create(OpCodes.Nop));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Brtrue, _nullCheckEndInstruction));

                // atom = new ComputedAtom<int>("name", get_atom);
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0)); // atom scope
                il.Insert(ind++, _atomDebugName != null
                    ? Instruction.Create(OpCodes.Ldstr, _atomDebugName)
                    : Instruction.Create(OpCodes.Ldnull)); // debugName
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0)); // getter_method

                if (_property.GetMethod.IsVirtual)
                {
                    il.Insert(ind++, Instruction.Create(OpCodes.Dup));
                    il.Insert(ind++, Instruction.Create(OpCodes.Ldvirtftn, _property.GetMethod));
                }
                else
                {
                    il.Insert(ind++, Instruction.Create(OpCodes.Ldftn, _property.GetMethod));
                }

                il.Insert(ind++, Instruction.Create(OpCodes.Newobj, _atomPullCtorMethod));
                il.Insert(ind++, Instruction.Create(_options.KeepAlive ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Call, _atomCreateMethod)); // create atom
                il.Insert(ind++, Instruction.Create(OpCodes.Stfld, _atomField));

                // end if
                il.Insert(ind++, _nullCheckEndInstruction);

                // if (!_atom_Result.TryEnter()) goto tryEnterEnd;
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Callvirt, _tryEnterMethod));
                il.Insert(ind++, Instruction.Create(OpCodes.Brtrue, _directEvalEndInstruction));

                // return atom.Value
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Callvirt, _atomGetMethod));
                il.Insert(ind++, Instruction.Create(OpCodes.Ret));

                il.Insert(ind++, _directEvalEndInstruction);
            }
        }

        private struct AtomSetterMethodWeaver
        {
            private FieldReference _atomField;
            private PropertyDefinition _property;

            private Instruction _nullCheckEndInstruction;

            private MethodReference _compAndInvalidateMethod;
            private MethodReference _throwIfDisposedMethod;

            public AtomSetterMethodWeaver(AtomWeaverV2 weaver, PropertyDefinition property, FieldReference atomField)
            {
                _atomField = atomField;
                _property = property;

                _nullCheckEndInstruction = Instruction.Create(OpCodes.Nop);

                var propertyType = property.PropertyType;
                _compAndInvalidateMethod =
                    Helpers.MakeHostInstanceGeneric(weaver._atomCompAndInvalidateMethod, propertyType);
                _throwIfDisposedMethod = weaver._throwIfDisposedMethod;
            }

            public void Weave()
            {
                var body = _property.SetMethod.Body;
                var instructions = body.Instructions;
                var index = 0;
                Prepend(ref index, instructions);

                body.OptimizeMacros();
            }

            private void Prepend(ref int ind, IList<Instruction> il)
            {
                // LifetimeScopeExtension.ThrowIfDisposed(this);
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Call, _throwIfDisposedMethod));

                // if (atom == null) goto nullCheckEnd;
                il.Insert(ind++, Instruction.Create(OpCodes.Nop));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Brfalse, _nullCheckEndInstruction));

                // if (atom.CompareAndInvalidate()) goto nullCheckEnd;
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_1));
                il.Insert(ind++, Instruction.Create(OpCodes.Callvirt, _compAndInvalidateMethod));
                il.Insert(ind++, Instruction.Create(OpCodes.Brtrue, _nullCheckEndInstruction));

                // return;
                il.Insert(ind++, Instruction.Create(OpCodes.Ret));

                il.Insert(ind++, _nullCheckEndInstruction);
            }
        }
    }
}