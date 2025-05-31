using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MHServerEmu.Games.GameData;
using System.Windows.Threading;

namespace CatalogManager
{
    public partial class App : Application
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_crash.log");

        [STAThread]
        public static void Main()
        {
            try
            {
                LogMessage("Application starting...");
                LogEnvironmentInfo();

                string dataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Game");
                LogMessage($"Current directory: {Directory.GetCurrentDirectory()}");
                LogMessage($"Looking for .sip files in: {dataPath}");
                LogMessage($"Game directory exists: {Directory.Exists(dataPath)}");

                if (Directory.Exists(dataPath))
                {
                    var files = Directory.GetFiles(dataPath, "*.sip");
                    LogMessage($"Found .sip files: {string.Join(", ", files)}");
                }
                else
                {
                    LogMessage("WARNING: Game directory does not exist!");
                }

                LogMessage("Initializing pak file system...");
                if (!PakFileSystem.Instance.Initialize())
                {
                    LogMessage("ERROR: Failed to initialize pak file system");
                    MessageBox.Show("Failed to initialize pak file system", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                LogMessage("Pak file system initialized successfully");

                // Check for Data directory and catalog files
                string catalogPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Catalog.json");
                string patchPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "CatalogPatch.json");
                
                LogMessage($"Checking for catalog files:");
                LogMessage($"Catalog.json exists: {File.Exists(catalogPath)}");
                LogMessage($"CatalogPatch.json exists: {File.Exists(patchPath)}");

                // Create application and set up exception handlers
                var application = new App();
                
                // Set up global exception handling
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                application.DispatcherUnhandledException += App_DispatcherUnhandledException;
                
                LogMessage("Initializing application components...");
                application.InitializeComponent();
                LogMessage("Application components initialized");
                
                LogMessage("Starting application main loop");
                application.Run();
                LogMessage("Application exited normally");
            }
            catch (Exception ex)
            {
                LogException("Fatal error during application startup", ex);
                MessageBox.Show($"A fatal error occurred during startup: {ex.Message}\n\nSee log file for details: {LogFilePath}", 
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("Unhandled UI exception", e.Exception);
            MessageBox.Show($"An unhandled exception occurred: {e.Exception.Message}\n\nSee log file for details: {LogFilePath}", 
                "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            e.Handled = true; // Prevent the application from crashing
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("Unhandled application exception", e.ExceptionObject as Exception);
            MessageBox.Show($"A fatal error occurred. The application will now close.\n\nSee log file for details: {LogFilePath}", 
                "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void LogMessage(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                Debug.WriteLine(logEntry);
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore errors in logging
            }
        }

        private static void LogException(string context, Exception ex)
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

        private static void LogEnvironmentInfo()
        {
            try
            {
                LogMessage($"OS Version: {Environment.OSVersion}");
                LogMessage($".NET Version: {Environment.Version}");
                LogMessage($"64-bit OS: {Environment.Is64BitOperatingSystem}");
                LogMessage($"64-bit Process: {Environment.Is64BitProcess}");
                LogMessage($"Processor Count: {Environment.ProcessorCount}");
                LogMessage($"Working Set: {Environment.WorkingSet / (1024 * 1024)} MB");
            }
            catch
            {
                // Ignore errors in logging environment info
            }
        }
    }
}
