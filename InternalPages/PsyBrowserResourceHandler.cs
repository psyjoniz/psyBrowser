using CefSharp;
using CefSharp.Handler;
using CefSharp.ResponseFilter;
using System.Text;

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
                    var pageKey = uri.Host.ToLowerInvariant();

                    string html;
                    switch (pageKey)
                    {
                        case "config":
                            html = InternalPageAssets.ReadHtml("about_config.html");
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
