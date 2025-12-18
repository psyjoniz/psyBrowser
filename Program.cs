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
        private static Mutex? _singleInstanceMutex;
        internal static BrowserAppContext? AppCtx;
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, @"Local\psyBrowser.SingleInstance", out createdNew);
            if(!createdNew) { return; } //another instance was already running
            if (Cef.IsInitialized == true) // Check explicitly for 'true'
            {
                Debug.WriteLine("CefSharp is already initialized.");
            }
            else
            {
                Debug.WriteLine("CefSharp is not initialized. Initializing now...");
                //var appName = Assembly.GetExecutingAssembly().GetName().Name;
                //var appVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "psyBrowser");

                var cachePath = Path.Combine(basePath, "cef_cache", "Default");
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
            var ctx = new BrowserAppContext(startupUrl);
            Program.AppCtx = ctx;
            Application.Run(ctx);
        }
    }
    internal sealed class BrowserAppContext : ApplicationContext
    {
        private int _openWindows = 0;

        public BrowserAppContext(string? startupUrl = null)
        {
            OpenNewWindow(startupUrl ?? "about:blank");
        }

        public void OpenNewWindow(string url)
        {
            var win = new psyBrowser(url);
            _openWindows++;

            win.FormClosed += (_, __) =>
            {
                _openWindows--;
                if (_openWindows <= 0)
                    ExitThread(); // ends message loop, app exits
            };

            win.Show();
        }
    }
}