using CefSharp.WinForms;
using CefSharp;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Drawing;
using psyBrowser.InternalPages;

namespace psyBrowser
{
    internal static class Program
    {
        private static Mutex? _singleInstanceMutex;
        internal static BrowserAppContext? AppCtx;
        private const string MutexName = @"Local\psyBrowser.SingleInstance";
        internal const string OpenWindowEventName = @"Local\psyBrowser.OpenWindow";
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                // Another instance is running (likely tray-only). Ask it to open a window.
                try
                {
                    using var ev = EventWaitHandle.OpenExisting(OpenWindowEventName);
                    ev.Set();
                }
                catch
                {
                    // If the first instance hasn't created the event yet, just exit quietly.
                }
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
        private readonly List<Form> _windows = new();
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _trayMenu;
        private readonly EventWaitHandle _openWindowEvent;
        private readonly Thread _openWindowListener;
        private readonly SynchronizationContext _uiCtx;

        public BrowserAppContext(string? startupUrl = null)
        {
            // Tray menu
            _trayMenu = new ContextMenuStrip();

            var miNewWindow = new ToolStripMenuItem("New Window");
            miNewWindow.Click += (_, __) => OpenNewWindow(null);

            var miExit = new ToolStripMenuItem("Exit");
            miExit.Click += (_, __) => ExitAll();

            _trayMenu.Items.Add(miNewWindow);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(miExit);

            // Tray icon (use the app icon)
            var appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

            _tray = new NotifyIcon
            {
                Icon = appIcon,
                Text = "psyBrowser",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };

            //bring all windows to front or start a new window with double-click of system tray
            _tray.MouseDoubleClick += (_, e) =>
            {
                if (e.Button != MouseButtons.Left)
                    return;

                if (_windows.Count == 0)
                {
                    OpenNewWindow(null);
                    return;
                }

                foreach (var w in _windows.ToArray())
                {
                    if (w.WindowState == FormWindowState.Minimized)
                        w.WindowState = FormWindowState.Normal;

                    w.Show();
                    w.BringToFront();
                    w.Activate();
                }
            };

            OpenNewWindow(startupUrl);

            _uiCtx = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            // Create the named event that 2nd launches will signal
            _openWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.OpenWindowEventName);

            // Background listener: when signaled, open a new window on the UI thread
            _openWindowListener = new Thread(() =>
            {
                while (true)
                {
                    _openWindowEvent.WaitOne();
                    _uiCtx.Post(_ => OpenNewWindow(null), null);
                }
            })
            {
                IsBackground = true
            };

            _openWindowListener.Start();

        }
        public void OpenNewWindow(string? url = null)
        {
            // Rule:
            // - first window: load vault.LastLocation (unless an explicit url was supplied)
            // - not first: about:blank unless an explicit url was supplied (ctrl+click/target/etc.)
            string? effectiveUrl;
            if (_windows.Count == 0)
                effectiveUrl = string.IsNullOrWhiteSpace(url) ? null : url;
            else
                effectiveUrl = string.IsNullOrWhiteSpace(url) ? "about:blank" : url;

            var win = new psyBrowser(effectiveUrl);
            _windows.Add(win);

            _openWindows++;

            win.FormClosed += (_, __) =>
            {
                _windows.Remove(win);
                _openWindows--;
            };

            win.Show();
        }
        private void ExitAll()
        {
            // Hide tray first so it doesn't linger
            _tray.Visible = false;

            // Close all windows (triggers FormClosed handlers)
            foreach (var w in _windows.ToArray())
            {
                try { w.Close(); } catch { /* ignore */ }
            }

            // Ensure the message loop ends even if something prevents Close()
            ExitThread();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _tray.Visible = false; } catch { }
                _tray?.Dispose();
                _trayMenu?.Dispose();
                _openWindowEvent?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}