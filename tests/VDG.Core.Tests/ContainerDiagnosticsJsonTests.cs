using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

public class ContainerDiagnosticsJsonTests
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
    public void Emits_ContainerOverflow_In_Diagnostics_Json()
    {
        var json = "{\n" +
                   "  \"schemaVersion\": \"1.2\",\n" +
                   "  \"metadata\": {\n" +
                   "    \"properties\": {\n" +
                   "      \"layout.outputMode\": \"print\"\n" +
                   "    }\n" +
                   "  },\n" +
                   "  \"layout\": {\n" +
                   "    \"tiers\": [\"Services\"],\n" +
                   "    \"page\": { \"heightIn\": 8.5, \"marginIn\": 0.5, \"paginate\": false }\n" +
                   "  },\n" +
                   "  \"nodes\": [\n" +
                   "    { \"id\": \"S1\", \"label\": \"S1\", \"tier\": \"Services\", \"size\": {\"width\": 1.8, \"height\": 1.0} }\n" +
                   "  ],\n" +
                   "  \"containers\": [\n" +
                   "    { \"id\": \"C_SVC\", \"label\": \"Svc\", \"tier\": \"Services\", \"bounds\": { \"x\": 0.0, \"y\": 0.0, \"width\": 20.0, \"height\": 2.0 } }\n" +
                   "  ]\n" +
                   "}";

        var input = TempJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        var diagPath = Path.ChangeExtension(input, ".diag.json");
        try
        {
            var exit = RunCli(new[] { "--diag-level", "info", "--diag-json", diagPath, input, output });
            Assert.Equal(0, exit);
            var text = File.ReadAllText(diagPath);
            Assert.Contains("\"Code\": \"ContainerOverflow\"", text, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
            if (File.Exists(diagPath)) File.Delete(diagPath);
        }
    }

    [Fact]
    public void Emits_ContainerCrowding_For_Inferred_Bounds()
    {
        var json = "{\n" +
                   "  \"schemaVersion\": \"1.2\",\n" +
                   "  \"metadata\": {\n" +
                   "    \"properties\": {\n" +
                   "      \"layout.outputMode\": \"print\"\n" +
                   "    }\n" +
                   "  },\n" +
                   "  \"layout\": {\n" +
                   "    \"tiers\": [\"Services\"],\n" +
                   "    \"spacing\": { \"vertical\": 0.6 },\n" +
                   "    \"page\": { \"heightIn\": 2.5, \"marginIn\": 0.2, \"paginate\": false }\n" +
                   "  },\n" +
                   "  \"nodes\": [\n" +
                   "    { \"id\": \"S1\", \"label\": \"S1\", \"tier\": \"Services\", \"size\": {\"width\": 1.8, \"height\": 1.2}, \"containerId\": \"C_SVC\" },\n" +
                   "    { \"id\": \"S2\", \"label\": \"S2\", \"tier\": \"Services\", \"size\": {\"width\": 1.8, \"height\": 1.2}, \"containerId\": \"C_SVC\" }\n" +
                   "  ],\n" +
                   "  \"containers\": [\n" +
                   "    { \"id\": \"C_SVC\", \"label\": \"Svc\", \"tier\": \"Services\" }\n" +
                   "  ]\n" +
                   "}";

        var input = TempJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        var diagPath = Path.ChangeExtension(input, ".diag.json");
        try
        {
            var exit = RunCli(new[] { "--diag-level", "info", "--diag-json", diagPath, input, output });
            Assert.Equal(0, exit);
            var text = File.ReadAllText(diagPath);
            Assert.Contains("\"Code\": \"ContainerCrowding\"", text, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
            if (File.Exists(diagPath)) File.Delete(diagPath);
        }
    }
}

