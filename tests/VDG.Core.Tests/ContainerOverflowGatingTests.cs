using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

public class ContainerOverflowGatingTests
{
    private static string TempJson(string content)
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
    public void SubContainerOverflow_Warning_Gated_By_Level()
    {
        var json = "{\n" +
                   "  \"schemaVersion\": \"1.2\",\n" +
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
        try
        {
            // At info level, the overflow warning should be visible
            var textInfo = RunCliCapture("--diag-level", "info", input, output);
            Assert.Contains("sub-container", textInfo, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("overflows lane", textInfo, StringComparison.OrdinalIgnoreCase);

            // At error level, the overflow warning should be suppressed
            var textErr = RunCliCapture("--diag-level", "error", input, output);
            Assert.DoesNotContain("sub-container", textErr, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("overflows lane", textErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }
}

