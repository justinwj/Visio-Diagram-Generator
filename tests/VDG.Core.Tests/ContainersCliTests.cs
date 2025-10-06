using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

public class ContainersCliTests
{
    private static string CreateTempJson(string content)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, content);
        return path;
    }

    private static string RunCliCapture(params string[] args)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var sw = new StringWriter();
        var se = new StringWriter();
        Console.SetOut(sw);
        Console.SetError(se);
        string? restoreSkip = Environment.GetEnvironmentVariable("VDG_SKIP_RUNNER", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", "1", EnvironmentVariableTarget.Process);
            var asm = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "VDG.CLI") ?? Assembly.Load("VDG.CLI");
            var type = asm.GetType("VDG.CLI.Program", throwOnError: true)!;
            var main = type.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            var exit = (int?)main.Invoke(null, new object[] { args });
            Assert.Equal(0, exit.GetValueOrDefault(-1));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", restoreSkip, EnvironmentVariableTarget.Process);
        }
        return sw.ToString() + se.ToString();
    }

    [Fact]
    public void Diagnostics_Reports_Containers_Count_And_Settings()
    {
        var json = "{\n" +
                   "  \"schemaVersion\": \"1.2\",\n" +
                   "  \"layout\": {\n" +
                   "    \"tiers\": [\"External\", \"Services\"],\n" +
                   "    \"containers\": { \"paddingIn\": 0.4, \"cornerIn\": 0.2 }\n" +
                   "  },\n" +
                   "  \"nodes\": [\n" +
                   "    { \"id\": \"E1\", \"label\": \"E1\", \"tier\": \"External\", \"containerId\": \"C_EXT\" },\n" +
                   "    { \"id\": \"S1\", \"label\": \"S1\", \"tier\": \"Services\", \"containerId\": \"C_SVC\" }\n" +
                   "  ],\n" +
                   "  \"containers\": [\n" +
                   "    { \"id\": \"C_EXT\", \"label\": \"External Zone\", \"tier\": \"External\" },\n" +
                   "    { \"id\": \"C_SVC\", \"label\": \"Services Zone\", \"tier\": \"Services\" }\n" +
                   "  ]\n" +
                   "}";
        var input = CreateTempJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunCliCapture(input, output);
            Assert.Contains("containers:", outText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("paddingIn=0.40in", outText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cornerIn=0.20in", outText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_Warns_Unknown_Container_Assignments()
    {
        var json = "{\n" +
                   "  \"schemaVersion\": \"1.2\",\n" +
                   "  \"layout\": { \"tiers\": [\"External\"] },\n" +
                   "  \"nodes\": [ { \"id\": \"E1\", \"label\": \"E1\", \"tier\": \"External\", \"containerId\": \"MISSING\" } ],\n" +
                   "  \"containers\": [ { \"id\": \"C_EXT\", \"label\": \"External Zone\", \"tier\": \"External\" } ]\n" +
                   "}";
        var input = CreateTempJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunCliCapture(input, output);
            Assert.Contains("unknown container", outText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }
}
