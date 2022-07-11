using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using DType = Unity.CompilationPipeline.Common.Diagnostics.DiagnosticType;

namespace UniMob.Editor.Weaver
{
    internal static class UserError
    {
        public static DiagnosticMessage AtomAttributeCanBeUsedOnlyOnClassMembers(PropertyDefinition property)
            => Make(DType.Error, Code(11), $"Atom attribute can be used only on class members", property);
        
        public static DiagnosticMessage AtomAttributeCanBeUsedOnlyOnLifetimeScope(PropertyDefinition property)
            => Make(DType.Error, Code(12), $"Atom attribute can be used only on class that implements ILifetimeScope interface", property);

        public static DiagnosticMessage AtomAttributeCannotBeUsedOnGenericClasses(PropertyDefinition property)
            => Make(DType.Error, Code(13), $"Atom attribute cannot be used on generic classes", property);

        public static DiagnosticMessage CannotUseAtomAttributeOnSetOnlyProperty(PropertyDefinition property)
            => Make(DType.Error, Code(21), $"Atom attribute cannot be used on set-only property", property);

        public static DiagnosticMessage CannotUseAtomAttributeOnStaticProperty(PropertyDefinition property)
            => Make(DType.Error, Code(22), $"Atom attribute cannot be used on static property", property);

        public static DiagnosticMessage CannotUseAtomAttributeOnAbstractProperty(PropertyDefinition property)
            => Make(DType.Error, Code(23), $"Atom attribute cannot be used on abstract property", property);

        private static string Code(int code)
        {
            return "UniMob" + code.ToString().PadLeft(4, '0');
        }

        private static DiagnosticMessage Make(DiagnosticType type, string errorCode, string messageData,
            PropertyDefinition property)
        {
            return Make(type, errorCode, messageData, property?.GetMethod ?? property?.SetMethod, null);
        }

        private static DiagnosticMessage Make(DiagnosticType type, string errorCode, string messageData,
            MethodDefinition method, Instruction instruction)
        {
            var seq = method != null ? Helpers.FindBestSequencePointFor(method, instruction) : null;
            return Make(type, errorCode, messageData, seq);
        }

        private static DiagnosticMessage Make(DiagnosticType type, string errorCode, string messageData,
            SequencePoint seq)
        {
            var result = new DiagnosticMessage {Column = 0, Line = 0, DiagnosticType = type, File = ""};

            messageData = $"error {errorCode}: {messageData}";
            if (seq != null)
            {
                result.File = seq.Document.Url;
                result.Column = seq.StartColumn;
                result.Line = seq.StartLine;
#if UNITY_EDITOR
                result.MessageData = $"{seq.Document.Url}({seq.StartLine},{seq.StartColumn}): {messageData}";
#else
                result.MessageData = messageData;
#endif
            }
            else
            {
                result.MessageData = messageData;
            }

            return result;
        }
    }
}