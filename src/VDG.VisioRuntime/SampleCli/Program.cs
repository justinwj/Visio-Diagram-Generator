using System;
using VDG.VisioRuntime.Infrastructure;
using VDG.VisioRuntime.Rendering;

namespace VDG.VisioRuntime.SampleCli
{
    /// <summary>
    /// A simple command‑line entry point demonstrating how to use
    /// VisioStaHost with VisioJsonRenderer.  It accepts a JSON file and
    /// optionally an output .vsdx file.  All drawing operations are
    /// marshalled onto a dedicated STA thread.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet run -- <diagram.json> [out.vsdx]");
                return;
            }

            string jsonPath = args[0];
            string? vsdxPath = args.Length > 1 ? args[1] : null;

            using var host = new VisioStaHost(visible: true);

            // Render diagram on STA
            host.Invoke(svc => VisioJsonRenderer.RenderFile(svc, jsonPath));

            // Optionally save
            if (vsdxPath != null)
            {
                host.Invoke(svc => svc.SaveAsVsdx(vsdxPath));
                Console.WriteLine($"Saved diagram to {vsdxPath}");
            }
        }
    }
}