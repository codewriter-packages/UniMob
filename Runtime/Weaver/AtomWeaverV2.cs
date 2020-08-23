#if UNITY_EDITOR && MONO_CECIL && UNIMOB_CODEGEN_ENABLED

// ReSharper disable FieldCanBeMadeReadOnly.Local

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using UnityEngine;

namespace UniMob.Editor.Weaver
{
    public class AtomWeaverV2
    {
        private const string ValuePropertyName = nameof(ComputedAtom<int>.Value);
        private const string ConstructorName = ".ctor";
        private const string DirectEvaluateMethodName = nameof(ComputedAtom<int>.DirectEvaluate);
        private const string InvalidateMethodName = nameof(ComputedAtom<int>.Invalidate);

        private ModuleDefinition _module;

        private TypeReference _atomType;

        private MethodReference _atomCtorMethod;
        private MethodReference _atomGetValueMethod;
        private MethodReference _atomDirectEvalMethod;
        private MethodReference _atomInvalidateMethod;

        private MethodReference _atomPullCtorMethod;

        public bool Weave(AssemblyDefinition assembly)
        {
            Prepare(assembly);

            var dirty = false;

            var allProperties = _module
                .GetAllTypes()
                .SelectMany(type => type.Properties);

            foreach (var property in allProperties)
            {
                dirty |= Weave(property);
            }

            return dirty;
        }

        private void Prepare(AssemblyDefinition assembly)
        {
            _module = assembly.MainModule;

            _atomType = _module.ImportReference(typeof(ComputedAtom<>));

            var atomTypeDef = _atomType.Resolve();
            var atomPullDef = _module.ImportReference(typeof(AtomPull<>)).Resolve();

            _atomGetValueMethod = _module.ImportReference(atomTypeDef.FindProperty(ValuePropertyName).GetMethod);

            _atomCtorMethod = _module.ImportReference(atomTypeDef.FindMethod(ConstructorName, 2));
            _atomDirectEvalMethod = _module.ImportReference(atomTypeDef.FindMethod(DirectEvaluateMethodName, 0));
            _atomInvalidateMethod = _module.ImportReference(atomTypeDef.FindMethod(InvalidateMethodName, 0));

            _atomPullCtorMethod = _module.ImportReference(atomPullDef.FindMethod(ConstructorName, 2));
        }

        public bool Weave(PropertyDefinition property)
        {
            var atomAttribute = Helpers.GetCustomAttribute<AtomAttribute>(property);
            if (atomAttribute == null)
            {
                return false;
            }

            property.CustomAttributes.Remove(atomAttribute);

            if (property.GetMethod == null)
            {
                Debug.LogError($"[UniMob] Atom attribute on set-only property '{property.FullName}' don't make sense");
                return false;
            }

            FixAutoPropertyBackingField(property);

            var atomField = CreateAtomField(property);
            property.DeclaringType.Fields.Add(atomField);

            new AtomGetterMethodWeaver(this, property, atomField).Weave();

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

        private struct AtomGetterMethodWeaver
        {
            private FieldReference _atomField;
            private PropertyDefinition _property;

            private string _atomDebugName;
            private VariableDefinition _resultVariable;

            private Instruction _nullCheckEndInstruction;
            private Instruction _directEvalEndInstruction;
            private Instruction _loadResultInstruction;

            private MethodReference _atomCtorMethod;
            private MethodReference _atomPullCtorMethod;
            private MethodReference _tryEnterMethod;
            private MethodReference _atomGetMethod;

            public AtomGetterMethodWeaver(AtomWeaverV2 weaver, PropertyDefinition property, FieldReference atomField)
            {
                _atomField = atomField;
                _property = property;

                _atomDebugName = $"{property.DeclaringType.FullName}::{property.Name}";
                _resultVariable = new VariableDefinition(property.PropertyType);

                _nullCheckEndInstruction = Instruction.Create(OpCodes.Nop);
                _directEvalEndInstruction = Instruction.Create(OpCodes.Nop);
                _loadResultInstruction = Instruction.Create(OpCodes.Ldloc, _resultVariable);

                var propertyType = property.PropertyType;
                _atomCtorMethod = Helpers.MakeHostInstanceGeneric(weaver._atomCtorMethod, propertyType);
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
                il.Insert(ind++, Instruction.Create(OpCodes.Ldstr, _atomDebugName)); // debugName
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0)); // getter_method
                il.Insert(ind++, Instruction.Create(OpCodes.Ldftn, _property.GetMethod));
                il.Insert(ind++, Instruction.Create(OpCodes.Newobj, _atomPullCtorMethod));
                il.Insert(ind++, Instruction.Create(OpCodes.Newobj, _atomCtorMethod)); // create atom
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

            private MethodReference _invalidateMethod;

            public AtomSetterMethodWeaver(AtomWeaverV2 weaver, PropertyDefinition property, FieldReference atomField)
            {
                _atomField = atomField;
                _property = property;

                _nullCheckEndInstruction = Instruction.Create(OpCodes.Nop);

                var propertyType = property.PropertyType;
                _invalidateMethod = Helpers.MakeHostInstanceGeneric(weaver._atomInvalidateMethod, propertyType);
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
                // if (atom == null) goto nullCheckEnd;
                il.Insert(ind++, Instruction.Create(OpCodes.Nop));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Brfalse, _nullCheckEndInstruction));

                // atom.Invalidate()
                il.Insert(ind++, Instruction.Create(OpCodes.Ldarg_0));
                il.Insert(ind++, Instruction.Create(OpCodes.Ldfld, _atomField));
                il.Insert(ind++, Instruction.Create(OpCodes.Callvirt, _invalidateMethod));

                il.Insert(ind++, _nullCheckEndInstruction);
            }
        }
    }
}
#endif