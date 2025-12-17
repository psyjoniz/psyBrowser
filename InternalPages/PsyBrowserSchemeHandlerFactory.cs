using CefSharp;
using CefSharp.Handler;

namespace psyBrowser.InternalPages
{
    internal sealed class PsyBrowserSchemeHandlerFactory : ISchemeHandlerFactory
    {
        public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
        {
            return new PsyBrowserResourceHandler();
        }
    }
}
