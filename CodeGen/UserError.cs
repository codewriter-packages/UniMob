using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace UniMob.Editor.Weaver
{
    internal static class UserError
    {
        private static DiagnosticMessage MakeInternal(DiagnosticType type, string errorCode, string messageData,
            MethodDefinition method, Instruction instruction)
        {
            var result = new DiagnosticMessage {Column = 0, Line = 0, DiagnosticType = type, File = ""};

            var seq = method != null ? Helpers.FindBestSequencePointFor(method, instruction) : null;

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

        public static DiagnosticMessage MakeError(string errorCode, string messageData, MethodDefinition method,
            Instruction instruction)
        {
            return MakeInternal(DiagnosticType.Error, errorCode, messageData, method, instruction);
        }

        public static DiagnosticMessage MakeWarning(string errorCode, string messageData, MethodDefinition method,
            Instruction instruction)
        {
            return MakeInternal(DiagnosticType.Warning, errorCode, messageData, method, instruction);
        }
    }
}