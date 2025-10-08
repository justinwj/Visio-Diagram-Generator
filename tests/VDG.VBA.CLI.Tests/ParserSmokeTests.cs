using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

public class ParserSmokeTests
{
    private static readonly string RepoRoot = LocateRepoRoot();
    private static readonly string CliProjectPath = Path.Combine(RepoRoot, "src", "VDG.VBA.CLI", "VDG.VBA.CLI.csproj");

    private static string RunCli(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepoRoot
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(CliProjectPath);
        start.ArgumentList.Add("--");
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

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

    [Fact]
    public void ModuleCallMapAggregatesCrossModuleCalls()
    {
        using var diagram = GenerateDiagram("cross_module_calls", "module-callmap");
        var nodes = diagram.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "Module1");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "Module2");

        var edge = diagram.RootElement.GetProperty("edges").EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("sourceId").GetString() == "Module1" &&
                                 e.GetProperty("targetId").GetString() == "Module2");
        Assert.NotEqual(JsonValueKind.Undefined, edge.ValueKind);
        Assert.Equal("1 call(s)", edge.GetProperty("label").GetString());
        Assert.Equal("module-call", edge.GetProperty("metadata").GetProperty("code.edge").GetString());
    }

    [Fact]
    public void EventWiringConnectsControlsToHandlers()
    {
        using var diagram = GenerateDiagram("events_and_forms", "event-wiring");
        var nodes = diagram.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "Form1.Command1");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "Form1.Command1_Click");

        var edge = diagram.RootElement.GetProperty("edges").EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("sourceId").GetString() == "Form1.Command1" &&
                                 e.GetProperty("targetId").GetString() == "Form1.Command1_Click");
        Assert.NotEqual(JsonValueKind.Undefined, edge.ValueKind);
        Assert.Equal("Click", edge.GetProperty("label").GetString());
        Assert.Equal("event", edge.GetProperty("metadata").GetProperty("code.edge").GetString());
    }

    [Fact]
    public void ProcCfgBuildsLinearFlowForSimpleProcedures()
    {
        using var diagram = GenerateDiagram("cross_module_calls", "proc-cfg");
        var nodes = diagram.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var containers = diagram.RootElement.GetProperty("containers").EnumerateArray().ToList();

        Assert.Contains(containers, c => c.GetProperty("id").GetString() == "Module1.Caller#proc");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "Module1.Caller#start");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "Module1.Caller#call:Module2.Work");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "Module1.Caller#end");

        var edges = diagram.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "Module1.Caller#start" &&
                                    e.GetProperty("targetId").GetString() == "Module1.Caller#call:Module2.Work" &&
                                    e.GetProperty("metadata").GetProperty("code.edge").GetString() == "flow");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "Module1.Caller#call:Module2.Work" &&
                                    e.GetProperty("targetId").GetString() == "Module1.Caller#end" &&
                                    e.GetProperty("metadata").GetProperty("code.edge").GetString() == "flow");
    }

    [Fact]
    public void ProcCfgEmitsDecisionNodes()
    {
        using var diagram = GenerateDiagram("cfg_shapes", "proc-cfg");
        var nodes = diagram.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithIf#dec");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithIf#then");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithIf#call:ModuleCfg.HelperA");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithIf#end");

        var edges = diagram.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithIf#dec" &&
                                    e.GetProperty("targetId").GetString() == "ModuleCfg.WithIf#then" &&
                                    e.GetProperty("label").GetString() == "True");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithIf#dec" &&
                                    e.GetProperty("targetId").GetString() == "ModuleCfg.WithIf#end" &&
                                    e.GetProperty("label").GetString() == "False");
    }

    [Fact]
    public void ProcCfgEmitsLoopNodes()
    {
        using var diagram = GenerateDiagram("cfg_shapes", "proc-cfg");
        var nodes = diagram.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithLoop#loop");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithLoop#call:ModuleCfg.HelperB");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithLoop#end");

        var edges = diagram.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithLoop#loop" &&
                                    e.GetProperty("targetId").GetString() == "ModuleCfg.WithLoop#call:ModuleCfg.HelperB" &&
                                    e.GetProperty("label").GetString() == "seq");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithLoop#call:ModuleCfg.HelperB" &&
                                    e.GetProperty("targetId").GetString() == "ModuleCfg.WithLoop#loop" &&
                                    e.GetProperty("label").GetString() == "back");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithLoop#loop" &&
                                    e.GetProperty("targetId").GetString() == "ModuleCfg.WithLoop#end" &&
                                    e.GetProperty("label").GetString() == "exit");
    }

    [Fact]
    public void ProcCfgHandlesNestedLoopWithBranch()
    {
        using var diagram = GenerateDiagram("cfg_nested", "proc-cfg");
        var nodes = diagram.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleNested.LoopWithBranch#loop");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleNested.LoopWithBranch#dec");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleNested.LoopWithBranch#then");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleNested.LoopWithBranch#call:ModuleNested.HelperEven");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleNested.LoopWithBranch#call:ModuleNested.HelperOdd");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleNested.LoopWithBranch#end");

        var edges = diagram.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#loop" &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#dec" &&
                                    e.GetProperty("label").GetString() == "iter");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#dec" &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#then" &&
                                    e.GetProperty("label").GetString() == "True");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#call:ModuleNested.HelperOdd" &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#loop" &&
                                    e.GetProperty("label").GetString() == "back");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#dec" &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#end" &&
                                    e.GetProperty("label").GetString() == "False");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#loop" &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#end" &&
                                    e.GetProperty("label").GetString() == "exit");
    }

    [Fact]
    public void Vba2JsonMatchesGoldenFixture()
    {
        var actual = JsonNode.Parse(GenerateIrJson("hello_world"));
        var expectedPath = Path.Combine(RepoRoot, "tests", "fixtures", "ir", "hello_world.json");
        var expected = JsonNode.Parse(File.ReadAllText(expectedPath));

        Assert.NotNull(actual);
        Assert.NotNull(expected);
        Assert.True(JsonNode.DeepEquals(expected, actual!), "Generated IR diverges from golden fixture for hello_world.");
    }

    [Fact]
    public void Vba2JsonOutputIsDeterministic()
    {
        static string Normalize(string text) => text.Replace("\r\n", "\n");
        var first = Normalize(GenerateIrJson("cross_module_calls"));
        var second = Normalize(GenerateIrJson("cross_module_calls"));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Vba2JsonEmitsRequiredFields()
    {
        using var ir = GenerateIrDocument("events_and_forms");
        var root = ir.RootElement;
        Assert.True(root.TryGetProperty("irSchemaVersion", out var version));
        Assert.Equal("0.1", version.GetString());

        var project = root.GetProperty("project");
        Assert.False(string.IsNullOrWhiteSpace(project.GetProperty("name").GetString()));

        var modules = project.GetProperty("modules");
        Assert.True(modules.GetArrayLength() > 0);
        foreach (var module in modules.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(module.GetProperty("id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(module.GetProperty("name").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(module.GetProperty("kind").GetString()));

            var procedures = module.GetProperty("procedures");
            Assert.True(procedures.GetArrayLength() > 0);
            foreach (var procedure in procedures.EnumerateArray())
            {
                Assert.False(string.IsNullOrWhiteSpace(procedure.GetProperty("id").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(procedure.GetProperty("name").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(procedure.GetProperty("kind").GetString()));
            }
        }
    }

    [Fact]
    public void AliasAndChainedCallsAreCaptured()
    {
        using var ir = GenerateIrDocument("alias_and_chain");
        var project = ir.RootElement.GetProperty("project");
        var modules = project.GetProperty("modules").EnumerateArray();
        var caller = modules
            .SelectMany(m => m.GetProperty("procedures").EnumerateArray())
            .Single(p => p.GetProperty("id").GetString() == "ModuleAlias.Caller");

        var calls = caller.GetProperty("calls").EnumerateArray().ToList();
        Assert.Contains(calls, c => c.GetProperty("target").GetString() == "Worker.DoWork");
        Assert.Contains(calls, c => c.GetProperty("target").GetString() == "Worker.Factory");
        Assert.Contains(calls, c => c.GetProperty("target").GetString() == "Worker.RunFactory");
        var helperRunAll = calls.Where(c => c.GetProperty("target").GetString() == "Helper.RunAll").ToList();
        Assert.True(helperRunAll.Count >= 4, "Expected multiple Helper.RunAll targets from aliases and chains.");
    }

    private static JsonDocument GenerateDiagram(string fixtureName, string mode)
    {
        var irPath = Path.Combine(Path.GetTempPath(), $"vdg_ir_{Guid.NewGuid():N}.json");
        var diagramPath = Path.Combine(Path.GetTempPath(), $"vdg_diag_{Guid.NewGuid():N}.json");
        JsonDocument doc;
        try
        {
            var fixtureDir = Path.Combine(RepoRoot, "tests", "fixtures", "vba", fixtureName);
            RunCli("vba2json", "--in", fixtureDir, "--out", irPath);
            RunCli("ir2diagram", "--in", irPath, "--out", diagramPath, "--mode", mode);
            doc = JsonDocument.Parse(File.ReadAllText(diagramPath));
        }
        finally
        {
            if (File.Exists(irPath)) File.Delete(irPath);
            if (File.Exists(diagramPath)) File.Delete(diagramPath);
        }

        return doc;
    }

    private static JsonDocument GenerateIrDocument(string fixtureName) =>
        JsonDocument.Parse(GenerateIrJson(fixtureName));

    private static string GenerateIrJson(string fixtureName)
    {
        var irPath = Path.Combine(Path.GetTempPath(), $"vdg_ir_{Guid.NewGuid():N}.json");
        try
        {
            var fixtureDir = Path.Combine(RepoRoot, "tests", "fixtures", "vba", fixtureName);
            RunCli("vba2json", "--in", fixtureDir, "--out", irPath);
            return File.ReadAllText(irPath);
        }
        finally
        {
            if (File.Exists(irPath)) File.Delete(irPath);
        }
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Visio-Diagram-Generator.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Unable to locate repository root.");
    }
}

