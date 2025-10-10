using System;
using System.Collections.Generic;
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
    private static readonly string[] ModuleKinds = { "Module", "Class", "Form" };
    private static readonly string[] ProcedureKinds = { "Sub", "Function", "PropertyGet", "PropertyLet", "PropertySet" };
    private static readonly string[] AccessModifiers = { "Public", "Private", "Friend" };

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
            using var diagram = JsonDocument.Parse(File.ReadAllText(dj));
            var root = diagram.RootElement;
            var nodes = root.GetProperty("nodes").EnumerateArray().ToList();
            var callerNode = nodes.Single(n => n.GetProperty("id").GetString() == "Module1.Caller");
            var callerMeta = callerNode.GetProperty("metadata");
            Assert.Equal("Module1", callerMeta.GetProperty("code.module").GetString());
            Assert.Equal("Caller", callerMeta.GetProperty("code.proc").GetString());
            Assert.Equal("Module1.bas", callerMeta.GetProperty("code.locs.file").GetString());
            Assert.Equal("4", callerMeta.GetProperty("code.locs.startLine").GetString());
            Assert.Equal("6", callerMeta.GetProperty("code.locs.endLine").GetString());

            var edges = root.GetProperty("edges").EnumerateArray().ToList();
            var callEdge = edges.Single(e => e.GetProperty("sourceId").GetString() == "Module1.Caller" &&
                                             e.GetProperty("targetId").GetString() == "Module2.Work");
            var edgeMeta = callEdge.GetProperty("metadata");
            Assert.Equal("call", edgeMeta.GetProperty("code.edge").GetString());
            Assert.Equal("Module1", edgeMeta.GetProperty("code.site.module").GetString());
            Assert.Equal("Module1.bas", edgeMeta.GetProperty("code.site.file").GetString());
            Assert.Equal("5", edgeMeta.GetProperty("code.site.line").GetString());
            Assert.False(edgeMeta.TryGetProperty("code.branch", out _));
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
        Assert.Contains(nodes, n => n.GetProperty("id").GetString()?.StartsWith("Module1.Caller#call:Module2.Work") == true);
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "Module1.Caller#end");

        var edges = diagram.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "Module1.Caller#start" &&
                                    e.GetProperty("targetId").GetString()?.StartsWith("Module1.Caller#call:Module2.Work") == true &&
                                    e.GetProperty("metadata").GetProperty("code.edge").GetString() == "flow");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString()?.StartsWith("Module1.Caller#call:Module2.Work") == true &&
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
        Assert.Contains(nodes, n => n.GetProperty("id").GetString()?.StartsWith("ModuleCfg.WithIf#call:ModuleCfg.HelperA") == true);
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithIf#else");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString()?.StartsWith("ModuleCfg.WithIf#call:ModuleCfg.HelperB") == true);
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithIf#end");

        var edges = diagram.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithIf#dec" &&
                                    e.GetProperty("targetId").GetString() == "ModuleCfg.WithIf#then" &&
                                    e.GetProperty("label").GetString() == "True");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithIf#dec" &&
                                    e.GetProperty("targetId").GetString() == "ModuleCfg.WithIf#else" &&
                                    e.GetProperty("label").GetString() == "False");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithIf#then" &&
                                    e.GetProperty("targetId").GetString()?.StartsWith("ModuleCfg.WithIf#call:ModuleCfg.HelperA") == true &&
                                    e.GetProperty("metadata").GetProperty("code.edge").GetString() == "flow");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithIf#else" &&
                                    e.GetProperty("targetId").GetString()?.StartsWith("ModuleCfg.WithIf#call:ModuleCfg.HelperB") == true &&
                                    e.GetProperty("metadata").GetProperty("code.edge").GetString() == "flow");
    }

    [Fact]
    public void ProcCfgEmitsLoopNodes()
    {
        using var diagram = GenerateDiagram("cfg_shapes", "proc-cfg");
        var nodes = diagram.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithLoop#loop");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString()?.StartsWith("ModuleCfg.WithLoop#call:ModuleCfg.HelperB") == true);
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleCfg.WithLoop#end");

        var edges = diagram.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleCfg.WithLoop#loop" &&
                                    e.GetProperty("targetId").GetString()?.StartsWith("ModuleCfg.WithLoop#call:ModuleCfg.HelperB") == true &&
                                    e.GetProperty("label").GetString() == "seq");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString()?.StartsWith("ModuleCfg.WithLoop#call:ModuleCfg.HelperB") == true &&
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
        Assert.Contains(nodes, n => n.GetProperty("id").GetString()?.StartsWith("ModuleNested.LoopWithBranch#call:ModuleNested.HelperEven") == true);
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleNested.LoopWithBranch#else");
        Assert.Contains(nodes, n => n.GetProperty("id").GetString()?.StartsWith("ModuleNested.LoopWithBranch#call:ModuleNested.HelperOdd") == true);
        Assert.Contains(nodes, n => n.GetProperty("id").GetString() == "ModuleNested.LoopWithBranch#end");

        var edges = diagram.RootElement.GetProperty("edges").EnumerateArray().ToList();
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#loop" &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#dec" &&
                                    e.GetProperty("label").GetString() == "iter");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#dec" &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#then" &&
                                    e.GetProperty("label").GetString() == "True");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString()?.StartsWith("ModuleNested.LoopWithBranch#call:ModuleNested.HelperEven") == true &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#loop" &&
                                    e.GetProperty("label").GetString() == "back");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#dec" &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#else" &&
                                    e.GetProperty("label").GetString() == "False");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString() == "ModuleNested.LoopWithBranch#else" &&
                                    e.GetProperty("targetId").GetString()?.StartsWith("ModuleNested.LoopWithBranch#call:ModuleNested.HelperOdd") == true &&
                                    e.GetProperty("label").GetString() == "seq");
        Assert.Contains(edges, e => e.GetProperty("sourceId").GetString()?.StartsWith("ModuleNested.LoopWithBranch#call:ModuleNested.HelperOdd") == true &&
                                    e.GetProperty("targetId").GetString() == "ModuleNested.LoopWithBranch#loop" &&
                                    e.GetProperty("label").GetString() == "back");
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
    public void GoldenIrFixturesValidateAgainstSchema()
    {
        var fixturesDir = Path.Combine(RepoRoot, "tests", "fixtures", "ir");
        foreach (var path in Directory.EnumerateFiles(fixturesDir, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            ValidateIrSchema(doc.RootElement, Path.GetFileName(path));
        }
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
    public void Vba2JsonSortsModulesAndProcedures()
    {
        using var ir = GenerateIrDocument("alias_and_chain");
        var modules = ir.RootElement.GetProperty("project").GetProperty("modules").EnumerateArray().ToList();
        var moduleNames = modules.Select(m => m.GetProperty("name").GetString()).ToList();
        var sortedModules = moduleNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sortedModules, moduleNames);

        foreach (var module in modules)
        {
            var procedures = module.GetProperty("procedures").EnumerateArray().ToList();
            var procNames = procedures.Select(p => p.GetProperty("name").GetString()).ToList();
            var sortedProcs = procNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.Equal(sortedProcs, procNames);
        }
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
            Assert.False(module.TryGetProperty("metrics", out _), "module metrics should be omitted unless --infer-metrics is used.");

            var procedures = module.GetProperty("procedures");
            Assert.True(procedures.GetArrayLength() > 0);
            foreach (var procedure in procedures.EnumerateArray())
            {
                Assert.False(string.IsNullOrWhiteSpace(procedure.GetProperty("id").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(procedure.GetProperty("name").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(procedure.GetProperty("kind").GetString()));
                Assert.False(procedure.TryGetProperty("metrics", out _), "procedure metrics should be omitted unless --infer-metrics is used.");
            }
        }
    }

    [Fact]
    public void Vba2JsonInferMetricsIncludesLines()
    {
        using var ir = GenerateIrDocument("hello_world", "--infer-metrics");
        var modules = ir.RootElement.GetProperty("project").GetProperty("modules").EnumerateArray().ToList();
        Assert.NotEmpty(modules);

        var module = modules.Single();
        var moduleMetrics = module.GetProperty("metrics");
        Assert.True(moduleMetrics.TryGetProperty("lines", out var moduleLines));
        Assert.True(moduleLines.GetInt32() > 0);

        var procedure = module.GetProperty("procedures").EnumerateArray().Single();
        var procMetrics = procedure.GetProperty("metrics");
        Assert.True(procMetrics.TryGetProperty("lines", out var procLines));
        Assert.True(procLines.GetInt32() > 0);
    }

    [Fact]
    public void Vba2JsonSupportsGlobPatterns()
    {
        var irPath = Path.Combine(Path.GetTempPath(), $"vdg_ir_glob_{Guid.NewGuid():N}.json");
        try
        {
            var fixtureDir = Path.Combine(RepoRoot, "tests", "fixtures", "vba", "cross_module_calls");
            var module2Abs = Path.Combine(fixtureDir, "Module2.bas");
            RunCli("vba2json", "--in", fixtureDir, "--glob", "Module*.bas", "--glob", module2Abs, "--out", irPath);

            using var ir = JsonDocument.Parse(File.ReadAllText(irPath));
            var modules = ir.RootElement.GetProperty("project").GetProperty("modules").EnumerateArray().ToList();
            Assert.Equal(2, modules.Count);
            Assert.Contains(modules, m => m.GetProperty("id").GetString() == "Module1");
            Assert.Contains(modules, m => m.GetProperty("id").GetString() == "Module2");
        }
        finally
        {
            if (File.Exists(irPath)) File.Delete(irPath);
        }
    }

    [Fact]
    public void Vba2JsonStreamsToStdoutWhenOutOmitted()
    {
        var fixtureDir = Path.Combine(RepoRoot, "tests", "fixtures", "vba", "hello_world");
        var output = RunCli("vba2json", "--in", fixtureDir);
        Assert.False(string.IsNullOrWhiteSpace(output));

        using var ir = JsonDocument.Parse(output);
        var module = ir.RootElement.GetProperty("project").GetProperty("modules").EnumerateArray().Single();
        Assert.False(module.TryGetProperty("metrics", out _));
    }

    [Fact]
    public void Vba2JsonHonorsRootForFilePaths()
    {
        using var ir = GenerateIrDocument("cross_module_calls", "--root", Path.Combine(RepoRoot, "tests"));
        var modules = ir.RootElement.GetProperty("project").GetProperty("modules").EnumerateArray().ToList();
        Assert.Equal(2, modules.Count);
        Assert.All(modules, module =>
        {
            var file = module.GetProperty("file").GetString();
            Assert.StartsWith("fixtures/", file, StringComparison.OrdinalIgnoreCase);
        });
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

    private static void ValidateIrSchema(JsonElement root, string fixtureName)
    {
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("0.1", RequireString(root, "irSchemaVersion", fixtureName));

        if (root.TryGetProperty("generator", out var generator))
        {
            Assert.Equal(JsonValueKind.Object, generator.ValueKind);
            RequireString(generator, "name", $"{fixtureName} generator");
            RequireString(generator, "version", $"{fixtureName} generator");
        }

        var project = RequireObject(root, "project", fixtureName);
        RequireString(project, "name", $"{fixtureName} project");
        ExpectOptionalString(project, "version", $"{fixtureName} project");

        var modules = RequireArray(project, "modules", $"{fixtureName} project");
        Assert.True(modules.GetArrayLength() > 0, $"{fixtureName} project.modules must contain at least one module");

        var seenModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenModuleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (module, index) in modules.EnumerateArray().Select((m, i) => (m, i)))
        {
            ValidateModule(module, fixtureName, index, seenModuleIds, seenModuleNames);
        }
    }

    private static void ValidateModule(
        JsonElement module,
        string fixtureName,
        int index,
        HashSet<string> seenModuleIds,
        HashSet<string> seenModuleNames)
    {
        Assert.Equal(JsonValueKind.Object, module.ValueKind);
        var context = $"{fixtureName} module[{index}]";
        var moduleId = RequireString(module, "id", context);
        Assert.True(seenModuleIds.Add(moduleId), $"{context}: duplicate module id '{moduleId}'");
        var moduleName = RequireString(module, "name", context);
        Assert.True(seenModuleNames.Add(moduleName), $"{context}: duplicate module name '{moduleName}'");
        var kind = RequireString(module, "kind", context);
        Assert.Contains(kind, ModuleKinds);
        ExpectOptionalString(module, "file", context);
        if (module.TryGetProperty("metrics", out var moduleMetrics))
        {
            Assert.Equal(JsonValueKind.Object, moduleMetrics.ValueKind);
            if (moduleMetrics.TryGetProperty("lines", out var moduleLines))
            {
                Assert.Equal(JsonValueKind.Number, moduleLines.ValueKind);
                Assert.True(moduleLines.GetInt32() >= 0, $"{context}.metrics.lines must be >= 0");
            }
            if (moduleMetrics.TryGetProperty("cyclomatic", out var moduleCyclomatic))
            {
                Assert.Equal(JsonValueKind.Number, moduleCyclomatic.ValueKind);
                Assert.True(moduleCyclomatic.GetInt32() >= 0, $"{context}.metrics.cyclomatic must be >= 0");
            }
        }

        if (module.TryGetProperty("attributes", out var attributes))
        {
            Assert.Equal(JsonValueKind.Array, attributes.ValueKind);
            foreach (var attribute in attributes.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.String, attribute.ValueKind);
            }
        }

        var procedures = RequireArray(module, "procedures", context);
        Assert.True(procedures.GetArrayLength() > 0, $"{context}: procedures must contain at least one entry");
        var seenProcedureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (procedure, procIndex) in procedures.EnumerateArray().Select((p, i) => (p, i)))
        {
            ValidateProcedure(procedure, fixtureName, moduleName, procIndex, seenProcedureIds);
        }
    }

    private static void ValidateProcedure(
        JsonElement procedure,
        string fixtureName,
        string moduleName,
        int index,
        HashSet<string> seenProcedureIds)
    {
        Assert.Equal(JsonValueKind.Object, procedure.ValueKind);
        var context = $"{fixtureName} {moduleName}.procedures[{index}]";
        var id = RequireString(procedure, "id", context);
        Assert.StartsWith(moduleName + ".", id, StringComparison.Ordinal);
        Assert.True(seenProcedureIds.Add(id), $"{context}: duplicate procedure id '{id}'");
        RequireString(procedure, "name", context);
        var kind = RequireString(procedure, "kind", context);
        Assert.Contains(kind, ProcedureKinds);

        if (procedure.TryGetProperty("access", out var access))
        {
            Assert.Equal(JsonValueKind.String, access.ValueKind);
            Assert.Contains(access.GetString(), AccessModifiers);
        }

        if (procedure.TryGetProperty("static", out var isStatic))
        {
            Assert.True(
                isStatic.ValueKind is JsonValueKind.True or JsonValueKind.False,
                $"{context}: 'static' must be boolean when present");
        }

        if (procedure.TryGetProperty("params", out var parameters))
        {
            Assert.Equal(JsonValueKind.Array, parameters.ValueKind);
            foreach (var (parameter, paramIndex) in parameters.EnumerateArray().Select((p, i) => (p, i)))
            {
                ValidateParameter(parameter, $"{context}.params[{paramIndex}]");
            }
        }

        if (procedure.TryGetProperty("returns", out var returns))
        {
            if (returns.ValueKind == JsonValueKind.String)
            {
                Assert.False(string.IsNullOrWhiteSpace(returns.GetString()), $"{context}: returns must not be empty");
            }
            else if (returns.ValueKind == JsonValueKind.Object)
            {
                ExpectOptionalString(returns, "type", $"{context}.returns");
            }
            else
            {
                Assert.True(false, $"{context}: returns must be string or object when present");
            }
        }

        var locs = RequireObject(procedure, "locs", context);
        RequireString(locs, "file", $"{context}.locs");
        RequireNumber(locs, "startLine", $"{context}.locs");
        RequireNumber(locs, "endLine", $"{context}.locs");

        if (procedure.TryGetProperty("calls", out var calls))
        {
            Assert.Equal(JsonValueKind.Array, calls.ValueKind);
            foreach (var (call, callIndex) in calls.EnumerateArray().Select((c, i) => (c, i)))
            {
                ValidateCall(call, $"{context}.calls[{callIndex}]");
            }
        }

        if (procedure.TryGetProperty("metrics", out var metrics))
        {
            Assert.Equal(JsonValueKind.Object, metrics.ValueKind);
            if (metrics.TryGetProperty("lines", out var lines))
            {
                Assert.Equal(JsonValueKind.Number, lines.ValueKind);
                Assert.True(lines.GetInt32() >= 0, $"{context}.metrics.lines must be >= 0");
            }
            if (metrics.TryGetProperty("cyclomatic", out var cyclomatic))
            {
                Assert.Equal(JsonValueKind.Number, cyclomatic.ValueKind);
                Assert.True(cyclomatic.GetInt32() >= 0, $"{context}.metrics.cyclomatic must be >= 0");
            }
        }

        if (procedure.TryGetProperty("tags", out var tags))
        {
            Assert.Equal(JsonValueKind.Array, tags.ValueKind);
            foreach (var tag in tags.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.String, tag.ValueKind);
            }
        }
    }

    private static void ValidateParameter(JsonElement parameter, string context)
    {
        Assert.Equal(JsonValueKind.Object, parameter.ValueKind);
        RequireString(parameter, "name", context);
        ExpectOptionalString(parameter, "type", context);
        if (parameter.TryGetProperty("byRef", out var byRef))
        {
            Assert.True(
                byRef.ValueKind is JsonValueKind.True or JsonValueKind.False,
                $"{context}: 'byRef' must be boolean when present");
        }
    }

    private static void ValidateCall(JsonElement call, string context)
    {
        Assert.Equal(JsonValueKind.Object, call.ValueKind);
        RequireString(call, "target", context);
        if (call.TryGetProperty("isDynamic", out var isDynamic))
        {
            Assert.True(
                isDynamic.ValueKind is JsonValueKind.True or JsonValueKind.False,
                $"{context}: 'isDynamic' must be boolean when present");
        }

        if (call.TryGetProperty("site", out var site))
        {
            Assert.Equal(JsonValueKind.Object, site.ValueKind);
            RequireString(site, "module", $"{context}.site");
            RequireString(site, "file", $"{context}.site");
            RequireNumber(site, "line", $"{context}.site");
        }
    }

    private static JsonElement RequireObject(JsonElement element, string propertyName, string context)
    {
        Assert.True(element.TryGetProperty(propertyName, out var property), $"{context}: missing '{propertyName}'");
        Assert.Equal(JsonValueKind.Object, property.ValueKind);
        return property;
    }

    private static JsonElement RequireArray(JsonElement element, string propertyName, string context)
    {
        Assert.True(element.TryGetProperty(propertyName, out var property), $"{context}: missing '{propertyName}'");
        Assert.Equal(JsonValueKind.Array, property.ValueKind);
        return property;
    }

    private static string RequireString(JsonElement element, string propertyName, string context)
    {
        Assert.True(element.TryGetProperty(propertyName, out var property), $"{context}: missing '{propertyName}'");
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        var value = property.GetString();
        Assert.False(string.IsNullOrWhiteSpace(value), $"{context}: '{propertyName}' must not be empty");
        return value!;
    }

    private static int RequireNumber(JsonElement element, string propertyName, string context)
    {
        Assert.True(element.TryGetProperty(propertyName, out var property), $"{context}: missing '{propertyName}'");
        Assert.Equal(JsonValueKind.Number, property.ValueKind);
        return property.GetInt32();
    }

    private static void ExpectOptionalString(JsonElement element, string propertyName, string context)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            Assert.Equal(JsonValueKind.String, property.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(property.GetString()), $"{context}: '{propertyName}' must not be empty when provided");
        }
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

    private static JsonDocument GenerateIrDocument(string fixtureName, params string[] extraArgs) =>
        JsonDocument.Parse(GenerateIrJson(fixtureName, extraArgs));

    private static string GenerateIrJson(string fixtureName, params string[] extraArgs)
    {
        var irPath = Path.Combine(Path.GetTempPath(), $"vdg_ir_{Guid.NewGuid():N}.json");
        try
        {
            var fixtureDir = Path.Combine(RepoRoot, "tests", "fixtures", "vba", fixtureName);
            var cliArgs = new List<string> { "vba2json", "--in", fixtureDir, "--out", irPath };
            if (extraArgs is { Length: > 0 }) cliArgs.AddRange(extraArgs);
            RunCli(cliArgs.ToArray());
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

