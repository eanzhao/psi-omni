using System;
using System.Threading.Tasks;

namespace PsiOrleans.Specialized.Services
{
    public interface ICallbackManager
    {
        /// <summary>
        /// Registers a callback handler for a given callId.
        /// </summary>
        Task RegisterCallback(string callId, Func<CallbackResult, Task> handler);

        /// <summary>
        /// Invokes the callback for the given callId with the result.
        /// </summary>
        Task InvokeCallback(string callId, CallbackResult result);
    }

    public class CallbackResult
    {
        public string CallId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
    }
} 