using System;
using System.Diagnostics;
using System.IO;
using Xunit;

public class ParserSmokeTests
{
    private static string RunCli(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet", $"run --project src/VDG.VBA.CLI -- {string.Join(' ', args)}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };
        using var p = Process.Start(start)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new Exception($"CLI failed: {stderr}\n{stdout}");
        return stdout + stderr;
    }

    [Fact]
    public void CrossModuleCalls_Appear_In_Callgraph_Diagram()
    {
        var ir = Path.GetTempFileName(); var dj = Path.GetTempFileName();
        try
        {
            File.Delete(ir); File.Delete(dj);
            RunCli("vba2json", "--in", "tests/fixtures/vba/cross_module_calls", "--out", ir);
            RunCli("ir2diagram", "--in", ir, "--out", dj, "--mode", "callgraph");
            var text = File.ReadAllText(dj);
            Assert.Contains("Module1.Caller", text);
            Assert.Contains("Module2.Work", text);
        }
        finally { if (File.Exists(ir)) File.Delete(ir); if (File.Exists(dj)) File.Delete(dj); }
    }
}

