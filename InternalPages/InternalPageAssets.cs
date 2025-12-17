using System.Diagnostics;
using System.Reflection;

namespace psyBrowser.InternalPages
{
    internal static class InternalPageAssets
    {
        internal static string ReadHtml(string fileName)
        {
            //System.Windows.Forms.MessageBox.Show("InternalPageAssets::ReadHtml fileName: " + fileName);
            //Debug.WriteLine("InternalPageAssets::ReadHtml fileName: " + fileName);
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = $"{asm.GetName().Name}.InternalPages.{fileName}";

            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Missing embedded resource: {resourceName}");

            using var reader = new StreamReader(stream);
            string contents = reader.ReadToEnd();
            //System.Windows.Forms.MessageBox.Show(contents);
            //Debug.WriteLine("contents: " + contents);
            return contents;
        }
    }
}
