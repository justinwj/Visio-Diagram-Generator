using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

public class LayoutDiagnosticsTests
{
    private static string TempJson(string content)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, content);
        return path;
    }

    private static string RunCli(string inputPath, string outputPath, params string[] extraArgs)
    {
        var originalOut = Console.Out; var originalErr = Console.Error;
        var sw = new StringWriter(); var se = new StringWriter();
        Console.SetOut(sw); Console.SetError(se);
        string? restore = Environment.GetEnvironmentVariable("VDG_SKIP_RUNNER", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", "1", EnvironmentVariableTarget.Process);
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "VDG.CLI") ?? Assembly.Load("VDG.CLI");
            var type = asm.GetType("VDG.CLI.Program", throwOnError: true)!;
            var main = type.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)!;
            var args = extraArgs.Concat(new[] { inputPath, outputPath }).ToArray();
            var exit = (int?)main.Invoke(null, new object[] { args });
            Assert.Equal(0, exit.GetValueOrDefault(-1));
        }
        finally
        {
            Console.SetOut(originalOut); Console.SetError(originalErr);
            Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", restore, EnvironmentVariableTarget.Process);
        }
        return sw.ToString() + se.ToString();
    }

    [Fact]
    public void Emits_Lane_Crowding_Warnings_When_Over_Occupancy()
    {
        var json = "{\n" +
                   "  \"schemaVersion\": \"1.2\",\n" +
                   "  \"layout\": {\n" +
                   "    \"tiers\": [\"Services\"],\n" +
                   "    \"spacing\": { \"vertical\": 0.6 },\n" +
                   "    \"page\": { \"heightIn\": 2.5, \"marginIn\": 0.2, \"paginate\": false }\n" +
                   "  },\n" +
                   "  \"nodes\": [\n" +
                   "    { \"id\": \"S1\", \"label\": \"S1\", \"tier\": \"Services\", \"size\": {\"width\": 1.8, \"height\": 1.2} },\n" +
                   "    { \"id\": \"S2\", \"label\": \"S2\", \"tier\": \"Services\", \"size\": {\"width\": 1.8, \"height\": 1.2} },\n" +
                   "    { \"id\": \"S3\", \"label\": \"S3\", \"tier\": \"Services\", \"size\": {\"width\": 1.8, \"height\": 1.2} }\n" +
                   "  ]\n" +
                   "}";
        var input = TempJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var text = RunCli(input, output, "--diag-lane-warn", "0.80", "--diag-lane-error", "0.90");
            Assert.Contains("lane", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("crowded", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }
}
