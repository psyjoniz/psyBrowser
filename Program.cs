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
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            bool createdNew;
            mutex = new Mutex(true, "psyBrowser", out createdNew);

            if (!createdNew)
            {
                // Another instance already owns the mutex
                return;
            }

            if (Cef.IsInitialized == true) // Check explicitly for 'true'
            {
                Debug.WriteLine("CefSharp is already initialized.");
            }
            else
            {
                Debug.WriteLine("CefSharp is not initialized. Initializing now...");
                //var appName = Assembly.GetExecutingAssembly().GetName().Name;
                //var appVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var settings = new CefSettings
                {
                    LogSeverity = LogSeverity.Verbose,
                    LogFile = "cef_log.txt",
                    // Put all Chromium profile data somewhere explicit (and lock down preference persistence)
                    CachePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "psyBrowser",
                        "cef_cache"
                    ),
                };
                //register "psybrowser://" as first class URL telling CEF it can be resolved (locally)
                settings.RegisterScheme(new CefCustomScheme
                {
                    SchemeName = "psybrowser",
                    IsStandard = true,
                    IsSecure = true,
                    IsLocal = true,
                    SchemeHandlerFactory = new global::psyBrowser.InternalPages.PsyBrowserSchemeHandlerFactory()
                });
                Cef.Initialize(settings);
            }
            //process-level shutdown
            Application.ApplicationExit += (_, __) =>
            {
                Debug.WriteLine("Application exiting — shutting down CEF");
                Cef.Shutdown();
            };
            ApplicationConfiguration.Initialize();
            Application.Run(new psyBrowser());
        }
    }
}