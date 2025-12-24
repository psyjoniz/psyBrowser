using CefSharp;
using CefSharp.WinForms;
using System.Diagnostics;

namespace psyBrowser.InternalPages
{
    internal static class InternalPageRouter
    {
        private static readonly Dictionary<string, string> AboutRoutes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["about:config"] = "psybrowser://config/",
                ["about:history"] = "psybrowser://history/",
                ["about:about"] = "psybrowser://about/"
            };

        internal static bool TryHandle(ChromiumWebBrowser browser, string input)
        {
            var key = (input ?? "").Trim();

            if (AboutRoutes.TryGetValue(key, out var target))
            {
                browser.Load(target);
                return true;
            }

            return false;
        }
    }

}
