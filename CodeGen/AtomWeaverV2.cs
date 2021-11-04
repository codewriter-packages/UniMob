// ReSharper disable FieldCanBeMadeReadOnly.Local

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace UniMob.Editor.Weaver
{
    internal class AtomWeaverV2
    {
        private const string ValuePropertyName = nameof(ComputedAtom<int>.Value);
        private const string ConstructorName = ".ctor";
        private const string DirectEvaluateMethodName = nameof(ComputedAtom<int>.DirectEvaluate);
        private const string CompAndInvalidateMethodName = nameof(ComputedAtom<int>.CompareAndInvalidate);
        private const string CreateAtomMethodName = nameof(CodeGenAtom.Create);
        private const string KeepAliveParameterName = nameof(AtomAttribute.KeepAlive);
        private const string RequireReactionParameterName = nameof(AtomAttribute.RequireReaction);

        private List<DiagnosticMessage> _diagnosticMessages = new List<DiagnosticMessage>();

        private ModuleDefinition _module;

        private TypeReference _atomType;

        private MethodReference _atomCreateMethod;
        private MethodReference _atomGetValueMethod;
        private MethodReference _atomDirectEvalMethod;
        private MethodReference _atomCompAndInvalidateMethod;

        private MethodReference _atomPullCtorMethod;

        private bool generateDebugNames;

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
            generateDebugNames = Helpers.GetCustomAttribute<AtomGenerateDebugNamesAttribute>(assembly) != null;
            
            _module = assembly.MainModule;

            _atomType = _module.ImportReference(typeof(ComputedAtom<>));

            var atomTypeDef = _atomType.Resolve();
            var atomPullDef = _module.ImportReference(typeof(AtomPull<>)).Resolve();
            var atomFactoryDef = _module.ImportReference(typeof(CodeGenAtom)).Resolve();

            _atomGetValueMethod = _module.ImportReference(atomTypeDef.FindProperty(ValuePropertyName).GetMethod);

            _atomCreateMethod = _module.ImportReference(atomFactoryDef.FindMethod(CreateAtomMethodName, 4));
            _atomDirectEvalMethod = _module.ImportReference(atomTypeDef.FindMethod(DirectEvaluateMethodName, 0));
            _atomCompAndInvalidateMethod =
                _module.ImportReference(atomTypeDef.FindMethod(CompAndInvalidateMethodName, 1));

            _atomPullCtorMethod = _module.ImportReference(atomPullDef.FindMethod(ConstructorName, 2));
        }

        public bool Weave(PropertyDefinition property)
        {
            var atomAttribute = Helpers.GetCustomAttribute<AtomAttribute>(property);
            if (atomAttribute == null)
            {
                return false;
            }

            if (!property.DeclaringType.IsClass || property.DeclaringType.IsValueType)
            {
                _diagnosticMessages.Add(UserError.AtomAttributeCanBeUsedOnlyOnClassMembers(property));
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
                RequireReaction = atomAttribute.GetArgumentValueOrDefault(RequireReactionParameterName, false),
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
            public bool RequireReaction;
        }

        private struct AtomGetterMethodWeaver
        {
            private FieldReference _atomField;
            private AtomOptions _options;
            private PropertyDefinition _property;

            private string _atomDebugName;
            private VariableDefinition _resultVariable;

            private Instruction _nullCheckEndInstruction;
            private Instruction _directEvalEndInstruction;
            private Instruction _loadResultInstruction;

            private MethodReference _atomCreateMethod;
            private MethodReference _atomPullCtorMethod;
            private MethodReference _tryEnterMethod;
            private MethodReference _atomGetMethod;

            public AtomGetterMethodWeaver(AtomWeaverV2 weaver, PropertyDefinition property, FieldReference atomField,
                AtomOptions options)
            {
                _atomField = atomField;
                _options = options;
                _property = property;

                _atomDebugName = weaver.generateDebugNames
                    ? $"{property.DeclaringType.FullName}::{property.Name}"
                    : null;
                _resultVariable = new VariableDefinition(property.PropertyType);

                _nullCheckEndInstruction = Instruction.Create(OpCodes.Nop);
                _directEvalEndInstruction = Instruction.Create(OpCodes.Nop);
                _loadResultInstruction = Instruction.Create(OpCodes.Ldloc, _resultVariable);

                var propertyType = property.PropertyType;
                _atomCreateMethod = Helpers.MakeGenericMethod(weaver._atomCreateMethod, propertyType);
                _atomPullCtorMethod = Helpers.MakeHostInstanceGeneric(weaver._atomPullCtorMethod, propertyType);
                _tryEnterMethod = Helpers.MakeHostInstanceGeneric(weaver._atomDirectEvalMethod, propertyType);
                _atomGetMethod = Helpers.MakeHostInstanceGeneric(weaver._atomGetValueMethod, propertyType);

                var body = property.GetMethod.Body;
                body.InitLocals = true;
                body.Variables.Add(_resultVariable);
            }

            public void Weave()
            {
                var body = _property.GetMethod.Body;
                var instructions = body.Instructions;
                var index = 0;

                Prepend(ref index, instructions);

                while (index < instructions.Count)
                {
                    var current = instructions[index++];
                    if (current.OpCode != OpCodes.Ret)
                    {
                        continue;
                    }

                    current.OpCode = OpCodes.Nop;
                    current.Operand = null;

                    ReplaceReturn(ref index, instructions);
                }

                Append(ref index, instructions);

                body.OptimizeMacros();
            }

            private void Prepend(ref int ind, IList<Instruction> il)
            {
                // if (atom != null) goto nullCheckEnd;
                il.Insert(ind++, Instruction.Create(OpCodes.Nop));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Brtrue, _nullCheckEndInstruction));

                // atom = new ComputedAtom<int>("name", get_atom);
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, _atomDebugName != null
                    ? Instruction.Create(OpCodes.Ldstr, _atomDebugName)
                    : Instruction.Create(OpCodes.Ldnull)); // debugName
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0)); // getter_method
                il.Insert(ind++, Instruction.Create(OpCodes.Ldftn, _property.GetMethod));
                il.Insert(ind++, Instruction.Create(OpCodes.Newobj, _atomPullCtorMethod));
                il.Insert(ind++, Instruction.Create(_options.KeepAlive ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
                il.Insert(ind++, Instruction.Create(_options.RequireReaction ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
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
                il.Insert(ind++, Instruction.Create(OpCodes.Stloc, _resultVariable));
                il.Insert(ind++, Instruction.Create(OpCodes.Br, _loadResultInstruction));

                il.Insert(ind++, _directEvalEndInstruction);
            }

            private void ReplaceReturn(ref int ind, IList<Instruction> il)
            {
                il.Insert(ind++, Instruction.Create(OpCodes.Nop));
                il.Insert(ind++, Instruction.Create(OpCodes.Stloc, _resultVariable));
                il.Insert(ind++, Instruction.Create(OpCodes.Br, _loadResultInstruction));
            }

            private void Append(ref int index, IList<Instruction> il)
            {
                il.Insert(index++, _loadResultInstruction);
                il.Insert(index++, Instruction.Create(OpCodes.Ret));
            }
        }

        private struct AtomSetterMethodWeaver
        {
            private FieldReference _atomField;
            private PropertyDefinition _property;

            private Instruction _nullCheckEndInstruction;
            private Instruction _preReturnInstruction;

            private MethodReference _compAndInvalidateMethod;

            public AtomSetterMethodWeaver(AtomWeaverV2 weaver, PropertyDefinition property, FieldReference atomField)
            {
                _atomField = atomField;
                _property = property;

                _nullCheckEndInstruction = Instruction.Create(OpCodes.Nop);
                _preReturnInstruction = Instruction.Create(OpCodes.Nop);

                var propertyType = property.PropertyType;
                _compAndInvalidateMethod =
                    Helpers.MakeHostInstanceGeneric(weaver._atomCompAndInvalidateMethod, propertyType);
            }

            public void Weave()
            {
                var body = _property.SetMethod.Body;
                var instructions = body.Instructions;
                var index = 0;
                Prepend(ref index, instructions);

                body.Instructions.Insert(body.Instructions.Count - 1, _preReturnInstruction);

                body.OptimizeMacros();
            }

            private void Prepend(ref int ind, IList<Instruction> il)
            {
                // if (atom == null) goto nullCheckEnd;
                il.Insert(ind++, Instruction.Create(OpCodes.Nop));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Brfalse, _nullCheckEndInstruction));

                // atom.Invalidate()
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_1));
                il.Insert(ind++, Instruction.Create(OpCodes.Callvirt, _compAndInvalidateMethod));
                il.Insert(ind++, Instruction.Create(OpCodes.Brtrue, _nullCheckEndInstruction));

                il.Insert(ind++, Instruction.Create(OpCodes.Br, _preReturnInstruction));

                il.Insert(ind++, _nullCheckEndInstruction);
            }
        }
    }
}