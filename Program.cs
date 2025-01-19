using CefSharp.WinForms;
using CefSharp;
using System.Diagnostics;
using System.Reflection;

namespace psyBrowser
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            bool createdNew;
            Mutex mutex = new Mutex(true, "psyBrowser", out createdNew);

            if (!createdNew)
            {
                // Mutex exists, meaning another instance is already running.
                // Launch a new instance explicitly:
                Process.Start(Assembly.GetExecutingAssembly().Location);
                return;
            }

            if (Cef.IsInitialized == true) // Check explicitly for 'true'
            {
                Debug.WriteLine("CefSharp is already initialized.");
            }
            else
            {
                Debug.WriteLine("CefSharp is not initialized. Initializing now...");
                var settings = new CefSettings
                {
                    UserAgent = "psyBrowser/1.0 (Windows NT; Custom Browser)",
                    LogSeverity = LogSeverity.Verbose,
                    LogFile = "cef_log.txt"
                };
                Cef.Initialize(settings);
            }
            ApplicationConfiguration.Initialize();
            Application.Run(new psyBrowser());
        }
    }
}