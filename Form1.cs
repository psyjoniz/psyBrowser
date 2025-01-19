using CefSharp;
using CefSharp.WinForms;
using System.Diagnostics;
using System.Reflection;

namespace psyBrowser
{
    public partial class psyBrowser : Form
    {
        private ChromiumWebBrowser browser;
        private double currentZoomLevel = 0;
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Debug.WriteLine($"Key pressed: {keyData}");
            if (keyData == (Keys.Control | Keys.N))
            {
                Debug.WriteLine($"===============================================");
                try
                {
                    string exePath = Assembly.GetExecutingAssembly().Location;
                    Debug.WriteLine($"Launching new instance from: {exePath}");
                    Process.Start(exePath);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error launching new instance: {ex.Message}");
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        public psyBrowser()
        {
            InitializeComponent();
            /* BEWARE
            var appName = Assembly.GetExecutingAssembly().GetName().Name;
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            var settings = new CefSettings
            {
                UserAgent = $"{appName}/{appVersion} (Windows NT; Custom Browser)"
            };

            Cef.Initialize(settings);
            /BEWARE */
            var url = "about:blank";
            browser = new ChromiumWebBrowser(url);
            textBoxURL.Text = url;
            panelRenderer.Controls.Add(browser);

            textBoxURL.KeyDown += TextBoxURL_KeyDown;

            browser.KeyboardHandler = new CustomKeyboardHandler(this);

            this.KeyPreview = true;

            /* this needs to be implemented but doesn't seem to be the right place for it (its persistant; once first page doesn't load nothing else will render)
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
        private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            // Ensure UI updates are done on the UI thread
            this.Invoke(new Action(() =>
            {
                if (e.IsLoading)
                {
                    progressBarPageLoading.Visible = true; // Show progress bar
                }
                else
                {
                    progressBarPageLoading.Visible = false; // Hide progress bar
                }
            }));
        }
        private void TextBoxURL_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (IsUrl(textBoxURL.Text) || IsUrl("https://" + textBoxURL.Text))
                {
                    string url = textBoxURL.Text;
                    if (
                        !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase)
                        )
                    {
                        url = "https://" + url;
                    }
                    textBoxURL.Text = url;
                    browser.Load(url);
                } else
                {
                    string searchUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(textBoxURL.Text);
                    browser.Load(searchUrl);
                    textBoxURL.Text = searchUrl;
                    //Debug.WriteLine("processing search term");
                }
                e.SuppressKeyPress = true;
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            browser.Dispose();
            Cef.Shutdown(); //cleanup cpu/mem footprint
            base.OnFormClosing(e);
        }
        private bool IsUrl(string input)
        {
            if (!input.Contains('.') && !input.Contains(' '))
            {
                return false;
            }

            try
            {
                var uri = new Uri(input);
                return !string.IsNullOrEmpty(uri.Scheme);
            }
            catch
            {
                return false;
            }

            return false;
        }
        private class CustomKeyboardHandler : IKeyboardHandler
        {
            private readonly psyBrowser form;

            public CustomKeyboardHandler(psyBrowser form)
            {
                this.form = form;
            }
            
            public bool OnKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey)
            {
                Debug.WriteLine($"OnKeyEvent()");
                // Forward key press events to the form
                if (type == KeyType.RawKeyDown && (modifiers & CefEventFlags.ControlDown) != 0)
                {
                    Debug.WriteLine($"Ctrl pressed");
                    if (windowsKeyCode == (int)Keys.Oemplus || windowsKeyCode == (int)Keys.Add) // Ctrl + =
                    {
                        Debug.WriteLine($"= pressed (zooming in)");
                        form.AdjustZoomLevel(1); // Zoom in
                        return true;
                    }
                    if (windowsKeyCode == (int)Keys.OemMinus || windowsKeyCode == (int)Keys.Subtract) // Ctrl + -
                    {
                        form.AdjustZoomLevel(-1); // Zoom out
                        return true;
                    }
                    if (windowsKeyCode == (int)Keys.D0) // Ctrl + 0
                    {
                        form.currentZoomLevel = 0;
                        chromiumWebBrowser.SetZoomLevel(0); // Reset zoom
                        return true;
                    }
                    if ((modifiers & CefEventFlags.ShiftDown) == 0 && windowsKeyCode == (int)Keys.R) // Ctrl + R
                    {
                        chromiumWebBrowser.Reload(); //normal reload
                    }
                    if ((modifiers & CefEventFlags.ShiftDown) != 0 && windowsKeyCode == (int)Keys.R) // Ctrl + Shift + R
                    {
                        chromiumWebBrowser.Reload(true); //hard reload
                    }
                    if (windowsKeyCode == (int)Keys.N)
                    {
                        Debug.WriteLine($"Ctrl + N pressed (launching new instance of application)");
                        Process.Start(Assembly.GetExecutingAssembly().Location);
                    }
                }
                return false; // Let other events propagate
            }

            public bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
            {
                // No action needed for pre-key events
                return false;
            }
        }
        private void AdjustZoomLevel(int delta)
        {
            currentZoomLevel += delta;
            browser.SetZoomLevel(currentZoomLevel);
            Debug.WriteLine($"setting currentZoomLevel: {currentZoomLevel}");
        }
    }
}
