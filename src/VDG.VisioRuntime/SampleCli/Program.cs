using System;
using System.IO;
using VDG.VisioRuntime.Infrastructure;
using VDG.VisioRuntime.Rendering;

namespace VDG.VisioRuntime.SampleCli
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: VDG.VisioRuntime <diagram.json> [out.vsdx]");
                Environment.Exit(1);
            }

            var jsonPath = args[0];
            var vsdxArg  = args.Length > 1 ? args[1] : null;

            // Normalize + validate JSON path
            var jsonFull = Path.GetFullPath(jsonPath);
            if (!File.Exists(jsonFull))
            {
                Console.Error.WriteLine($"JSON not found: {jsonFull}");
                Environment.Exit(2);
            }

            using var host = new VisioStaHost(visible: true);

            // Ensure there's a document and page, then render
            host.Invoke(svc => svc.EnsureDocumentAndPage());
            host.Invoke(svc => VisioJsonRenderer.RenderJsonFromFile(svc, jsonFull));

            // Optionally save — normalize to absolute path using the CLI's CWD
            if (!string.IsNullOrWhiteSpace(vsdxArg))
            {
                var outFull = Path.GetFullPath(Path.ChangeExtension(vsdxArg, ".vsdx"));
                host.Invoke(svc => svc.SaveAsVsdx(outFull));
                Console.WriteLine($"Saved diagram to {outFull}");
            }
        }
    }
}
