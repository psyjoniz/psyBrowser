using CefSharp;
using CefSharp.WinForms;
using System.Diagnostics;

namespace psyBrowser.InternalPages
{
    internal static class InternalPageRouter
    {
        internal static bool TryHandle(ChromiumWebBrowser browser, string input)
        {
            var key = (input ?? "").Trim();

            if (string.Equals(key, "about:config", StringComparison.OrdinalIgnoreCase))
            {
                browser.Load("psybrowser://config");
                return true;
            }

            if (string.Equals(key, "about:config:history", StringComparison.OrdinalIgnoreCase))
            {
                browser.Load("psybrowser://config/history/");
                return true;
            }

            return false;
        }
    }
}
