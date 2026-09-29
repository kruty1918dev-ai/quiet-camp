using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Kruty1918.Diagnostics
{
    public sealed class AsyncGlobalErrorHandlerService : IDisposable
    {
        public void Initialize()
        {
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        public void Dispose()
        {
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.LogError($"[AsyncErrorHandler] UnobservedTaskException: {e.Exception}");
            e.SetObserved();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.LogError($"[AsyncErrorHandler] UnhandledException: {e.ExceptionObject}");
        }
    }

}
