using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PsiOrleans.Specialized.Services
{
    public class CallbackManager : ICallbackManager
    {
        private readonly ConcurrentDictionary<string, Func<CallbackResult, Task>> _handlers = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<CallbackResult>> _pending = new();

        public Task RegisterCallback(string callId, Func<CallbackResult, Task> handler)
        {
            _handlers[callId] = handler;
            return Task.CompletedTask;
        }

        public Task<CallbackResult> WaitForCallback(string callId)
        {
            var tcs = new TaskCompletionSource<CallbackResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[callId] = tcs;
            RegisterCallback(callId, result => {
                tcs.TrySetResult(result);
                return Task.CompletedTask;
            });
            return tcs.Task;
        }

        public async Task InvokeCallback(string callId, CallbackResult result)
        {
            if (_pending.TryRemove(callId, out var tcs))
            {
                tcs.TrySetResult(result);
            }
            if (_handlers.TryRemove(callId, out var handler))
            {
                try
                {
                    await handler(result);
                }
                catch (Exception ex)
                {
                    // 记录异常，允许上层捕获
                    throw new InvalidOperationException($"Callback handler for {callId} failed: {ex.Message}", ex);
                }
            }
            // 幂等性：多次调用不会重复触发
        }
    }
} 