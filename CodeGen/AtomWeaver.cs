using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UniMob.Editor.Weaver
{
    public class AtomWeaver
    {
        private const string MemFieldNameFormat = "__atom__{0}";
        private const string PullMethodNameFormat = "__atom__get_{0}";
        private const string PushMethodNameFormat = "__atom__set_{0}";

        private const string ValueName = "Value";
        private const string ConstructorName = ".ctor";
        private const string ComputedName = "Computed";

        private ModuleDefinition _module;

        private TypeDefinition _atomFactoryDef;
        private TypeReference _atomTypeGeneric;
        private TypeDefinition _atomDefGeneric;
        private TypeDefinition _atomPullDef;
        private TypeDefinition _atomPushDef;

        private MethodReference _atomGetValue;
        private MethodReference _atomSetValue;
        private MethodReference _atomPullCtor;
        private MethodReference _atomPushCtor;
        private MethodReference _atomFactoryComputed;

        public bool Weave(AssemblyDefinition asmDef)
        {
            _module = asmDef.MainModule;

            _atomTypeGeneric = _module.ImportReference(typeof(MutableAtom<>));
            _atomDefGeneric = _atomTypeGeneric.Resolve();
            _atomPullDef = _module.ImportReference(typeof(AtomPull<>)).Resolve();
            _atomPushDef = _module.ImportReference(typeof(AtomPush<>)).Resolve();
            _atomFactoryDef = _module.ImportReference(typeof(Atom)).Resolve();

            bool ValuePropertyFilter(PropertyDefinition p) => p.Name == ValueName;
            _atomGetValue = _module.ImportReference(_atomDefGeneric.Properties.Single(ValuePropertyFilter).GetMethod);
            _atomSetValue = _module.ImportReference(_atomDefGeneric.Properties.Single(ValuePropertyFilter).SetMethod);

            bool ConstructorFilter(MethodDefinition m) => m.Name == ConstructorName;
            _atomPullCtor = _module.ImportReference(_atomPullDef.Methods.Single(ConstructorFilter));
            _atomPushCtor = _module.ImportReference(_atomPushDef.Methods.Single(ConstructorFilter));

            bool ComputedMethodFilter(MethodDefinition m) => m.Name == ComputedName && m.Parameters.Count == 6;
            _atomFactoryComputed = _module.ImportReference(_atomFactoryDef.Methods.Single(ComputedMethodFilter));

            bool dirty = false;

            var types = _module.Types;
            for (var typeIndex = 0; typeIndex < types.Count; typeIndex++)
            {
                var type = types[typeIndex];
                var properties = type.Properties;
                for (var propIndex = 0; propIndex < properties.Count; propIndex++)
                {
                    var property = properties[propIndex];
                    dirty |= Weave(property);
                }
            }

            return dirty;
        }

        public bool Weave(PropertyDefinition propDef)
        {
            var customAttr = Helpers.GetCustomAttribute<AtomAttribute>(propDef);
            if (customAttr == null)
                return false;

            //propDef.CustomAttributes.Remove(customAttr);

            var pull = (propDef.GetMethod != null) ? CreatePullMethod(propDef) : null;
            var push = (propDef.SetMethod != null) ? CreatePushMethod(propDef) : null;
            var atom = CreateAtomField(propDef);

            if (pull != null) propDef.DeclaringType.Methods.Add(pull);
            if (push != null) propDef.DeclaringType.Methods.Add(push);
            propDef.DeclaringType.Fields.Add(atom);

            ReplacePropertyCall(propDef, atom, pull, push);

            return true;
        }

        private static MethodDefinition CreatePullMethod(PropertyDefinition propDef)
        {
            var ops = propDef.GetMethod.Body.Instructions;
            if (ops.Count == 3 &&
                ops[0].OpCode == OpCodes.Ldarg_0 &&
                ops[2].OpCode == OpCodes.Ret &&
                ops[1].OpCode == OpCodes.Ldfld &&
                ops[1].Operand is FieldDefinition backingField &&
                backingField.Name.Equals($"<{propDef.Name}>k__BackingField"))
            {
                backingField.Name = $"__atom__{propDef.Name}__BackingField";
            }

            var name = string.Format(PullMethodNameFormat, propDef.Name);
            var method = new MethodDefinition(name, MethodAttributes.Private, propDef.PropertyType)
            {
                Body = propDef.GetMethod.Body,
            };
            return method;
        }

        private static MethodDefinition CreatePushMethod(PropertyDefinition propDef)
        {
            var name = string.Format(PushMethodNameFormat, propDef.Name);
            var method = new MethodDefinition(name, MethodAttributes.Private, propDef.SetMethod.ReturnType)
            {
                Body = propDef.SetMethod.Body,
            };

            var parameter = new ParameterDefinition("value", ParameterAttributes.None, propDef.PropertyType);
            method.Parameters.Add(parameter);

            return method;
        }

        private FieldDefinition CreateAtomField(PropertyDefinition propDef)
        {
            var name = string.Format(MemFieldNameFormat, propDef.Name);
            var type = Helpers.MakeGenericType(_atomTypeGeneric, propDef.PropertyType);
            return new FieldDefinition(name, FieldAttributes.Private, type);
        }

        private void ReplacePropertyCall(PropertyDefinition prop, FieldReference field,
            MethodReference pullMethod, MethodReference pushMethod)
        {
            var type = prop.PropertyType;

            if (prop.GetMethod != null)
            {
                prop.GetMethod.Body = new MethodBody(prop.GetMethod);
                prop.GetMethod.Body.Variables.Add(new VariableDefinition(_atomTypeGeneric));

                var ilProcessor = prop.GetMethod.Body.GetILProcessor();
                var typedAtomGet = Helpers.MakeHostInstanceGeneric(_atomGetValue, type);

                GenerateAtom(ilProcessor, type, field, pullMethod, pushMethod,
                    proc =>
                    {
                        Instruction first;
                        proc.Append(first = proc.Create(OpCodes.Callvirt, typedAtomGet));
                        return first;
                    });
            }

            if (prop.SetMethod != null)
            {
                prop.SetMethod.Body = new MethodBody(prop.SetMethod);
                prop.SetMethod.Body.Variables.Add(new VariableDefinition(_atomTypeGeneric));

                var ilProcessor = prop.SetMethod.Body.GetILProcessor();
                var typedAtomSet = Helpers.MakeHostInstanceGeneric(_atomSetValue, type);

                GenerateAtom(ilProcessor, type, field, pullMethod, pushMethod, proc =>
                {
                    Instruction first;
                    proc.Append(first = proc.Create(OpCodes.Ldarg_1));
                    proc.Append(proc.Create(OpCodes.Callvirt, typedAtomSet));
                    return first;
                });
            }
        }

        private void GenerateAtom(ILProcessor proc, TypeReference type, FieldReference field,
            MethodReference pullMethod, MethodReference pushMethod, Func<ILProcessor, Instruction> generate)
        {
            Instruction br;

            //load class instance onto stack
            proc.Append(proc.Create(OpCodes.Ldarg_0));

            // pop class instance and push field value
            proc.Append(proc.Create(OpCodes.Ldfld, field));

            // duplicate field value
            proc.Append(br = proc.Create(OpCodes.Dup));

            {
                // pop field value and jump if atom not null
                //worker.Append(worker.Create(OpCodes.Brtrue_S, label));  //will be inserted later

                // pop field value (now stack is empty)
                proc.Append(proc.Create(OpCodes.Pop));

                //load class instance
                proc.Append(proc.Create(OpCodes.Ldarg_0));

                //create pull delegate or null
                if (pullMethod != null)
                {
                    proc.Append(proc.Create(OpCodes.Ldarg_0));
                    proc.Append(proc.Create(OpCodes.Ldftn, pullMethod));
                    proc.Append(proc.Create(OpCodes.Newobj, Helpers.MakeHostInstanceGeneric(_atomPullCtor, type)));
                }
                else
                {
                    proc.Append(proc.Create(OpCodes.Ldnull));
                }

                //create push delegate or null
                if (pushMethod != null)
                {
                    proc.Append(proc.Create(OpCodes.Ldarg_0));
                    proc.Append(proc.Create(OpCodes.Ldftn, pushMethod));
                    proc.Append(proc.Create(OpCodes.Newobj, Helpers.MakeHostInstanceGeneric(_atomPushCtor, type)));
                }
                else
                {
                    proc.Append(proc.Create(OpCodes.Ldnull));
                }

                proc.Append(proc.Create(OpCodes.Ldc_I4_0)); //keepAlive = false
                proc.Append(proc.Create(OpCodes.Ldc_I4_0)); //requireReaction = false
                proc.Append(proc.Create(OpCodes.Ldnull)); //callbacks = null
                proc.Append(proc.Create(OpCodes.Ldnull)); //comparer = null

                //invoke Atom.Computed(pull, push, keepAlive, requiredReaction, onActive, onInactive, comparer)
                proc.Append(proc.Create(OpCodes.Call, Helpers.MakeGenericMethod(_atomFactoryComputed, type)));

                // duplicate created Atom
                proc.Append(proc.Create(OpCodes.Dup));

                //pop created atom and save it to local variable
                proc.Append(proc.Create(OpCodes.Stloc_0));

                //pop atom and save it to class field (now stack is empty)
                proc.Append(proc.Create(OpCodes.Stfld, field));

                //load atom onto stack
                proc.Append(proc.Create(OpCodes.Ldloc_0));
            }

            //use atom
            var brTarget = generate(proc);

            proc.InsertAfter(br, proc.Create(OpCodes.Brtrue_S, brTarget));

            // return
            proc.Append(proc.Create(OpCodes.Ret));
        }
    }
}