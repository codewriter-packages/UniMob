using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using ICustomAttributeProvider = Mono.Cecil.ICustomAttributeProvider;

namespace UniMob.Editor.Weaver
{
    internal static class Helpers
    {
        public static CustomAttribute GetCustomAttribute<T>(ICustomAttributeProvider instance)
        {
            if (!instance.HasCustomAttributes)
            {
                return null;
            }

            var attributes = instance.CustomAttributes;

            for (var i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttributeType.FullName.Equals(typeof(T).FullName, StringComparison.Ordinal))
                {
                    return attribute;
                }
            }

            return null;
        }

        public static string GetEngineCoreModuleDirectoryName()
        {
            return Path.GetDirectoryName(UnityEditorInternal.InternalEditorUtility.GetEngineCoreModuleAssemblyPath());
        }

        public static string UnityEngineDllDirectoryName()
        {
            var directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            return directoryName?.Replace(@"file:\", "");
        }

        public static TypeReference MakeGenericType(TypeReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
            {
                throw new ArgumentException();
            }

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
            {
                instance.GenericArguments.Add(argument);
            }

            return instance;
        }

        public static MethodReference MakeGenericMethod(MethodReference self, TypeReference argument)
        {
            if (self.GenericParameters.Count != 1)
            {
                throw new ArgumentException();
            }

            var instance = new GenericInstanceMethod(self);
            instance.GenericArguments.Add(argument);

            return instance;
        }

        public static MethodReference MakeGenericMethod(MethodReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
            {
                throw new ArgumentException();
            }

            var instance = new GenericInstanceMethod(self);
            foreach (var argument in arguments)
            {
                instance.GenericArguments.Add(argument);
            }

            return instance;
        }

        // used to get a specialized method on a generic class, such as SyncList<T>::HandleMsg()
        public static MethodReference MakeHostInstanceGeneric(MethodReference self, params TypeReference[] arguments)
        {
            var reference =
                new MethodReference(self.Name, self.ReturnType, MakeGenericType(self.DeclaringType, arguments))
                {
                    HasThis = self.HasThis,
                    ExplicitThis = self.ExplicitThis,
                    CallingConvention = self.CallingConvention,
                };

            foreach (var parameter in self.Parameters)
            {
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
            }

            foreach (var genericParameter in self.GenericParameters)
            {
                reference.GenericParameters.Add(new GenericParameter(genericParameter.Name, reference));
            }

            return reference;
        }

        public static MethodDefinition FindMethod(this TypeDefinition type, string name, int parametersCount)
        {
            return type.Methods.Single(m => m.Name == name && m.Parameters.Count == parametersCount);
        }

        public static T GetArgumentValueOrDefault<T>(this CustomAttribute attr, string name, T defaultValue)
        {
            foreach (var arg in attr.Properties)
            {
                if (arg.Name == name)
                {
                    return (T) arg.Argument.Value;
                }
            }

            return defaultValue;
        }

        public static PropertyDefinition FindProperty(this TypeDefinition type, string name)
        {
            return type.Properties.Single(p => p.Name == name);
        }

        public static void InsertRange(this Collection<Instruction> collection, int index, Instruction[] instructions)
        {
            foreach (var instruction in instructions)
            {
                collection.Insert(index++, instruction);
            }
        }

        public static string GenerateUniqueFieldName(TypeDefinition type, string name)
        {
            var resultName = name;

            var index = 0;
            while (type.Fields.Any(f => f.Name == name))
            {
                resultName = name + ++index;
            }

            return resultName;
        }

        public static SequencePoint FindBestSequencePointFor(MethodDefinition method, Instruction instruction)
        {
            var sequencePoints = method.DebugInformation?.GetSequencePointMapping().Values.OrderBy(s => s.Offset)
                .ToList();
            if (sequencePoints == null || !sequencePoints.Any())
            {
                return null;
            }

            if (instruction != null)
            {
                for (var i = 0; i != sequencePoints.Count - 1; i++)
                {
                    if (sequencePoints[i].Offset < instruction.Offset &&
                        sequencePoints[i + 1].Offset > instruction.Offset)
                    {
                        return sequencePoints[i];
                    }
                }
            }

            return sequencePoints.FirstOrDefault();
        }

        public static bool IsInterfaceImplemented(this TypeDefinition type, TypeReference interfaceType)
        {
            while (type != null)
            {
                if (type.Interfaces.Any(t => t.InterfaceType.FullName == interfaceType.FullName))
                {
                    return true;
                }

                type = type.BaseType?.Resolve();
            }

            return false;
        }
    }
}