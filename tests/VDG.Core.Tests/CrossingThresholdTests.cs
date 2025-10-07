using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

public class CrossingThresholdTests
{
    private static string TempJson(string content)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, content);
        return path;
    }

    private static int RunCli(string[] args)
    {
        string? restoreSkip = Environment.GetEnvironmentVariable("VDG_SKIP_RUNNER", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", "1", EnvironmentVariableTarget.Process);
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "VDG.CLI") ?? Assembly.Load("VDG.CLI");
            var type = asm.GetType("VDG.CLI.Program", throwOnError: true)!;
            var main = type.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            var exit = (int?)main.Invoke(null, new object[] { args });
            return exit.GetValueOrDefault(-1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", restoreSkip, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void Emits_CrossingDensity_Issue_When_Above_Threshold()
    {
        var json = "{\n" +
                   "  \"schemaVersion\": \"1.2\",\n" +
                   "  \"layout\": {\n" +
                   "    \"tiers\": [\"External\", \"Services\"],\n" +
                   "    \"routing\": { \"channels\": { \"gapIn\": 0.5 } }\n" +
                   "  },\n" +
                   "  \"nodes\": [\n" +
                   "    { \"id\": \"E1\", \"label\": \"E1\", \"tier\": \"External\" },\n" +
                   "    { \"id\": \"E2\", \"label\": \"E2\", \"tier\": \"External\" },\n" +
                   "    { \"id\": \"S1\", \"label\": \"S1\", \"tier\": \"Services\" },\n" +
                   "    { \"id\": \"S2\", \"label\": \"S2\", \"tier\": \"Services\" }\n" +
                   "  ],\n" +
                   "  \"edges\": [\n" +
                   "    { \"sourceId\": \"E1\", \"targetId\": \"S2\" },\n" +
                   "    { \"sourceId\": \"E2\", \"targetId\": \"S1\" }\n" +
                   "  ]\n" +
                   "}";

        var input = TempJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        var diagPath = Path.ChangeExtension(input, ".diag.json");
        try
        {
            var exit = RunCli(new[] { "--diag-level", "info", "--diag-json", diagPath, "--diag-cross-warn", "1", input, output });
            Assert.Equal(0, exit);
            var text = File.ReadAllText(diagPath);
            Assert.Contains("\"Code\": \"CrossingDensity\"", text, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
            if (File.Exists(diagPath)) File.Delete(diagPath);
        }
    }
}

