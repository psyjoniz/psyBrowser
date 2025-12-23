using CefSharp;
using CefSharp.WinForms;
using psyBrowser.InternalPages;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using psyBrowser.InternalPages;
using CefSharp.Handler;
using System.Windows.Forms;

namespace psyBrowser
{
    public partial class psyBrowser : Form
    {
        private string version = "0.1";
        private static readonly Mutex VaultMutex = new(false, @"Local\psyBrowserVault");
        private readonly string vaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "psyBrowser",
            "vault.bin"
        );
        private static readonly JsonSerializerOptions VaultJsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private ChromiumWebBrowser browser;
        private double currentZoomLevel = 0;
        private const int CascadeOffset = 30;
        /* history suggestion */
        private readonly System.Windows.Forms.Timer _historyDebounceTimer = new();
        private readonly ListBox _historySuggestList = new();
        private const int HistoryDebounceMs = 250;
        private const int MaxSuggestions = 20;
        /* /history suggestion */
        /*
         * don't forget to apply keyboard listeneners in cef layer too (this is the window layer)
         */
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            //Debug.WriteLine($"Key pressed: {keyData}");
            //chromium dev tools
            if (keyData == (Keys.Control | Keys.Shift | Keys.I))
            {
                browser?.ShowDevTools();
                return true;
            }
            // Zoom in: Ctrl+= (also covers Ctrl+Shift+= on most keyboards)
            if (keyData == (Keys.Control | Keys.Oemplus) ||
                keyData == (Keys.Control | Keys.Shift | Keys.Oemplus) ||
                keyData == (Keys.Control | Keys.Add))
            {
                ApplyZoomStep(+1);
                return true;
            }
            // Zoom out: Ctrl+-
            if (keyData == (Keys.Control | Keys.OemMinus) ||
                keyData == (Keys.Control | Keys.Subtract))
            {
                ApplyZoomStep(-1);
                return true;
            }
            // Reset zoom: Ctrl+0
            if (keyData == (Keys.Control | Keys.D0))
            {
                ResetZoom();
                return true;
            }
            if (keyData == (Keys.Control | Keys.N))
            {
                Program.AppCtx?.OpenNewWindow("about:blank");
                return true;
            }
            if (keyData == (Keys.Control | Keys.R))
            {
                browser?.Reload();       // normal reload
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.R))
            {
                browser?.Reload(true);   // hard reload
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        internal void Navigate(string input)
        {
            HideSuggestList(); // hide suggestions on any navigation

            //System.Windows.Forms.MessageBox.Show("Navigate() hit from: " + System.Reflection.Assembly.GetExecutingAssembly().Location);

            input = (input ?? "").Trim();
            if (input.Length == 0)
                input = "about:blank";

            // Internal pages first
            if (InternalPageRouter.TryHandle(browser, input))
            {
                textBoxURL.Text = input;
                PersistNavigation(input);
                return;
            }

            if (IsUrl(input))
            {
                string url = NormalizeToUrl(input);
                textBoxURL.Text = url;
                browser.Load(url);
                PersistNavigation(url);
                return;
            }

            string searchUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
            textBoxURL.Text = searchUrl;
            browser.Load(searchUrl);
        }
        internal static void OpenNewWindow(string? url = null, psyBrowser? source = null)
        {
            source ??= Form.ActiveForm as psyBrowser;

            var win = new psyBrowser();

            if (source != null && source.WindowState == FormWindowState.Maximized)
            {
                win.WindowState = FormWindowState.Maximized;
            }
            else if (source != null)
            {
                win.StartPosition = FormStartPosition.Manual;

                win.Size = source.Size;

                var newLoc = new Point(source.Left + CascadeOffset, source.Top + CascadeOffset);
                var newBounds = new Rectangle(newLoc, win.Size);

                if (!IsFullyOnAnyWorkingArea(newBounds))
                    newLoc = new Point(0, 0);

                win.Location = newLoc;
            }

            win.Show();

            if (!string.IsNullOrWhiteSpace(url))
                win.Navigate(url);
        }
        internal static void OpenNewProcess(string? url = null)
        {
            url ??= "about:blank";

            var exe = Environment.ProcessPath
                      ?? Assembly.GetExecutingAssembly().Location;

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true
            };
            psi.ArgumentList.Add(url);

            Process.Start(psi);
        }
        private static bool IsFullyOnAnyWorkingArea(Rectangle bounds)
        {
            foreach (var screen in Screen.AllScreens)
                if (screen.WorkingArea.Contains(bounds))
                    return true;

            return false;
        }
        public psyBrowser(string? startupUrl = null)
        {
            InitializeComponent();

            this.MouseDown += (_, __) => HideSuggestList();

            string originalTitle = this.Text;

            var vault = LoadVault(vaultPath);
            ApplyWindowPlacement(vault);
            var url = string.IsNullOrWhiteSpace(startupUrl) ? (vault?.LastLocation ?? "about:blank") : startupUrl.Trim();
            currentZoomLevel = vault?.CurrentZoomLevel ?? 0;
            //url = "https://browserdetect.psy-core.com";

            browser = new ChromiumWebBrowser(url);
            browser.MouseDown += (_, __) => HideSuggestList();
            browser.LifeSpanHandler = new CustomLifeSpanHandler();
            browser.RequestHandler = new CustomRequestHandler(this);
            browser.MenuHandler = new CustomMenuHandler(isUiAlive: () => !IsDisposed && !Disposing && IsHandleCreated, openInNewWindowOnUi: (targetUrl) =>
                    {
                        if (IsDisposed || Disposing) return;

                        BeginInvoke(new Action(() =>
                            {
                                if (IsDisposed || Disposing) return;

                                OpenNewWindow(targetUrl);
                            }));
                    }
                );
            browser.DownloadHandler = new CustomDownloadHandler(this);
            browser.AddressChanged += Browser_AddressChanged;

            //respect html titles
            browser.TitleChanged += (_, e) =>
            {
                if (IsDisposed || !IsHandleCreated) return;

                BeginInvoke(new Action(() =>
                {
                    // keep it simple; you can prepend an app name later if you want
                    this.Text = e.Title + " | " + originalTitle + " v" + version ?? originalTitle + " v" + version;
                }));
            };

            textBoxURL.Text = url;

            /* handle select-all behavior for user input to url */
            textBoxURL.GotFocus += (_, __) => textBoxURL.SelectAll();
            textBoxURL.MouseUp += (_, __) => { if (textBoxURL.SelectionLength == 0) textBoxURL.SelectAll(); };

            panelRenderer.BackColor = Color.White;
            this.BackColor = Color.White;
            browser.BackColor = Color.White;

            panelRenderer.Controls.Add(browser);

            textBoxURL.KeyDown += TextBoxURL_KeyDown;

            /* history suggestion */
            // debounce timer
            _historyDebounceTimer.Interval = HistoryDebounceMs;
            _historyDebounceTimer.Tick += HistoryDebounceTimer_Tick;
            // suggestion list UI
            _historySuggestList.Visible = false;
            _historySuggestList.IntegralHeight = false;
            _historySuggestList.TabStop = false;
            _historySuggestList.BorderStyle = BorderStyle.FixedSingle;
            _historySuggestList.Click += HistorySuggestList_Click;
            _historySuggestList.DoubleClick += HistorySuggestList_Click;
            _historySuggestList.KeyDown += HistorySuggestList_KeyDown;
            // place it on the form (same parent as textBoxURL)
            this.Controls.Add(_historySuggestList);
            // typing events
            textBoxURL.TextChanged += TextBoxURL_TextChanged;
            textBoxURL.KeyDown += TextBoxURL_Autocomplete_KeyDown;
            textBoxURL.LostFocus += TextBoxURL_LostFocus;
            textBoxURL.MouseDown += (_, __) => HideSuggestList();
            // keep it positioned on resize/layout
            this.Resize += (_, __) => PositionSuggestList();
            this.Shown += (_, __) => PositionSuggestList();
            /* /history suggestion */

            browser.KeyboardHandler = new CustomKeyboardHandler(this);

            browser.IsBrowserInitializedChanged += (_, __) =>
            {
                if (!browser.IsBrowserInitialized) return;

                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        browser.SetZoomLevel(currentZoomLevel);
                    }));
                }
                catch { }
            };

            this.KeyPreview = true;

            /* @TODO: this needs to be implemented but doesn't seem to be the right place for it (its persistant; once first page doesn't load nothing else will render)
            browser.LoadingStateChanged += (sender, args) =>
            {
                if (!args.IsLoading && !browser.CanGoBack)
                {
                    browser.LoadHtml("<html><body><h1>Error: Page could not be loaded</h1></body></html>");
                }
            };
            */

            /* page loading indicator (currently a progress bar) */
            progressBarPageLoading.Style = ProgressBarStyle.Marquee; // Set indeterminate style
            progressBarPageLoading.Visible = false;
            browser.LoadingStateChanged += Browser_LoadingStateChanged;

        }
        private void Browser_AddressChanged(object? sender, AddressChangedEventArgs e)
        {
            // Always persist last location (covers clicks, redirects, back/forward, etc.)
            PersistNavigation(e.Address);

            // Safe UI update
            if (IsDisposed || !IsHandleCreated) return;
            if (textBoxURL.IsDisposed || !textBoxURL.IsHandleCreated) return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || textBoxURL.IsDisposed) return;
                    textBoxURL.Text = e.Address;
                }));
            }
            catch (ObjectDisposedException) { }
        }
        private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed)
                        return;
                    HideSuggestList();
                    progressBarPageLoading.Visible = e.IsLoading;
                }));
            }
            catch
            {
                // best-effort during shutdown
            }
        }
        private void TextBoxURL_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Navigate(textBoxURL.Text);

                PersistNavigation(textBoxURL.Text);

                e.SuppressKeyPress = true;
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Capture the final URL for next launch
            try
            {
                var finalUrl = browser?.Address;
                if (string.IsNullOrWhiteSpace(finalUrl))
                    finalUrl = textBoxURL?.Text;

                PersistNavigation(finalUrl);
            }
            catch { }

            try
            {
                browser.LoadingStateChanged -= Browser_LoadingStateChanged;
                try
                {
                    browser.AddressChanged -= Browser_AddressChanged;
                }
                catch { }
                browser.Dispose();
            }
            catch { }

            PersistWindowPlacement();

            base.OnFormClosing(e);
        }
        private bool IsUrl(string input)
        {
            input = (input ?? "").Trim();
            if (input.Length == 0) return false;
            if (input.Contains(' ')) return false;

            // already absolute with scheme
            if (Uri.TryCreate(input, UriKind.Absolute, out var abs) && !string.IsNullOrEmpty(abs.Scheme))
                return true;

            // domain or IP-like: must contain a dot (keeps "cypher" as NOT a URL)
            // This is your original intent, just without exception-based Uri parsing.
            if (!input.Contains('.'))
                return false;

            // treat as URL if it becomes valid when assuming https
            return Uri.TryCreate("https://" + input, UriKind.Absolute, out _);
        }
        private string NormalizeToUrl(string input)
        {
            input = (input ?? "").Trim();

            // if it already has a scheme, keep it
            if (Uri.TryCreate(input, UriKind.Absolute, out var abs) && !string.IsNullOrEmpty(abs.Scheme))
                return input;

            // otherwise assume https
            return "https://" + input;
        }
        private class CustomKeyboardHandler : IKeyboardHandler
        {
            private readonly psyBrowser form;

            public CustomKeyboardHandler(psyBrowser form)
            {
                this.form = form;
            }
            /*
             * dont forget to apply keyboard listeners in window layer too (this is cef layer)
             */
            public bool OnKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey)
            {
                //Debug.WriteLine($"OnKeyEvent()");
                // Forward key press events to the form
                if (type == KeyType.RawKeyDown && (modifiers & CefEventFlags.ControlDown) != 0)
                {
                    //Debug.WriteLine($"Ctrl pressed");
                    //chromium devtools
                    if ((modifiers & CefEventFlags.ShiftDown) != 0 && windowsKeyCode == (int)Keys.I)
                    {
                        form.BeginInvoke(new Action(() => form.browser?.ShowDevTools()));
                        return true;
                    }
                    if ((modifiers & CefEventFlags.ShiftDown) == 0 && windowsKeyCode == (int)Keys.R) // Ctrl + R
                    {
                        chromiumWebBrowser.Reload(); //normal reload
                        return true;
                    }
                    if ((modifiers & CefEventFlags.ShiftDown) != 0 && windowsKeyCode == (int)Keys.R) // Ctrl + Shift + R
                    {
                        chromiumWebBrowser.Reload(true); //hard reload
                        return true;
                    }
                    if ((modifiers & CefEventFlags.ShiftDown) == 0 && windowsKeyCode == (int)Keys.N)
                    {
                        form.BeginInvoke(new Action(() => Program.AppCtx?.OpenNewWindow("about:blank")));
                        return true;
                    }
                }
                return false; // Let other events propagate
            }
            public bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
            {
                if (type != KeyType.RawKeyDown)
                    return false;

                if ((modifiers & CefEventFlags.ControlDown) == 0)
                    return false;

                // We own zoom shortcuts; prevent Chromium default zoom too.
                if (windowsKeyCode == (int)Keys.Oemplus || windowsKeyCode == (int)Keys.Add)
                {
                    isKeyboardShortcut = true;
                    form.ApplyZoomStep(+1);
                    return true;
                }

                if (windowsKeyCode == (int)Keys.OemMinus || windowsKeyCode == (int)Keys.Subtract)
                {
                    isKeyboardShortcut = true;
                    form.ApplyZoomStep(-1);
                    return true;
                }

                if (windowsKeyCode == (int)Keys.D0)
                {
                    isKeyboardShortcut = true;
                    form.ResetZoom();
                    return true;
                }

                return false;
            }

        }
        private class CustomLifeSpanHandler : ILifeSpanHandler
        {
            public bool OnBeforePopup(
                IWebBrowser chromiumWebBrowser,
                IBrowser browser,
                IFrame frame,
                string targetUrl,
                string targetFrameName,
                WindowOpenDisposition targetDisposition,
                bool userGesture,
                IPopupFeatures popupFeatures,
                IWindowInfo windowInfo,
                IBrowserSettings browserSettings,
                ref bool noJavascriptAccess,
                out IWebBrowser newBrowser)
            {
                newBrowser = null;

                if (!string.IsNullOrWhiteSpace(targetUrl))
                {
                    Program.AppCtx?.OpenNewWindow(targetUrl);
                }

                return true; // cancel Chromium popup
            }
            public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
            }
            public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                return false;
            }
            public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
            }
        }
        private void PersistZoomLevel(double level)
        {
            var vault = LoadVault(vaultPath);
            if (vault.CurrentZoomLevel != level)
            {
                vault.CurrentZoomLevel = level;
                SaveVault(vaultPath, vault);
            }
        }
        private void ApplyZoomStep(int delta)
        {
            currentZoomLevel += delta;      // delta = +1 / -1
            browser.SetZoomLevel(currentZoomLevel);
            //Debug.WriteLine($"Zoom -> {currentZoomLevel}");
            PersistZoomLevel(currentZoomLevel);
        }
        private void ResetZoom()
        {
            currentZoomLevel = 0;
            browser.SetZoomLevel(0);
            //Debug.WriteLine("Zoom -> 0");
            PersistZoomLevel(0);
        }

        public static void SaveVault(string filePath, LocalVault vault)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(vault, VaultJsonOptions);

            byte[] encrypted = ProtectedData.Protect(
                jsonBytes,
                null, // optional entropy (keep null unless you intentionally manage it)
                DataProtectionScope.CurrentUser
            );

            VaultMutex.WaitOne();
            // atomic-ish write
            string tmp = filePath + ".tmp";
            try
            {
                File.WriteAllBytes(tmp, encrypted);
                File.Copy(tmp, filePath, true);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
                VaultMutex.ReleaseMutex();
            }
        }
        public static LocalVault LoadVault(string filePath)
        {
            VaultMutex.WaitOne();
            try
            {
                if (!File.Exists(filePath))
                    return new LocalVault();

                byte[] encrypted = File.ReadAllBytes(filePath);

                byte[] jsonBytes = ProtectedData.Unprotect(
                    encrypted,
                    null,
                    DataProtectionScope.CurrentUser
                );

                return JsonSerializer.Deserialize<LocalVault>(jsonBytes, VaultJsonOptions)
                       ?? new LocalVault();
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"Vault load failed, resetting: {ex.Message}");
                return new LocalVault();
            }
            finally
            {
                VaultMutex.ReleaseMutex();
            }
        }
        private const int MaxHistory = 500;

        private void PersistNavigation(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            url = url.Trim();

            // Optional: skip noise
            if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
                return;

            var vault = LoadVault(vaultPath);

            bool changed = false;

            if (!string.Equals(vault.LastLocation, url, StringComparison.OrdinalIgnoreCase))
            {
                vault.LastLocation = url;
                changed = true;
            }

            vault.History ??= new List<string>();

            // de-dupe consecutive + avoid duplicates elsewhere
            if (vault.History.Count == 0 ||
                !string.Equals(vault.History[^1], url, StringComparison.OrdinalIgnoreCase))
            {
                vault.History.RemoveAll(x => string.Equals(x, url, StringComparison.OrdinalIgnoreCase));
                vault.History.Add(url);
                changed = true;
            }

            if (vault.History.Count > MaxHistory)
            {
                int overflow = vault.History.Count - MaxHistory;
                vault.History.RemoveRange(0, overflow);
                changed = true;
            }

            if (changed)
                SaveVault(vaultPath, vault);
        }
        private sealed class CustomRequestHandler : IRequestHandler
        {
            private readonly psyBrowser form;
            public CustomRequestHandler(psyBrowser form) => this.form = form;

            public bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
            {
                if (frame.IsMain && userGesture && request != null && !string.IsNullOrWhiteSpace(request.Url))
                {
                    form.PersistNavigation(request.Url);
                }
                return false;
            }
            public bool OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
            {
                if (string.IsNullOrWhiteSpace(targetUrl))
                    return false;

                // If Chromium is asking for a new window/tab, we treat it as "open new psyBrowser window"
                if (targetDisposition == WindowOpenDisposition.NewForegroundTab ||
                    targetDisposition == WindowOpenDisposition.NewBackgroundTab ||
                    targetDisposition == WindowOpenDisposition.NewWindow ||
                    targetDisposition == WindowOpenDisposition.NewPopup)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        Program.AppCtx?.OpenNewWindow(targetUrl);
                    }));

                    return true; // cancel default handling
                }

                return false;
            }
            // no-op implementations
            public void OnRenderViewReady(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
            public bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback) { return false; }
            public bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback) => false;
            public void OnPluginCrashed(IWebBrowser chromiumWebBrowser, IBrowser browser, string pluginPath) { }
            public void OnDocumentAvailableInMainFrame(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
            public void OnResourceRedirect(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl) { }
            public bool OnQuotaRequest(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, long newSize, IRequestCallback callback) => false;
            public void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser, CefTerminationStatus status, int errorCode, string errorString) { }
            public void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls) { }
            public bool OnSelectClientCertificate(IWebBrowser chromiumWebBrowser, IBrowser browser, bool isProxy, string host, int port, X509Certificate2Collection certificates, ISelectClientCertificateCallback callback) => false;
            public bool OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback) => false;
            public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling) => null;
        }
        internal sealed class CustomMenuHandler : ContextMenuHandler
        {
            private const CefMenuCommand OpenLinkInNewWindowCmd = (CefMenuCommand)26501;

            private readonly Func<bool> _isUiAlive;
            private readonly Action<string> _openInNewWindowOnUi;

            public CustomMenuHandler(Func<bool> isUiAlive, Action<string> openInNewWindowOnUi)
            {
                _isUiAlive = isUiAlive ?? throw new ArgumentNullException(nameof(isUiAlive));
                _openInNewWindowOnUi = openInNewWindowOnUi ?? throw new ArgumentNullException(nameof(openInNewWindowOnUi));
            }

            protected override void OnBeforeContextMenu(
                IWebBrowser chromiumWebBrowser,
                IBrowser browser,
                IFrame frame,
                IContextMenuParams parameters,
                IMenuModel model)
            {
                var linkUrl = parameters?.LinkUrl;
                if (!string.IsNullOrWhiteSpace(linkUrl))
                {
                    model.AddSeparator();
                    model.AddItem(OpenLinkInNewWindowCmd, "Open link in new window");
                }

                base.OnBeforeContextMenu(chromiumWebBrowser, browser, frame, parameters, model);
            }
            protected override bool OnContextMenuCommand(
                IWebBrowser chromiumWebBrowser,
                IBrowser browser,
                IFrame frame,
                IContextMenuParams parameters,
                CefMenuCommand commandId,
                CefEventFlags eventFlags)
            {
                if (commandId == OpenLinkInNewWindowCmd)
                {
                    var url = parameters?.LinkUrl;
                    if (!string.IsNullOrWhiteSpace(url) && _isUiAlive())
                    {
                        _openInNewWindowOnUi(url);
                    }
                    return true;
                }

                return base.OnContextMenuCommand(
                    chromiumWebBrowser,
                    browser,
                    frame,
                    parameters,
                    commandId,
                    eventFlags);
            }
        }
        internal sealed class CustomDownloadHandler : IDownloadHandler
        {
            private readonly psyBrowser _form;
            public CustomDownloadHandler(psyBrowser form) => _form = form;
            public bool CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod) => true;
            private readonly HashSet<int> _notified = new();
            public bool OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem item, IBeforeDownloadCallback callback)
            {
                if (callback == null || callback.IsDisposed) return false;

                // sanitize name
                var name = string.IsNullOrWhiteSpace(item.SuggestedFileName) ? "download" : Path.GetFileName(item.SuggestedFileName);

                var downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(downloadsDir);

                var fullPath = MakeUnique(Path.Combine(downloadsDir, name));

                Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    using (callback)
                    {
                        // NO DIALOG. This is the key.
                        callback.Continue(fullPath, showDialog: false);
                    }
                });

                return true;
            }

            public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem item, IDownloadItemCallback callback)
            {
                if (!item.IsComplete) return;

                // avoid double-popups if CefSharp fires multiple updates at completion
                if (_notified.Contains(item.Id)) return;
                _notified.Add(item.Id);

                var path = item.FullPath;

                _form.BeginInvoke(new Action(() =>
                {
                    if (_form.IsDisposed || _form.Disposing) return;

                    MessageBox.Show(
                        _form,
                        $"Downloaded:\n\n{path}",
                        "Download complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }));
            }
            private static string MakeUnique(string path)
            {
                if (!File.Exists(path)) return path;

                var dir = Path.GetDirectoryName(path)!;
                var name = Path.GetFileNameWithoutExtension(path);
                var ext = Path.GetExtension(path);

                for (int i = 1; i < 10000; i++)
                {
                    var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                    if (!File.Exists(candidate)) return candidate;
                }
                return path; // last resort
            }
        }

        private void ApplyWindowPlacement(LocalVault vault)
        {
            // default behavior if we have nothing saved yet
            if (vault.WinX is null || vault.WinY is null || vault.WinW is null || vault.WinH is null)
                return;

            // reject nonsense sizes
            if (vault.WinW < 200 || vault.WinH < 200)
                return;

            var target = new Rectangle(vault.WinX.Value, vault.WinY.Value, vault.WinW.Value, vault.WinH.Value);

            // ensure it lands on a visible monitor area
            var wa = Screen.FromRectangle(target).WorkingArea;
            if (!wa.IntersectsWith(target))
            {
                target.X = wa.Left + 50;
                target.Y = wa.Top + 50;
                target.Width = Math.Min(target.Width, wa.Width);
                target.Height = Math.Min(target.Height, wa.Height);
            }

            StartPosition = FormStartPosition.Manual;
            Bounds = target;

            // apply state last
            if (vault.WindowState == FormWindowState.Maximized)
                WindowState = FormWindowState.Maximized;
            else
                WindowState = FormWindowState.Normal;
        }
        private void PersistWindowPlacement()
        {
            // never persist minimized (confusing on next start)
            if (WindowState == FormWindowState.Minimized)
                return;

            var vault = LoadVault(vaultPath);

            // If maximized, save the restore bounds (normal size/pos),
            // but remember that we were maximized.
            var b = (WindowState == FormWindowState.Maximized) ? RestoreBounds : Bounds;

            vault.WinX = b.X;
            vault.WinY = b.Y;
            vault.WinW = b.Width;
            vault.WinH = b.Height;

            vault.WindowState = (WindowState == FormWindowState.Maximized)
                ? FormWindowState.Maximized
                : FormWindowState.Normal;

            SaveVault(vaultPath, vault);
        }

        /* history suggestion */
        private void TextBoxURL_TextChanged(object? sender, EventArgs e)
        {
            // restart debounce on every keystroke
            _historyDebounceTimer.Stop();
            _historyDebounceTimer.Start();
        }
        private void HistoryDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _historyDebounceTimer.Stop();
            UpdateHistorySuggestions();
        }
        private void UpdateHistorySuggestions()
        {
            var q = (textBoxURL.Text ?? "").Trim();

            // don't show suggestions if empty
            if (string.IsNullOrWhiteSpace(q))
            {
                HideSuggestList();
                return;
            }

            // load history (safe: never modify URL textbox here)
            var vault = LoadVault(vaultPath);
            var history = vault?.History ?? new List<string>();

            // contains match, case-insensitive, most recent first
            var matches = history
                .AsEnumerable()
                .Reverse()
                .Where(u => !string.IsNullOrWhiteSpace(u) &&
                            u.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(MaxSuggestions)
                .ToList();

            if (matches.Count == 0)
            {
                HideSuggestList();
                return;
            }

            _historySuggestList.BeginUpdate();
            _historySuggestList.Items.Clear();
            foreach (var m in matches)
                _historySuggestList.Items.Add(m);
            _historySuggestList.EndUpdate();

            PositionSuggestList();
            _historySuggestList.Visible = true;
        }
        private void PositionSuggestList()
        {
            // position under the URL box
            var tb = textBoxURL;
            var pt = this.PointToClient(tb.Parent.PointToScreen(tb.Location));

            _historySuggestList.Left = pt.X;
            _historySuggestList.Top = pt.Y + tb.Height;
            _historySuggestList.Width = tb.Width;

            // height: up to ~8 rows
            int rows = Math.Min(_historySuggestList.Items.Count, 8);
            _historySuggestList.Height = Math.Max(1, rows) * _historySuggestList.ItemHeight + 4;

            _historySuggestList.BringToFront();
        }
        private void HideSuggestList()
        {
            _historySuggestList.Visible = false;
            _historySuggestList.Items.Clear();
        }
        private void ApplySelectedSuggestion()
        {
            if (_historySuggestList.SelectedItem is not string selected ||
                string.IsNullOrWhiteSpace(selected))
                return;

            // put it in the box (no auto-suggest fill except explicit selection)
            textBoxURL.TextChanged -= TextBoxURL_TextChanged;
            textBoxURL.Text = selected;
            textBoxURL.SelectionStart = textBoxURL.Text.Length;
            textBoxURL.SelectionLength = 0;
            textBoxURL.TextChanged += TextBoxURL_TextChanged;

            HideSuggestList();
            textBoxURL.Focus();

            // navigate immediately
            Navigate(selected);
        }

        private void HistorySuggestList_Click(object? sender, EventArgs e)
        {
            ApplySelectedSuggestion();
        }
        private void HistorySuggestList_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplySelectedSuggestion();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                HideSuggestList();
                textBoxURL.Focus();
            }
        }
        private void TextBoxURL_Autocomplete_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!_historySuggestList.Visible)
                return;

            if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                if (_historySuggestList.Items.Count > 0)
                {
                    _historySuggestList.Focus();
                    _historySuggestList.SelectedIndex = Math.Min(
                        Math.Max(_historySuggestList.SelectedIndex, 0),
                        _historySuggestList.Items.Count - 1
                    );
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                HideSuggestList();
            }
            // IMPORTANT: do NOT auto-apply on Enter here (only on explicit list selection)
        }
        private void TextBoxURL_LostFocus(object? sender, EventArgs e)
        {
            // if focus goes to the suggestion list, keep it
            if (_historySuggestList.Focused)
                return;

            HideSuggestList();
        }
        /* /history suggestion */
    }
    public sealed class LocalVault
    {
        // URL memory
        public string LastLocation { get; set; } = "about:blank";
        public List<string> History { get; set; } = new();
        // Window placement
        public int? WinX { get; set; }
        public int? WinY { get; set; }
        public int? WinW { get; set; }
        public int? WinH { get; set; }

        // "Normal" or "Maximized" (never store "Minimized")
        public FormWindowState WindowState { get; set; } = FormWindowState.Normal;

        // Zoom level
        public double CurrentZoomLevel { get; set; } = 0;
    }
}
