using System;
using VDG.VisioRuntime.Infrastructure;
using VDG.VisioRuntime.Rendering;

namespace VDG.VisioRuntime.SampleCli
{
    /// <summary>
    /// Simple CLI entry point: renders a diagram from JSON; optionally saves .vsdx.
    /// All Visio automation runs on a dedicated STA thread.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: VDG.VisioRuntime <diagram.json> [out.vsdx]");
                return;
            }

            string jsonPath = args[0];
            string? vsdxPath = args.Length > 1 ? args[1] : null;

            using var host = new VisioStaHost(visible: true);

            // Render diagram on STA
            host.Invoke(svc => VisioJsonRenderer.RenderJsonFromFile(svc, jsonPath));

            // Optionally save (.vsdx extension normalized)
            if (!string.IsNullOrWhiteSpace(vsdxPath))
            {
                var outPath = System.IO.Path.ChangeExtension(vsdxPath, ".vsdx");
                host.Invoke(svc => svc.SaveAsVsdx(outPath));
                Console.WriteLine($"Saved diagram to {outPath}");
            }
        }
    }
}
