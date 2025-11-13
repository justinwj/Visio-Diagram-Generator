using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using Xunit;

public class RoutingCliTests
{
    private string CreateTempModelJson(string json)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private static (int ExitCode, string Output) RunMainWithExit(params string[] args)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var sw = new StringWriter();
        var se = new StringWriter();

        string? originalSkip = Environment.GetEnvironmentVariable("VDG_SKIP_RUNNER", EnvironmentVariableTarget.Process);
        int exitCode = -1;
        try
        {
            Console.SetOut(sw);
            Console.SetError(se);
            Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", "1", EnvironmentVariableTarget.Process);
            var cliAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "VDG.CLI")
                         ?? Assembly.Load("VDG.CLI");
            var programType = cliAsm.GetType("VDG.CLI.Program", throwOnError: true)!;
            var mainMethod = programType.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)!;
            exitCode = (mainMethod.Invoke(null, new object[] { args }) as int?) ?? -1;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", originalSkip, EnvironmentVariableTarget.Process);
        }
        var output = sw.ToString() + se.ToString();
        return (exitCode, output);
    }

    private static string RunMainCaptureOut(params string[] args)
    {
        var (exitCode, output) = RunMainWithExit(args);
        Assert.Equal(0, exitCode);
        return output;
    }

    [Fact]
    public void Diagnostics_Reports_RoutingMode_FromJson()
    {
        var json = """
        {
          "schemaVersion": "1.2",
          "layout": {
            "routing": { "mode": "straight" },
            "tiers": ["External", "Services"]
          },
          "nodes": [
            { "id": "A", "label": "A", "tier": "External" },
            { "id": "B", "label": "B", "tier": "Services" }
          ],
          "edges": [
            { "sourceId": "A", "targetId": "B" }
          ]
        }
        """;
        var input = CreateTempModelJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunMainCaptureOut(input, output);
            Assert.True(outText.IndexOf("routing mode:", StringComparison.OrdinalIgnoreCase) >= 0, outText);
            Assert.True(File.Exists(output));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_Reports_RoutingMode_CliOverride()
    {
        var json = """
        {
          "schemaVersion": "1.2",
          "layout": { "routing": { "mode": "orthogonal" } },
          "nodes": [ { "id": "A", "label": "A" }, { "id": "B", "label": "B" } ],
          "edges": [ { "sourceId": "A", "targetId": "B" } ]
        }
        """;
        var input = CreateTempModelJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunMainCaptureOut("--route-mode", "straight", input, output);
            Assert.Contains("routing mode: straight", outText, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(output));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_Reports_Bundles_And_Corridors()
    {
        var json = """
        {
          "schemaVersion": "1.2",
          "layout": {
            "tiers": ["External", "Services"],
            "routing": {
              "bundleBy": "nodePair",
              "channels": { "gapIn": 0.4 }
            }
          },
          "nodes": [
            { "id": "C1", "label": "C1", "tier": "External" },
            { "id": "C2", "label": "C2", "tier": "External" },
            { "id": "S1", "label": "S1", "tier": "Services" },
            { "id": "S2", "label": "S2", "tier": "Services" }
          ],
          "edges": [
            { "sourceId": "C1", "targetId": "S1" },
            { "sourceId": "C2", "targetId": "S2" },
            { "sourceId": "C1", "targetId": "S1" }
          ]
        }
        """;
        var input = CreateTempModelJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunMainCaptureOut(input, output);
            Assert.Contains("bundles planned", outText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("channels gapIn", outText, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(output));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_Reports_PlannedRoutes_And_Utilization()
    {
        var json = """
        {
          "schemaVersion": "1.2",
          "layout": {
            "tiers": ["External", "Services", "Data"],
            "routing": { "channels": { "gapIn": 0.5 } }
          },
          "nodes": [
            { "id": "E1", "label": "E1", "tier": "External" },
            { "id": "E2", "label": "E2", "tier": "External" },
            { "id": "S1", "label": "S1", "tier": "Services" },
            { "id": "D1", "label": "D1", "tier": "Data" }
          ],
          "edges": [
            { "sourceId": "E1", "targetId": "S1" },
            { "sourceId": "E2", "targetId": "D1" }
          ]
        }
        """;
        var input = CreateTempModelJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunMainCaptureOut(input, output);
            Assert.Contains("planned route crossings", outText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("channel utilization", outText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_Reports_Waypoints_Count()
    {
        var json = """
        {
          "schemaVersion": "1.2",
          "layout": { "tiers": ["Left", "Right"] },
          "nodes": [ { "id": "L", "label": "L", "tier": "Left" }, { "id": "R", "label": "R", "tier": "Right" } ],
          "edges": [ { "sourceId": "L", "targetId": "R", "waypoints": [ {"x": 0.5, "y": 0.5} ] } ]
        }
        """;
        var input = CreateTempModelJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunMainCaptureOut(input, output);
            Assert.Contains("edges with explicit waypoints", outText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_Warns_BundleSeparation_Ineffective_For_Tiny_Shapes()
    {
        var tinyJson = """
        {
          "schemaVersion": "1.2",
          "layout": {
            "tiers": ["External", "Services"],
            "routing": { "bundleSeparationIn": 0.30, "channels": { "gapIn": 0.4 } },
            "page": { "paginate": false }
          },
          "nodes": [
            { "id": "A1", "label": "A1", "tier": "External", "size": { "width": 1.4, "height": 0.20 } },
            { "id": "A2", "label": "A2", "tier": "External", "size": { "width": 1.4, "height": 0.20 } },
            { "id": "B1", "label": "B1", "tier": "Services", "size": { "width": 1.4, "height": 0.20 } },
            { "id": "B2", "label": "B2", "tier": "Services", "size": { "width": 1.4, "height": 0.20 } }
          ],
          "edges": [
            { "sourceId": "A1", "targetId": "B1" },
            { "sourceId": "A2", "targetId": "B2" },
            { "sourceId": "A1", "targetId": "B1" }
          ]
        }
        """;
        var input = CreateTempModelJson(tinyJson);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunMainCaptureOut(input, output);
            Assert.Contains("bundle separation", outText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ineffective", outText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_LaneWarn_EnvironmentOverridesThreshold()
    {
        var json = """
        {
          "schemaVersion": "1.2",
          "layout": {
            "tiers": ["Alpha"],
            "page": { "heightIn": 2.0, "marginIn": 0.1 }
          },
          "nodes": [
            { "id": "A", "label": "A", "tier": "Alpha" }
          ],
          "edges": []
        }
        """;
        var input = CreateTempModelJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");

        var originalWarn = Environment.GetEnvironmentVariable("VDG_DIAG_LANE_WARN", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("VDG_DIAG_LANE_WARN", null, EnvironmentVariableTarget.Process);
            var baseline = RunMainCaptureOut(input, output);
            Assert.DoesNotContain("lane crowded", baseline, StringComparison.OrdinalIgnoreCase);
            if (File.Exists(output)) File.Delete(output);

            Environment.SetEnvironmentVariable("VDG_DIAG_LANE_WARN", "0.50", EnvironmentVariableTarget.Process);
            var (exitCode, overridden) = RunMainWithExit(input, output);
            Assert.Equal(0, exitCode);
            Assert.Contains("lane crowded", overridden, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VDG_DIAG_LANE_WARN", originalWarn, EnvironmentVariableTarget.Process);
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_FailLevel_Error_ExitsWhenThresholdBreached()
    {
        var json = """
        {
          "schemaVersion": "1.2",
          "layout": {
            "tiers": ["Alpha"],
            "page": { "heightIn": 6.0, "marginIn": 0.25 }
          },
          "nodes": [
            { "id": "N1", "label": "N1", "tier": "Alpha" }
          ],
          "edges": []
        }
        """;
        var input = CreateTempModelJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");

        var originalLaneErr = Environment.GetEnvironmentVariable("VDG_DIAG_LANE_ERR", EnvironmentVariableTarget.Process);
        var originalFailLevel = Environment.GetEnvironmentVariable("VDG_DIAG_FAIL_LEVEL", EnvironmentVariableTarget.Process);
        var originalFailOn = Environment.GetEnvironmentVariable("VDG_DIAG_FAIL_ON", EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable("VDG_DIAG_LANE_ERR", "0.05", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("VDG_DIAG_FAIL_LEVEL", "error", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("VDG_DIAG_FAIL_ON", null, EnvironmentVariableTarget.Process);

            var (exitCode, outputText) = RunMainWithExit(input, output);
            Assert.Equal(65, exitCode);
            Assert.Contains("lane overcrowded", outputText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VDG_DIAG_LANE_ERR", originalLaneErr, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("VDG_DIAG_FAIL_LEVEL", originalFailLevel, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("VDG_DIAG_FAIL_ON", originalFailOn, EnvironmentVariableTarget.Process);
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Diagnostics_Corridor_Staggering_Applied()
    {
        var json = """
        {
          "schemaVersion": "1.2",
          "layout": {
            "tiers": ["External", "Services"],
            "routing": { "channels": { "gapIn": 0.5 } }
          },
          "nodes": [
            { "id": "E1", "label": "E1", "tier": "External" },
            { "id": "E2", "label": "E2", "tier": "External" },
            { "id": "S1", "label": "S1", "tier": "Services" },
            { "id": "S2", "label": "S2", "tier": "Services" }
          ],
          "edges": [
            { "sourceId": "E1", "targetId": "S1" },
            { "sourceId": "E2", "targetId": "S2" },
            { "sourceId": "E1", "targetId": "S1" }
          ]
        }
        """;
        var input = CreateTempModelJson(json);
        var output = Path.ChangeExtension(input, ".vsdx");
        try
        {
            var outText = RunMainCaptureOut(input, output);
            Assert.Contains("corridor staggering applied", outText, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(output));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
        }
    }
}
