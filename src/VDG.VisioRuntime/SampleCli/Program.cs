using System;
using System.IO;
using System.Runtime.InteropServices; // COMException
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

            // Normalize + validate JSON path
            var jsonFull = Path.GetFullPath(args[0]);
            if (!File.Exists(jsonFull))
            {
                Console.Error.WriteLine($"JSON not found: {jsonFull}");
                Environment.Exit(2);
            }

            // Resolve output path: if relative, anchor to the JSON's directory
            string outFull = null;
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                var vsdxArg = args[1];
                var jsonDir = Path.GetDirectoryName(jsonFull)!;
                var target  = Path.ChangeExtension(vsdxArg, ".vsdx");
                outFull     = Path.GetFullPath(
                                Path.IsPathRooted(target) ? target
                                  : Path.Combine(jsonDir, target));
            }

            Console.WriteLine($"WorkingDir: {Environment.CurrentDirectory}");
            Console.WriteLine($"JSON:       {jsonFull}");
            if (outFull != null) Console.WriteLine($"Output:     {outFull}");

            try
            {
                using var host = new VisioStaHost(visible: true);

                // Ensure there's a document and page, then render
                host.Invoke(svc => svc.EnsureDocumentAndPage());
                host.Invoke(svc => VisioJsonRenderer.RenderJsonFromFile(svc, jsonFull));

                // Optionally save
                if (!string.IsNullOrEmpty(outFull))
                {
                    host.Invoke(svc => svc.SaveAsVsdx(outFull));
                    Console.WriteLine($"Saved diagram to {outFull}");
                    Console.WriteLine(File.Exists(outFull) ? "[OK] File exists." : "[WARN] File not found after save.");
                }
            }
            catch (COMException ex)
            {
                Console.Error.WriteLine($"[COM] {ex.Message} (0x{ex.ErrorCode:X8})");
                Environment.Exit(3);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                Environment.Exit(4);
            }
        }
    }
}