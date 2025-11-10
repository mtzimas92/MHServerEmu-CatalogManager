using System;
using System.Diagnostics;
using System.IO;

namespace CatalogManager.Services
{
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_crash.log");

        public static void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                Debug.WriteLine(logEntry);
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public static void LogException(string context, Exception ex)
        {
            if (ex == null) return;
            
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {context}:\n" +
                                 $"Exception: {ex.GetType().FullName}\n" +
                                 $"Message: {ex.Message}\n" +
                                 $"Stack Trace: {ex.StackTrace}\n";
                
                if (ex.InnerException != null)
                {
                    logEntry += $"Inner Exception: {ex.InnerException.GetType().FullName}\n" +
                               $"Inner Message: {ex.InnerException.Message}\n" +
                               $"Inner Stack Trace: {ex.InnerException.StackTrace}\n";
                }
                
                Debug.WriteLine(logEntry);
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore errors in logging
            }
        }
    }
}
