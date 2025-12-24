using CefSharp;
using CefSharp.Handler;
using CefSharp.ResponseFilter;
using System.Text;
using System.Net;
using psyBrowser;


namespace psyBrowser.InternalPages
{
    internal sealed class PsyBrowserResourceHandler : ResourceHandler
    {
        public override CefReturnValue ProcessRequestAsync(IRequest request, ICallback callback)
        {
            // IMPORTANT: always call callback.Continue() or callback.Cancel()
            using (callback)
            {
                try
                {
                    // Example: psybrowser://config
                    var uri = new Uri(request.Url);

                    // Only GET for now (stub). Later you can add POST handling.
                    if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        StatusCode = 405;
                        StatusText = "Method Not Allowed";
                        MimeType = "text/plain";
                        Stream = new MemoryStream(Encoding.UTF8.GetBytes("Method not allowed."));
                        callback.Continue();
                        return CefReturnValue.Continue;
                    }

                    // Route by "host" (config, etc.)
                    var host = uri.Host.ToLowerInvariant();
                    var path = (uri.AbsolutePath ?? "/").ToLowerInvariant();

                    string html;

                    switch (host)
                    {
                        case "config":
                            // ignore path for now (keep it simple/expandable later)
                            html = InternalPageAssets.ReadHtml("about_config.html");
                            break;

                        case "history":
                            {
                                var vaultPath = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "psyBrowser",
                                    "vault.bin"
                                );

                                var vault = psyBrowser.LoadVault(vaultPath);
                                var history = vault?.History ?? new List<string>();

                                var sb = new StringBuilder();
                                foreach (var u in history.AsEnumerable().Reverse())
                                {
                                    var safe = WebUtility.HtmlEncode(u ?? "");
                                    sb.Append("<li><a href=\"")
                                      .Append(safe)
                                      .Append("\">")
                                      .Append(safe)
                                      .Append("</a></li>");
                                }

                                var template = InternalPageAssets.ReadHtml("about_history.html");
                                html = template.Replace("{{HISTORY_LIST}}", sb.ToString());
                                break;
                            }

                        case "about":
                            html = InternalPageAssets.ReadHtml("about.html");
                            break;

                        default:
                            StatusCode = 404;
                            StatusText = "404 Not Found";
                            MimeType = "text/plain";
                            Stream = new MemoryStream(Encoding.UTF8.GetBytes("404 Not found."));
                            callback.Continue();
                            return CefReturnValue.Continue;
                    }

                    StatusCode = 200;
                    StatusText = "OK";
                    MimeType = "text/html";
                    Stream = new MemoryStream(Encoding.UTF8.GetBytes(html));

                    callback.Continue();
                    return CefReturnValue.Continue;
                }
                catch (Exception ex)
                {
                    StatusCode = 500;
                    StatusText = "Internal Error";
                    MimeType = "text/plain";
                    Stream = new MemoryStream(Encoding.UTF8.GetBytes(ex.ToString()));
                    callback.Continue();
                    return CefReturnValue.Continue;
                }
            }
        }
    }
}
