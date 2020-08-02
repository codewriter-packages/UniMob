#if UNITY_EDITOR && MONO_CECIL && UNIMOB_CODEGEN_ENABLED

using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using ICustomAttributeProvider = Mono.Cecil.ICustomAttributeProvider;

namespace UniMob.Editor.Weaver
{
    internal static class Helpers
    {
        public static CustomAttribute GetCustomAttribute<T>(ICustomAttributeProvider instance)
        {
            if (!instance.HasCustomAttributes) return null;

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
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            return directoryName?.Replace(@"file:\", "");
        }

        public static TypeReference MakeGenericType(TypeReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceType(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

            return instance;
        }

        public static MethodReference MakeGenericMethod(MethodReference self, params TypeReference[] arguments)
        {
            if (self.GenericParameters.Count != arguments.Length)
                throw new ArgumentException();

            var instance = new GenericInstanceMethod(self);
            foreach (var argument in arguments)
                instance.GenericArguments.Add(argument);

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
                    CallingConvention = self.CallingConvention
                };

            foreach (var parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (var genericParameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(genericParameter.Name, reference));

            return reference;
        }
    }
}
#endif