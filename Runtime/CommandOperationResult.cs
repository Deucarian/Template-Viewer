using Newtonsoft.Json.Linq;

namespace Deucarian.TemplateViewer
{
    public readonly struct CommandOperationResult
    {
        private CommandOperationResult(
            bool succeeded,
            string errorCode,
            string message,
            JObject payload)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            Payload = payload ?? new JObject();
        }

        public bool Succeeded { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public JObject Payload { get; }

        public static CommandOperationResult Success(JObject payload) =>
            new CommandOperationResult(true, string.Empty, string.Empty, payload);

        public static CommandOperationResult Failure(string code, string message) =>
            new CommandOperationResult(false, code, message, new JObject());
    }
}
