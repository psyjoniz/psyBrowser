using CefSharp.WinForms;
using CefSharp;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using psyBrowser.InternalPages;

namespace psyBrowser
{
    internal static class Program
    {
        private static Mutex? mutex;
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (Cef.IsInitialized == true) // Check explicitly for 'true'
            {
                Debug.WriteLine("CefSharp is already initialized.");
            }
            else
            {
                Debug.WriteLine("CefSharp is not initialized. Initializing now...");
                //var appName = Assembly.GetExecutingAssembly().GetName().Name;
                //var appVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var basePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "psyBrowser"
);

                // unique per process so multiple instances can run
                var cachePath = Path.Combine(basePath, "cef_cache", $"pid_{Environment.ProcessId}");

                Directory.CreateDirectory(cachePath);

                var settings = new CefSettings
                {
                    LogSeverity = LogSeverity.Verbose,
                    LogFile = Path.Combine(basePath, "cef_log.txt"),
                    CachePath = cachePath,
                };

                settings.RegisterScheme(new CefCustomScheme
                {
                    SchemeName = "psybrowser",
                    IsStandard = true,
                    IsSecure = true,
                    IsLocal = true,
                    SchemeHandlerFactory = new global::psyBrowser.InternalPages.PsyBrowserSchemeHandlerFactory()
                });

                if (!Cef.Initialize(settings))
                {
                    MessageBox.Show("CEF failed to initialize. Check cef_log.txt.", "psyBrowser");
                    return;
                }
            }
            //process-level shutdown
            Application.ApplicationExit += (_, __) =>
            {
                Debug.WriteLine("Application exiting — shutting down CEF");
                Cef.Shutdown();
            };
            ApplicationConfiguration.Initialize();
            var startupUrl = (Environment.GetCommandLineArgs().Length > 1) ? Environment.GetCommandLineArgs()[1] : null;
            Application.Run(new psyBrowser(startupUrl));
        }
    }
}