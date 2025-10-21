using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VDG.VBA.CLI;

internal static class ExitCodes
{
    public const int Ok = 0;
    public const int InvalidInput = 65;
    public const int InternalError = 70;
}

internal sealed class UsageException(string message) : Exception(message);

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly CallgraphDiagnosticsSettings CallgraphDiagnostics = BuildCallgraphDiagnostics();

    private static CallgraphDiagnosticsSettings BuildCallgraphDiagnostics()
    {
        static int ParseThreshold(string? value, int fallback)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                return parsed;
            }
            return fallback;
        }

        var threshold = ParseThreshold(Environment.GetEnvironmentVariable("VDG_CALLGRAPH_FANOUT_THRESHOLD"), 30);
        var selfSeverity = Environment.GetEnvironmentVariable("VDG_CALLGRAPH_SELF_CALL_SEVERITY");
        var fanOutSeverity = Environment.GetEnvironmentVariable("VDG_CALLGRAPH_FANOUT_SEVERITY");

        return new CallgraphDiagnosticsSettings(
            threshold,
            string.IsNullOrWhiteSpace(selfSeverity) ? "warning" : selfSeverity!,
            string.IsNullOrWhiteSpace(fanOutSeverity) ? "warning" : fanOutSeverity!
        );
    }

    private readonly record struct CallgraphDiagnosticsSettings(int HighFanOutThreshold, string SelfCallSeverity, string FanOutSeverity);

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintUsage();
                return ExitCodes.Ok;
            }

            var cmd = args[0].ToLowerInvariant();
            return cmd switch
            {
                "vba2json" => RunVba2Json(args.Skip(1).ToArray()),
                "ir2diagram" => RunIr2Diagram(args.Skip(1).ToArray()),
                "render" => RunRender(args.Skip(1).ToArray()),
                _ => throw new UsageException($"Unknown command '{args[0]}'.")
            };
        }
        catch (UsageException uex)
        {
            Console.Error.WriteLine($"usage: {uex.Message}");
            PrintUsage();
            return ExitCodes.InvalidInput;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return ExitCodes.InternalError;
        }
    }

    private static bool IsHelp(string s) => string.Equals(s, "-h", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(s, "--help", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(s, "help", StringComparison.OrdinalIgnoreCase);

    private static void PrintUsage()
    {
        Console.Error.WriteLine("VDG.VBA.CLI");
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  vba2json --in <folder> [--glob <pattern> ...] [--out <ir.json>] [--project-name <name>] [--root <path>] [--infer-metrics]");
        Console.Error.WriteLine("    Extracts VBA sources into IR JSON (schema v0.2).");
        Console.Error.WriteLine("    --glob <pattern>       Limit inputs using * / ? wildcards relative to --in (repeatable).");
        Console.Error.WriteLine("    --root <path>          Base path for emitted module file paths (defaults to --in).");
        Console.Error.WriteLine("    --infer-metrics        Include simple line-count metrics in the IR payload.");
        Console.Error.WriteLine("    Example: dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/cross_module_calls --out out/project.ir.json");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  ir2diagram --in <ir.json> [--out <diagram.json>] [--mode <callgraph|module-structure|module-callmap|event-wiring|proc-cfg>] [--include-unknown] [--timeout <ms>] [--strict-validate] [--summary-log <csv>]");
        Console.Error.WriteLine("    Converts IR JSON into Diagram JSON (schema 1.2). Defaults to callgraph mode with tiered lanes.");
        Console.Error.WriteLine("    --include-unknown      Emit sentinel edges for '~unknown' dynamic calls.");
        Console.Error.WriteLine("    --timeout <ms>         Abort conversion if processing exceeds the provided timeout in milliseconds.");
        Console.Error.WriteLine("    --strict-validate      Enforce strict IR invariants before conversion (fails fast on issues).");
        Console.Error.WriteLine("    --summary-log <csv>    Write hyperlink summary (Name/File/Module/Lines) to the specified CSV alongside console output.");
        Console.Error.WriteLine("    Example: dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/project.ir.json --out out/project.callgraph.json");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  render --in <folder> --out <diagram.vsdx> [--mode <callgraph|module-structure|module-callmap>] [--cli <VDG.CLI.exe>] [--diagram-json <path>] [--diag-json <path>] [--summary-log <csv>]");
        Console.Error.WriteLine("    Runs vba2json + ir2diagram and renders with VDG.CLI.");
        Console.Error.WriteLine("    --diagram-json         Keep the intermediate Diagram JSON instead of using a temp path.");
        Console.Error.WriteLine("    --diag-json            Emit diagnostics JSON from VDG.CLI to the provided path.");
        Console.Error.WriteLine("    --summary-log <csv>    Mirror the hyperlink summary to a CSV when rendering end-to-end.");
        Console.Error.WriteLine("    --cli <VDG.CLI.exe>    Provide an explicit path to VDG.CLI (defaults to env or discovery).");
        Console.Error.WriteLine("    Example: dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/cross_module_calls --out out/project.vsdx");
    }

    private static int RunVba2Json(string[] args)
    {
        string? input = null;
        string? output = null;
        string? projectName = null;
        string? rootOverride = null;
        var globPatterns = new List<string>();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bas", ".cls", ".frm" };
        bool inferMetrics = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--in", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) throw new UsageException("--in requires a folder path.");
                input = args[++i];
            }
            else if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) throw new UsageException("--out requires a file path.");
                output = args[++i];
            }
            else if (string.Equals(a, "--project-name", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) throw new UsageException("--project-name requires a value.");
                projectName = args[++i];
            }
            else if (string.Equals(a, "--glob", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) throw new UsageException("--glob requires a pattern.");
                globPatterns.Add(args[++i]);
            }
            else if (string.Equals(a, "--infer-metrics", StringComparison.OrdinalIgnoreCase))
            {
                inferMetrics = true;
            }
            else if (string.Equals(a, "--root", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) throw new UsageException("--root requires a path.");
                rootOverride = args[++i];
            }
            else throw new UsageException($"Unknown option '{a}' for vba2json.");
        }

        if (string.IsNullOrWhiteSpace(input)) throw new UsageException("vba2json requires --in <folder>.");
        var inputFolder = Path.GetFullPath(input!);
        if (!Directory.Exists(inputFolder)) throw new UsageException($"Input folder not found: {inputFolder}");

        var discoveredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (globPatterns.Count == 0)
        {
            foreach (var file in Directory.EnumerateFiles(inputFolder, "*.*", SearchOption.AllDirectories))
            {
                if (allowedExtensions.Contains(Path.GetExtension(file)))
                    discoveredFiles.Add(Path.GetFullPath(file));
            }
        }
        else
        {
            var relativeCandidates = Directory.EnumerateFiles(inputFolder, "*.*", SearchOption.AllDirectories)
                                              .Where(f => allowedExtensions.Contains(Path.GetExtension(f)))
                                              .Select(Path.GetFullPath)
                                              .ToArray();

            foreach (var pattern in globPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                if (Path.IsPathRooted(pattern))
                {
                    var baseDir = Path.GetDirectoryName(pattern);
                    var searchPattern = Path.GetFileName(pattern);
                    if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Directory.GetCurrentDirectory();
                    if (string.IsNullOrWhiteSpace(searchPattern)) searchPattern = "*.*";
                    if (!Directory.Exists(baseDir)) continue;

                    foreach (var match in Directory.EnumerateFiles(baseDir!, searchPattern!, SearchOption.TopDirectoryOnly))
                    {
                        if (allowedExtensions.Contains(Path.GetExtension(match)))
                            discoveredFiles.Add(Path.GetFullPath(match));
                    }
                }
                else
                {
                    var normalized = pattern.Replace('\\', '/');
                    foreach (var candidate in relativeCandidates)
                    {
                        var relative = Path.GetRelativePath(inputFolder, candidate).Replace('\\', '/');
                        if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(normalized, relative, ignoreCase: true) ||
                            System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(normalized, Path.GetFileName(candidate), ignoreCase: true))
                        {
                            discoveredFiles.Add(candidate);
                        }
                    }
                }
            }
        }

        if (discoveredFiles.Count == 0)
            throw new UsageException(globPatterns.Count > 0
                ? $"No files matched the provided --glob patterns under '{inputFolder}'."
                : $"No .bas/.cls/.frm files found under '{inputFolder}'.");

        var rootDir = !string.IsNullOrWhiteSpace(rootOverride)
            ? Path.GetFullPath(rootOverride!)
            : inputFolder;

        // First pass: gather module names/kinds
        var firstPass = new List<(string file, string[] lines, string name, string kind)>();
        var moduleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in discoveredFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var lines = LoadLogicalLines(file);
            var modName = TryMatch(lines, "Attribute\\s+VB_Name\\s*=\\s*\\\"([^\\\"]+)\\\"") ?? Path.GetFileNameWithoutExtension(file);
            if (!moduleSet.Add(modName))
                throw new UsageException($"Duplicate module name detected: '{modName}'. Ensure module names are unique across inputs.");
            var kind = KindFromExt(Path.GetExtension(file));
            firstPass.Add((file, lines, modName, kind));
        }

        // Second pass: parse with symbol table awareness
        var returnTypeMap = BuildReturnTypeMap(firstPass);
        var modules = new List<ModuleIr>();
        foreach (var it in firstPass)
        {
            var relativePath = MakeRelative(rootDir, it.file);
            var procs = ParseProcedures(it.lines, it.name, relativePath, moduleSet, returnTypeMap, inferMetrics);
            var orderedProcedures = procs.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                                         .ThenBy(p => p.Id, StringComparer.Ordinal)
                                         .ToList();
            MetricsIr? moduleMetrics = null;
            if (inferMetrics)
            {
                var slocSum = orderedProcedures.Sum(p => p.Metrics?.Sloc ?? 0);
                var cyclomaticSum = orderedProcedures.Sum(p => p.Metrics?.Cyclomatic ?? 0);
                moduleMetrics = new MetricsIr
                {
                    Lines = it.lines.Length,
                    Sloc = slocSum,
                    Cyclomatic = cyclomaticSum
                };
            }
            modules.Add(new ModuleIr
            {
                Id = it.name,
                Name = it.name,
                Kind = it.kind,
                File = relativePath,
                Source = new SourceIr { File = relativePath, Module = it.name, Line = 1 },
                Metrics = moduleMetrics,
                Procedures = orderedProcedures
            });
        }

        modules = modules
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Id, StringComparer.Ordinal)
            .ToList();

        var ir = new IrRoot
        {
            IrSchemaVersion = "0.2",
            Generator = new() { Name = "vba2json", Version = "0.2.0" },
            Project = new()
            {
                Name = string.IsNullOrWhiteSpace(projectName) ? Path.GetFileName(inputFolder) : projectName!,
                Modules = modules
            }
        };

        var json = JsonSerializer.Serialize(ir, JsonOpts);
        if (string.IsNullOrWhiteSpace(output)) Console.WriteLine(json);
        else
        {
            var outPath = Path.GetFullPath(output!);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            File.WriteAllText(outPath, json);
        }
        return ExitCodes.Ok;
    }

    private static int RunIr2Diagram(string[] args)
    {
        string? input = null; string? output = null; string mode = "callgraph"; bool includeUnknown = false; int? timeoutMs = null; bool strictValidate = false; string? summaryLogPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--in", StringComparison.OrdinalIgnoreCase))
            { if (i + 1 >= args.Length) throw new UsageException("--in requires a json path."); input = args[++i]; }
            else if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase))
            { if (i + 1 >= args.Length) throw new UsageException("--out requires a json path."); output = args[++i]; }
            else if (string.Equals(a, "--mode", StringComparison.OrdinalIgnoreCase))
            { if (i + 1 >= args.Length) throw new UsageException("--mode requires callgraph|module-structure|module-callmap."); mode = args[++i]; }
            else if (string.Equals(a, "--include-unknown", StringComparison.OrdinalIgnoreCase))
            { includeUnknown = true; }
            else if (string.Equals(a, "--timeout", StringComparison.OrdinalIgnoreCase))
            { if (i + 1 >= args.Length) throw new UsageException("--timeout requires milliseconds value."); if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var t)) throw new UsageException("--timeout must be an integer (milliseconds)."); timeoutMs = t; }
            else if (string.Equals(a, "--strict-validate", StringComparison.OrdinalIgnoreCase))
            { strictValidate = true; }
            else if (string.Equals(a, "--summary-log", StringComparison.OrdinalIgnoreCase))
            { if (i + 1 >= args.Length) throw new UsageException("--summary-log requires a file path."); summaryLogPath = args[++i]; }
            else throw new UsageException($"Unknown option '{a}' for ir2diagram.");
        }
        if (string.IsNullOrWhiteSpace(input)) throw new UsageException("ir2diagram requires --in <ir.json>.");
        if (!File.Exists(input!)) throw new UsageException($"IR file not found: {input}");

        IrRoot root;
        try
        {
            root = JsonSerializer.Deserialize<IrRoot>(File.ReadAllText(input!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new UsageException("Invalid IR JSON.");
        }
        catch (JsonException)
        {
            throw new UsageException("Invalid IR JSON.");
        }

        var tiers = new[] { "Forms", "Sheets", "Classes", "Modules" };

        Dictionary<string, string> BuildModuleMetadata(ModuleIr module)
        {
            var meta = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(module.Name)) meta["code.module"] = module.Name;
            if (!string.IsNullOrWhiteSpace(module.Kind)) meta["code.module.kind"] = module.Kind!;
            if (!string.IsNullOrWhiteSpace(module.File)) meta["code.file"] = module.File!;
            if (module.Source is { } source)
            {
                if (!string.IsNullOrWhiteSpace(source.File)) meta["code.source.file"] = source.File;
                if (!string.IsNullOrWhiteSpace(source.Module)) meta["code.source.module"] = source.Module!;
                if (source.Line.HasValue && source.Line.Value > 0) meta["code.source.line"] = source.Line.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (module.Metrics is { } metrics)
            {
                if (metrics.Lines.HasValue) meta["code.metrics.lines"] = metrics.Lines.Value.ToString(CultureInfo.InvariantCulture);
                if (metrics.Sloc.HasValue) meta["code.metrics.sloc"] = metrics.Sloc.Value.ToString(CultureInfo.InvariantCulture);
                if (metrics.Cyclomatic.HasValue) meta["code.metrics.cyclomatic"] = metrics.Cyclomatic.Value.ToString(CultureInfo.InvariantCulture);
            }
            return meta;
        }

        Dictionary<string, string> BuildProcedureMetadata(ModuleIr module, ProcedureIr procedure)
        {
            var meta = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(module.Name)) meta["code.module"] = module.Name;
            if (!string.IsNullOrWhiteSpace(procedure.Name)) meta["code.proc"] = procedure.Name;
            if (!string.IsNullOrWhiteSpace(procedure.Kind)) meta["code.kind"] = procedure.Kind!;
            if (!string.IsNullOrWhiteSpace(procedure.Access)) meta["code.access"] = procedure.Access!;
            if (procedure.Locs is { } locs)
            {
                if (!string.IsNullOrWhiteSpace(locs.File)) meta["code.locs.file"] = locs.File;
                if (locs.StartLine > 0) meta["code.locs.startLine"] = locs.StartLine.ToString(CultureInfo.InvariantCulture);
                if (locs.EndLine > 0) meta["code.locs.endLine"] = locs.EndLine.ToString(CultureInfo.InvariantCulture);
            }
            if (procedure.Source is { } source)
            {
                if (!string.IsNullOrWhiteSpace(source.File)) meta["code.source.file"] = source.File;
                if (!string.IsNullOrWhiteSpace(source.Module)) meta["code.source.module"] = source.Module!;
                if (source.Line.HasValue && source.Line.Value > 0) meta["code.source.line"] = source.Line.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (procedure.Metrics is { } metrics)
            {
                if (metrics.Lines.HasValue) meta["code.metrics.lines"] = metrics.Lines.Value.ToString(CultureInfo.InvariantCulture);
                if (metrics.Sloc.HasValue) meta["code.metrics.sloc"] = metrics.Sloc.Value.ToString(CultureInfo.InvariantCulture);
                if (metrics.Cyclomatic.HasValue) meta["code.metrics.cyclomatic"] = metrics.Cyclomatic.Value.ToString(CultureInfo.InvariantCulture);
            }
            return meta;
        }
        if (strictValidate)
        {
            string[] allowedKinds = new[] { "Module", "Class", "Form" };
            if (root.Project is null) throw new UsageException("IR missing 'project'.");
            if (root.Project.Modules is null || root.Project.Modules.Count == 0) throw new UsageException("IR contains no modules.");
            int procCount = 0;
            foreach (var m in root.Project.Modules)
            {
                if (string.IsNullOrWhiteSpace(m.Id)) throw new UsageException("Module missing id.");
                if (string.IsNullOrWhiteSpace(m.Name)) throw new UsageException($"Module '{m.Id}' missing name.");
                if (string.IsNullOrWhiteSpace(m.Kind) || !allowedKinds.Contains(m.Kind, StringComparer.OrdinalIgnoreCase)) throw new UsageException($"Module '{m.Id}' has invalid kind '{m.Kind}'.");
                if (string.IsNullOrWhiteSpace(m.File)) throw new UsageException($"Module '{m.Id}' missing file path.");
                if (m.Procedures is null || m.Procedures.Count == 0) throw new UsageException($"Module '{m.Id}' contains no procedures.");
                foreach (var p in m.Procedures)
                {
                    procCount++;
                    if (string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Name)) throw new UsageException($"Procedure missing id/name in module '{m.Id}'.");
                    if (p.Locs is null || string.IsNullOrWhiteSpace(p.Locs.File) || p.Locs.StartLine <= 0 || p.Locs.EndLine < p.Locs.StartLine)
                        throw new UsageException($"Procedure '{p.Id}' has invalid locs.");
                    foreach (var c in p.Calls ?? new())
                    {
                        if (string.IsNullOrWhiteSpace(c.Target)) throw new UsageException($"Call in '{p.Id}' missing target.");
                        if (string.Equals(c.Target, "~unknown", StringComparison.Ordinal) && !c.IsDynamic)
                            throw new UsageException($"Call in '{p.Id}' has '~unknown' target but isDynamic=false.");
                        if (c.Site is null || string.IsNullOrWhiteSpace(c.Site.Module) || string.IsNullOrWhiteSpace(c.Site.File) || c.Site.Line <= 0)
                            throw new UsageException($"Call in '{p.Id}' missing site information.");
                    }
                }
            }
            if (procCount == 0) throw new UsageException("IR contains no procedures.");
        }
        // Basic IR sanity: require at least one module and one procedure overall
        var incomingModules = root.Project?.Modules ?? new List<ModuleIr>();
        if (incomingModules.Count == 0)
            throw new UsageException("IR contains no modules.");
        if (!incomingModules.Any(m => (m.Procedures?.Count ?? 0) > 0))
            throw new UsageException("IR contains no procedures.");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        void CheckTimeout()
        {
            if (timeoutMs.HasValue && sw.ElapsedMilliseconds > timeoutMs.Value)
            {
                throw new Exception($"ir2diagram timeout exceeded after {sw.ElapsedMilliseconds} ms (limit {timeoutMs.Value} ms)");
            }
        }
        static bool LooksLikeSheetName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.StartsWith("Sheet", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("Chart", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("ThisWorkbook", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("ThisDocument", StringComparison.OrdinalIgnoreCase);
        }

        string TierFor(ModuleIr module)
        {
            if (module == null) return "Modules";
            if (string.Equals(module.Kind, "Form", StringComparison.OrdinalIgnoreCase)) return "Forms";

            if (string.Equals(module.Kind, "Class", StringComparison.OrdinalIgnoreCase))
            {
                var candidates = new List<string?>();
                candidates.Add(module.Name);
                candidates.Add(module.Id);
                if (!string.IsNullOrWhiteSpace(module.File))
                {
                    try { candidates.Add(Path.GetFileNameWithoutExtension(module.File)); }
                    catch { candidates.Add(module.File); }
                }
                if (candidates.Any(LooksLikeSheetName))
                {
                    return "Sheets";
                }
                return "Classes";
            }

            return "Modules";
        }

        var nodes = new List<object>();
        var edges = new List<object>();
        var containers = new List<object>();

        int totalModules = 0, totalProcedures = 0, dynamicSkipped = 0, dynamicIncluded = 0;
        var diagConfig = CallgraphDiagnostics;
        bool progressEnabled = string.Equals(mode, "callgraph", StringComparison.OrdinalIgnoreCase);

        var warnings = new List<(string Severity, string Message)>();
        var warningKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var progressStopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan lastProgressAt = TimeSpan.Zero;
        int lastProgressModules = 0, lastProgressProcedures = 0, lastProgressEdges = 0;
        int progressEmits = 0;
        TimeSpan lastProgressElapsed = TimeSpan.Zero;

        void AddWarning(string severity, string message)
        {
            var key = $"{severity}:{message}";
            if (warningKeys.Add(key))
            {
                warnings.Add((severity, message));
            }
        }

        void EmitProgress(bool force = false)
        {
            if (!progressEnabled)
            {
                return;
            }
            if (!force)
            {
                bool moduleStep = totalModules - lastProgressModules >= 5 && totalModules > lastProgressModules;
                bool procedureStep = totalProcedures - lastProgressProcedures >= 25 && totalProcedures > lastProgressProcedures;
                bool edgeStep = edges.Count - lastProgressEdges >= 100 && edges.Count > lastProgressEdges;
                bool timeStep = progressStopwatch.Elapsed - lastProgressAt >= TimeSpan.FromSeconds(1);
                if (!(moduleStep || procedureStep || edgeStep || timeStep))
                {
                    return;
                }
            }

            lastProgressAt = progressStopwatch.Elapsed;
            lastProgressModules = totalModules;
            lastProgressProcedures = totalProcedures;
            lastProgressEdges = edges.Count;

            Console.WriteLine($"modules:{totalModules} procedures:{totalProcedures} edges:{edges.Count} (progress)");
            progressEmits++;
            lastProgressElapsed = progressStopwatch.Elapsed;
        }

        var orderedModules = (root.Project?.Modules ?? new List<ModuleIr>())
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var m in orderedModules)
        {
            CheckTimeout();
            var tier = TierFor(m);
            var moduleLabel = string.IsNullOrWhiteSpace(m.Name) ? m.Id : m.Name;
            var moduleMetadata = BuildModuleMetadata(m);
            containers.Add(new { id = m.Id, label = moduleLabel, tier, metadata = moduleMetadata });
            var orderedProcedures = (m.Procedures ?? new())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Id, StringComparer.Ordinal)
                .ToList();
            foreach (var p in orderedProcedures)
            {
                CheckTimeout();
                var nodeMeta = BuildProcedureMetadata(m, p);
                var label = mode.Equals("module-structure", StringComparison.OrdinalIgnoreCase) ? (p.Name ?? p.Id) : p.Id;
                nodes.Add(new { id = p.Id, label, tier, containerId = m.Id, metadata = nodeMeta });
                var procedureCalls = p.Calls ?? new List<CallIr>();
                if (!mode.Equals("callgraph", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                bool notedSelfCall = false;
                var fanOutTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in procedureCalls)
                {
                    CheckTimeout();
                    if (!string.IsNullOrWhiteSpace(c.Target) && c.Target != "~unknown")
                    {
                        fanOutTargets.Add(c.Target);
                        if (!notedSelfCall && string.Equals(c.Target, p.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            AddWarning(diagConfig.SelfCallSeverity, $"Procedure '{p.Id}' calls itself.");
                            notedSelfCall = true;
                        }
                        var edgeMeta = new Dictionary<string, string> { ["code.edge"] = "call" };
                        if (!string.IsNullOrWhiteSpace(c.Branch)) edgeMeta["code.branch"] = c.Branch!;
                        if (c.IsDynamic) edgeMeta["code.dynamic"] = "true";
                        if (c.Site is { } site)
                        {
                            if (!string.IsNullOrWhiteSpace(site.Module)) edgeMeta["code.site.module"] = site.Module;
                            if (!string.IsNullOrWhiteSpace(site.File)) edgeMeta["code.site.file"] = site.File;
                            if (site.Line > 0) edgeMeta["code.site.line"] = site.Line.ToString(CultureInfo.InvariantCulture);
                        }
                        edges.Add(new { sourceId = p.Id, targetId = c.Target, label = "call", metadata = edgeMeta });
                    }
                    else if (!string.IsNullOrWhiteSpace(c.Target) && c.Target == "~unknown")
                    {
                        if (includeUnknown)
                        {
                            var edgeMeta = new Dictionary<string, string> { ["code.edge"] = "call", ["code.dynamic"] = "true" };
                            if (!string.IsNullOrWhiteSpace(c.Branch)) edgeMeta["code.branch"] = c.Branch!;
                            if (c.Site is { } site)
                            {
                                if (!string.IsNullOrWhiteSpace(site.Module)) edgeMeta["code.site.module"] = site.Module;
                                if (!string.IsNullOrWhiteSpace(site.File)) edgeMeta["code.site.file"] = site.File;
                                if (site.Line > 0) edgeMeta["code.site.line"] = site.Line.ToString(CultureInfo.InvariantCulture);
                            }
                            edges.Add(new { sourceId = p.Id, targetId = c.Target, label = "call", metadata = edgeMeta });
                            dynamicIncluded++;
                        }
                        else
                        {
                            dynamicSkipped++;
                        }
                    }
                }
                if (fanOutTargets.Count >= diagConfig.HighFanOutThreshold)
                {
                    AddWarning(diagConfig.FanOutSeverity, $"Procedure '{p.Id}' has high fan-out ({fanOutTargets.Count} call targets).");
                }
                totalProcedures++;
                EmitProgress();
            }
            totalModules++;
            EmitProgress();
        }

        // Include sentinel container/node for '~unknown' if any such edges were added
        if (includeUnknown && dynamicIncluded > 0)
        {
            containers.Add(new { id = "~unknown_module", label = "~unknown", tier = "Modules" });
            nodes.Add(new { id = "~unknown", label = "~unknown", tier = "Modules", containerId = "~unknown_module", metadata = new Dictionary<string, string>() });
        }

        EmitProgress(force: true);

        if (warnings.Count > 0)
        {
            foreach (var warning in warnings)
            {
                Console.Error.WriteLine($"{warning.Severity}: {warning.Message}");
            }
        }

        if (mode.Equals("event-wiring", StringComparison.OrdinalIgnoreCase))
        {
            nodes.Clear(); edges.Clear(); containers.Clear();
            var seenControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var formModules = orderedModules.Where(m => string.Equals(m.Kind, "Form", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var m in formModules)
            {
                var tier = TierFor(m);
                var moduleMeta = BuildModuleMetadata(m);
                containers.Add(new { id = m.Id, label = m.Name, tier, metadata = moduleMeta });
                var orderedProcedures = (m.Procedures ?? new())
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Id, StringComparer.Ordinal)
                    .ToList();

                foreach (var p in orderedProcedures)
                {
                    if (string.IsNullOrWhiteSpace(p.Name)) continue;
                    var mm = Regex.Match(p.Name, @"^(?<ctl>[A-Za-z_][A-Za-z0-9_]*)_(?<evt>[A-Za-z_][A-Za-z0-9_]*)$");
                    if (!mm.Success) continue;
                    var moduleDisplay = string.IsNullOrWhiteSpace(m.Name) ? m.Id : m.Name;
                    var ctl = mm.Groups["ctl"].Value;
                    var srcId = m.Id + "." + ctl;
                    if (seenControls.Add(srcId))
                    {
                        var controlMeta = new Dictionary<string, string>();
                        controlMeta["code.module"] = moduleDisplay;
                        controlMeta["code.control"] = ctl;
                        if (m.Source is { } moduleSource && !string.IsNullOrWhiteSpace(moduleSource.File))
                        {
                            controlMeta["code.source.file"] = moduleSource.File;
                            if (moduleSource.Line.HasValue && moduleSource.Line.Value > 0)
                                controlMeta["code.source.line"] = moduleSource.Line.Value.ToString(CultureInfo.InvariantCulture);
                        }
                        nodes.Add(new { id = srcId, label = srcId, tier, containerId = m.Id, metadata = controlMeta });
                    }
                    // Handler node
                    var handlerMeta = BuildProcedureMetadata(m, p);
                    nodes.Add(new { id = p.Id, label = p.Id, tier, containerId = m.Id, metadata = handlerMeta });
                    var meta = new Dictionary<string, string> { ["code.edge"] = "event" };
                    meta["code.module"] = moduleDisplay;
                    if (p.Source is { } handlerSource)
                    {
                        if (!string.IsNullOrWhiteSpace(handlerSource.File)) meta["code.target.file"] = handlerSource.File;
                        if (handlerSource.Line.HasValue && handlerSource.Line.Value > 0)
                            meta["code.target.line"] = handlerSource.Line.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    edges.Add(new { sourceId = srcId, targetId = p.Id, label = mm.Groups["evt"].Value, metadata = meta });
                }
            }
        }

        if (mode.Equals("proc-cfg", StringComparison.OrdinalIgnoreCase))
        {
            nodes.Clear(); edges.Clear(); containers.Clear();
            var flowMeta = new Dictionary<string, string> { ["code.edge"] = "flow" };

            foreach (var m in orderedModules)
            {
                var tier = TierFor(m);
                var orderedProcedures = (m.Procedures ?? new())
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Id, StringComparer.Ordinal)
                    .ToList();

                foreach (var p in orderedProcedures)
                {
                    var contId = p.Id + "#proc";
                    var procMeta = BuildProcedureMetadata(m, p);
                    containers.Add(new { id = contId, label = p.Id, tier, metadata = procMeta });

                    var startId = p.Id + "#start";
                    var startMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "start" };
                    nodes.Add(new { id = startId, label = "Start", tier, containerId = contId, metadata = startMeta });

                    bool hasIf = (p.Tags?.Contains("hasIf") ?? false);
                    bool hasLoop = (p.Tags?.Contains("hasLoop") ?? false);
                    var calls = (p.Calls ?? new List<CallIr>()).Where(c => !string.IsNullOrWhiteSpace(c.Target) && c.Target != "~unknown").ToList();

                    string AppendSequence(IEnumerable<CallIr> sequence, string fromId)
                    {
                        foreach (var call in sequence)
                        {
                            var nodeId = $"{p.Id}#call:{call.Target}@{call.Site?.Line ?? 0}";
                            var callMeta = new Dictionary<string, string>(procMeta)
                            {
                                ["code.flow.node"] = "call",
                                ["code.call.target"] = call.Target
                            };
                            if (call.Site is { } site)
                            {
                                if (!string.IsNullOrWhiteSpace(site.Module)) callMeta["code.site.module"] = site.Module;
                                if (!string.IsNullOrWhiteSpace(site.File)) callMeta["code.site.file"] = site.File;
                                if (site.Line > 0) callMeta["code.site.line"] = site.Line.ToString(CultureInfo.InvariantCulture);
                            }
                            nodes.Add(new { id = nodeId, label = call.Target, tier, containerId = contId, metadata = callMeta });
                            edges.Add(new { sourceId = fromId, targetId = nodeId, label = "seq", metadata = flowMeta });
                            fromId = nodeId;
                        }
                        return fromId;
                    }

                    static bool ContainsBranch(CallIr call, string token)
                    {
                        if (string.IsNullOrWhiteSpace(call.Branch)) return false;
                        return call.Branch.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                          .Any(part => part.Equals(token, StringComparison.OrdinalIgnoreCase));
                    }

                    var plainCalls = calls.Where(c => string.IsNullOrWhiteSpace(c.Branch)).ToList();
                    var loopCalls = calls.Where(c => ContainsBranch(c, "loop")).ToList();
                    var loopCore = loopCalls.Where(c => !ContainsBranch(c, "then") && !ContainsBranch(c, "else")).ToList();
                    var loopThen = loopCalls.Where(c => ContainsBranch(c, "then")).ToList();
                    var loopElse = loopCalls.Where(c => ContainsBranch(c, "else")).ToList();
                    var topThen = calls.Where(c => !ContainsBranch(c, "loop") && ContainsBranch(c, "then")).ToList();
                    var topElse = calls.Where(c => !ContainsBranch(c, "loop") && ContainsBranch(c, "else")).ToList();

                    var handled = new HashSet<CallIr>();
                    string cursor = startId;
                    cursor = AppendSequence(plainCalls, cursor);
                    foreach (var call in plainCalls) handled.Add(call);

                    bool endAdded = false;

                    if (hasLoop)
                    {
                        var loopId = p.Id + "#loop";
                        var loopMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "loop" };
                        nodes.Add(new { id = loopId, label = "Loop", tier, containerId = contId, metadata = loopMeta });
                        edges.Add(new { sourceId = cursor, targetId = loopId, label = "seq", metadata = flowMeta });

                        foreach (var call in loopCore) handled.Add(call);
                        foreach (var call in loopThen) handled.Add(call);
                        foreach (var call in loopElse) handled.Add(call);

                        string loopCursor = AppendSequence(loopCore, loopId);

                        if (hasIf)
                        {
                            var decId = p.Id + "#dec";
                            var decMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "decision" };
                            nodes.Add(new { id = decId, label = "Decision", tier, containerId = contId, metadata = decMeta });
                            edges.Add(new { sourceId = loopCursor, targetId = decId, label = "iter", metadata = flowMeta });

                            var thenId = p.Id + "#then";
                            var thenMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "then" };
                            nodes.Add(new { id = thenId, label = "Then", tier, containerId = contId, metadata = thenMeta });
                            edges.Add(new { sourceId = decId, targetId = thenId, label = "True", metadata = flowMeta });
                            var thenExit = AppendSequence(loopThen, thenId);
                            edges.Add(new { sourceId = thenExit, targetId = loopId, label = "back", metadata = flowMeta });

                            if (loopElse.Any())
                            {
                                var elseId = p.Id + "#else";
                                var elseMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "else" };
                                nodes.Add(new { id = elseId, label = "Else", tier, containerId = contId, metadata = elseMeta });
                                edges.Add(new { sourceId = decId, targetId = elseId, label = "False", metadata = flowMeta });
                                var elseExit = AppendSequence(loopElse, elseId);
                                edges.Add(new { sourceId = elseExit, targetId = loopId, label = "back", metadata = flowMeta });
                            }
                            else
                            {
                                edges.Add(new { sourceId = decId, targetId = loopId, label = "False", metadata = flowMeta });
                            }
                        }
                        else
                        {
                            edges.Add(new { sourceId = loopCursor, targetId = loopId, label = "back", metadata = flowMeta });
                        }

                        var endId = p.Id + "#end";
                        var endMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "end" };
                        nodes.Add(new { id = endId, label = "End", tier, containerId = contId, metadata = endMeta });
                        edges.Add(new { sourceId = loopId, targetId = endId, label = "exit", metadata = flowMeta });
                        cursor = endId;
                        endAdded = true;
                    }
                    else if (hasIf)
                    {
                        var decId = p.Id + "#dec";
                        var decMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "decision" };
                        nodes.Add(new { id = decId, label = "Decision", tier, containerId = contId, metadata = decMeta });
                        edges.Add(new { sourceId = cursor, targetId = decId, label = "seq", metadata = flowMeta });

                        foreach (var call in topThen) handled.Add(call);
                        foreach (var call in topElse) handled.Add(call);

                        var thenId = p.Id + "#then";
                        var thenMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "then" };
                        nodes.Add(new { id = thenId, label = "Then", tier, containerId = contId, metadata = thenMeta });
                        edges.Add(new { sourceId = decId, targetId = thenId, label = "True", metadata = flowMeta });
                        var thenExit = AppendSequence(topThen, thenId);

                        var endId = p.Id + "#end";
                        var endMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "end" };
                        nodes.Add(new { id = endId, label = "End", tier, containerId = contId, metadata = endMeta });
                        edges.Add(new { sourceId = thenExit, targetId = endId, label = "seq", metadata = flowMeta });

                        if (topElse.Any())
                        {
                            var elseId = p.Id + "#else";
                            var elseMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "else" };
                            nodes.Add(new { id = elseId, label = "Else", tier, containerId = contId, metadata = elseMeta });
                            edges.Add(new { sourceId = decId, targetId = elseId, label = "False", metadata = flowMeta });
                            var elseExit = AppendSequence(topElse, elseId);
                            edges.Add(new { sourceId = elseExit, targetId = endId, label = "seq", metadata = flowMeta });
                        }
                        else
                        {
                            edges.Add(new { sourceId = decId, targetId = endId, label = "False", metadata = flowMeta });
                        }

                        cursor = endId;
                        endAdded = true;
                    }

                    if (!endAdded)
                    {
                        var remaining = calls.Where(c => !handled.Contains(c)).ToList();
                        cursor = AppendSequence(remaining, cursor);
                        var endId = p.Id + "#end";
                        var endMeta = new Dictionary<string, string>(procMeta) { ["code.flow.node"] = "end" };
                        nodes.Add(new { id = endId, label = "End", tier, containerId = contId, metadata = endMeta });
                        edges.Add(new { sourceId = cursor, targetId = endId, label = "seq", metadata = flowMeta });
                    }
                }
            }
        }

        if (mode.Equals("module-callmap", StringComparison.OrdinalIgnoreCase))
        {
            // Replace with module-level call map
            nodes.Clear(); edges.Clear(); containers.Clear();
            foreach (var m in orderedModules)
                nodes.Add(new { id = m.Id, label = m.Name, tier = TierFor(m) });
            var agg = new Dictionary<(string src, string dst), int>();
            foreach (var m in orderedModules)
            {
                var orderedProcedures = (m.Procedures ?? new())
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Id, StringComparer.Ordinal)
                    .ToList();

                foreach (var p in orderedProcedures)
                {
                    foreach (var c in p.Calls ?? new())
                    {
                        if (string.IsNullOrWhiteSpace(c.Target) || c.Target == "~unknown") continue;
                        var parts = c.Target.Split('.'); if (parts.Length < 2) continue;
                        var dstMod = parts[0]; var srcMod = m.Id;
                        if (string.Equals(srcMod, dstMod, StringComparison.OrdinalIgnoreCase)) continue;
                        var key = (srcMod, dstMod);
                        agg[key] = agg.TryGetValue(key, out var n) ? n + 1 : 1;
                    }
                }
            }
            foreach (var kv in agg)
            {
                var meta = new Dictionary<string, string> { ["code.edge"] = "module-call" };
                edges.Add(new { sourceId = kv.Key.src, targetId = kv.Key.dst, label = $"{kv.Value} call(s)", metadata = meta });
            }
        }

        object pageConfig = mode.Equals("event-wiring", StringComparison.OrdinalIgnoreCase)
            ? new
            {
                heightIn = 8.5,
                marginIn = 0.5,
                paginate = true,
                plan = new { laneSplitAllowed = true }
            }
            : new { heightIn = 8.5, marginIn = 0.5 };

        var diagram = new
        {
            schemaVersion = "1.2",
            layout = new
            {
                tiers,
                spacing = new { horizontal = 1.2, vertical = 0.6 },
                page = pageConfig,
                containers = new { paddingIn = 0.0, cornerIn = 0.12 }
            },
            nodes,
            edges,
            containers
        };

        var json = JsonSerializer.Serialize(diagram, JsonOpts);
        if (string.IsNullOrWhiteSpace(output)) Console.WriteLine(json);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!)) ?? ".");
            File.WriteAllText(output!, json);
        }

        EmitHyperlinkSummary(orderedModules, mode, summaryLogPath);

        if (!string.IsNullOrWhiteSpace(output))
        {
            var progressLastMs = progressEmits > 0
                ? (int)Math.Round(lastProgressElapsed.TotalMilliseconds)
                : (int)Math.Round(progressStopwatch.Elapsed.TotalMilliseconds);
            Console.WriteLine($"modules:{totalModules} procedures:{totalProcedures} edges:{edges.Count} dynamicSkipped:{dynamicSkipped} dynamicIncluded:{dynamicIncluded} progressEmits:{progressEmits} progressLastMs:{progressLastMs}");
        }
        return ExitCodes.Ok;
    }

    private static void EmitHyperlinkSummary(IReadOnlyList<ModuleIr> modules, string mode, string? summaryLogPath)
    {
        var entries = CollectLinkSummaryEntries(modules);
        if (entries.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(summaryLogPath))
            {
                var fullPath = Path.GetFullPath(summaryLogPath!);
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(fullPath, "Name,File,Module,StartLine,EndLine,Hyperlink" + Environment.NewLine);
                Console.WriteLine($"Hyperlink summary written to {fullPath}");
            }
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Hyperlink Summary ({mode}):");
        Console.WriteLine("Procedure/Control | File Path | Module | Start Line | End Line | Hyperlink");
        foreach (var entry in entries)
        {
            Console.WriteLine($"{entry.Name} | {entry.File} | {entry.Module} | {entry.StartLine} | {entry.EndLine} | {entry.Hyperlink}");
        }

        if (!string.IsNullOrWhiteSpace(summaryLogPath))
        {
            var fullPath = Path.GetFullPath(summaryLogPath!);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine("Name,File,Module,StartLine,EndLine,Hyperlink");
            string CsvEscape(string value) => value.Contains('"', StringComparison.Ordinal) || value.Contains(',', StringComparison.Ordinal)
                ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
                : value;
            foreach (var entry in entries)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    CsvEscape(entry.Name),
                    CsvEscape(entry.File),
                    CsvEscape(entry.Module),
                    CsvEscape(entry.StartLine.ToString(CultureInfo.InvariantCulture)),
                    CsvEscape(entry.EndLine.ToString(CultureInfo.InvariantCulture)),
                    CsvEscape(entry.Hyperlink)
                }));
            }
            File.WriteAllText(fullPath, sb.ToString());
            Console.WriteLine($"Hyperlink summary written to {fullPath}");
        }
    }

    private static List<LinkSummaryEntry> CollectLinkSummaryEntries(IEnumerable<ModuleIr> modules)
    {
        var entries = new List<LinkSummaryEntry>();
        if (modules is null) return entries;

        foreach (var module in modules)
        {
            if (module is null) continue;
            var moduleName = !string.IsNullOrWhiteSpace(module.Name) ? module.Name! : module.Id ?? string.Empty;
            var procedures = module.Procedures ?? new List<ProcedureIr>();
            foreach (var procedure in procedures)
            {
                if (procedure is null) continue;
                var displayName = !string.IsNullOrWhiteSpace(procedure.Name) ? procedure.Name! : procedure.Id ?? string.Empty;
                if (string.IsNullOrWhiteSpace(displayName)) continue;
                var file = procedure.Source?.File
                           ?? procedure.Locs?.File
                           ?? module.Source?.File
                           ?? module.File
                           ?? string.Empty;
                if (string.IsNullOrWhiteSpace(file)) continue;
                var start = procedure.Locs?.StartLine ?? procedure.Source?.Line ?? 0;
                var end = procedure.Locs?.EndLine ?? start;
                var hyperlink = start > 0 ? $"{file}#L{start}" : file;
                entries.Add(new LinkSummaryEntry(displayName, file, moduleName, start, end, hyperlink));
            }
        }

        return entries
            .OrderBy(e => e.Module, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record LinkSummaryEntry(string Name, string File, string Module, int StartLine, int EndLine, string Hyperlink);

    private static string KindFromExt(string? ext) => (ext ?? string.Empty).ToLowerInvariant() switch
    {
        ".frm" => "Form",
        ".cls" => "Class",
        _ => "Module"
    };

    private static string[] LoadLogicalLines(string file)
    {
        var raw = File.ReadAllLines(file);
        var logical = new List<string>();
        var current = new StringBuilder();
        foreach (var line in raw)
        {
            var trimmed = line.TrimEnd();
            current.Append(trimmed);
            if (trimmed.EndsWith("_", StringComparison.Ordinal))
            {
                current.Length -= 1; // remove trailing underscore
                continue;
            }
            logical.Add(current.ToString());
            current.Clear();
        }
        if (current.Length > 0) logical.Add(current.ToString());
        return logical.ToArray();
    }

    private static string? TryMatch(string[] lines, string pattern)
    {
        foreach (var l in lines)
        {
            var m = Regex.Match(l, pattern, RegexOptions.IgnoreCase);
            if (m.Success && m.Groups.Count > 1) return m.Groups[1].Value;
        }
        return null;
    }

    private static List<ProcedureIr> ParseProcedures(
        string[] lines,
        string moduleName,
        string filePath,
        HashSet<string> moduleSet,
        Dictionary<string, string?> returnTypeMap,
        bool includeMetrics)
    {
        var result = new List<ProcedureIr>();
        string TrimToken(string token) => Regex.Replace(token ?? string.Empty, @"\([^)]*\)$", "");
        string? ResolveReferenceType(Dictionary<string, string> scopeTypes, string token)
        {
            var cleaned = TrimToken(token);
            if (string.IsNullOrWhiteSpace(cleaned)) return null;
            if (string.Equals(cleaned, "Me", StringComparison.OrdinalIgnoreCase)) return moduleName;
            if (scopeTypes.TryGetValue(cleaned, out var ty)) return ty;
            if (moduleSet.Contains(cleaned)) return cleaned;
            return null;
        }
        string? LookupReturnType(string? qualifierType, string methodName)
        {
            if (string.IsNullOrWhiteSpace(qualifierType)) return null;
            var key = qualifierType + "." + methodName;
            return returnTypeMap.TryGetValue(key, out var ret) && !string.IsNullOrWhiteSpace(ret) ? ret : null;
        }
        string? ResolveExpressionType(string expression, Dictionary<string, string> scopeTypes)
        {
            var segments = expression.Split('.', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim())
                                     .Where(s => !string.IsNullOrEmpty(s))
                                     .ToArray();
            if (segments.Length == 0) return null;
            string? currentType = null;
            for (int idx = 0; idx < segments.Length; idx++)
            {
                var segment = segments[idx];
                var token = TrimToken(segment);
                var isCall = segment.Contains("(");
                if (idx == 0)
                {
                    currentType = ResolveReferenceType(scopeTypes, token) ?? token;
                    if (isCall)
                    {
                        currentType = LookupReturnType(currentType, token) ?? currentType;
                    }
                }
                else
                {
                    var baseType = currentType ?? ResolveReferenceType(scopeTypes, token) ?? token;
                    if (isCall)
                        currentType = LookupReturnType(baseType, token) ?? baseType;
                    else
                        currentType = ResolveReferenceType(scopeTypes, token) ?? token;
                }
            }
            return currentType;
        }
        var sig = new Regex(@"^(?<indent>\s*)(?<access>Public|Private|Friend)?\s*(?<static>Static\s*)?(?<kind>Sub|Function|Property\s+Get|Property\s+Let|Property\s+Set)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<rest>.*)$", RegexOptions.IgnoreCase);
        for (int i = 0; i < lines.Length; i++)
        {
            var m = sig.Match(lines[i]);
            if (!m.Success) continue;
            var kindRaw = m.Groups["kind"].Value.Trim();
            var kind = kindRaw.Equals("Sub", StringComparison.OrdinalIgnoreCase) ? "Sub" :
                       kindRaw.Equals("Function", StringComparison.OrdinalIgnoreCase) ? "Function" :
                       kindRaw.Contains("Get", StringComparison.OrdinalIgnoreCase) ? "PropertyGet" :
                       kindRaw.Contains("Let", StringComparison.OrdinalIgnoreCase) ? "PropertyLet" : "PropertySet";
            var name = m.Groups["name"].Value;
            var access = m.Groups["access"].Success ? m.Groups["access"].Value : null;
            var isStatic = m.Groups["static"].Success;
            var rest = m.Groups["rest"].Value;

            // Parse params within (...) if present
            var @params = new List<ParamIr>();
            var paren = Regex.Match(rest, @"\((?<plist>[^\)]*)\)");
            if (paren.Success)
            {
                var plist = paren.Groups["plist"].Value;
                foreach (var raw in plist.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var token = raw.Trim();
                    var pm = Regex.Match(token, @"^(?:Optional\s+)?(?<by>(ByRef|ByVal)\s+)?(?<nm>[A-Za-z_][A-Za-z0-9_]*)(?:\s+As\s+(?<ty>[A-Za-z_][A-Za-z0-9_\.]*))?", RegexOptions.IgnoreCase);
                    if (pm.Success)
                    {
                        @params.Add(new ParamIr
                        {
                            Name = pm.Groups["nm"].Value,
                            Type = pm.Groups["ty"].Success ? pm.Groups["ty"].Value : null,
                            ByRef = pm.Groups["by"].Success && pm.Groups["by"].Value.Trim().StartsWith("ByRef", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }

            // Parse return type for Function/PropertyGet
            string? returns = null;
            if (kind == "Function" || kind == "PropertyGet")
            {
                // Look in rest after closing paren (if any) for 'As Type'
                var after = rest;
                var asMatch = Regex.Match(after, @"\)\s*As\s+(?<rt>[A-Za-z_][A-Za-z0-9_\.]*)", RegexOptions.IgnoreCase);
                if (!asMatch.Success)
                    asMatch = Regex.Match(after, @"\bAs\s+(?<rt>[A-Za-z_][A-Za-z0-9_\.]*)", RegexOptions.IgnoreCase);
                if (asMatch.Success) returns = asMatch.Groups["rt"].Value;
            }
            int start = i + 1, end = start;
            var endToken = kind == "Function" ? "End Function" : kind == "Sub" ? "End Sub" : "End Property";
            for (int j = i + 1; j < lines.Length; j++)
            {
                if (Regex.IsMatch(lines[j], @"^\s*" + Regex.Escape(endToken) + @"\b", RegexOptions.IgnoreCase)) { end = j + 1; break; }
            }
            if (end <= start) end = Math.Min(lines.Length, start + 1);

            var calls = new List<CallIr>();
            var varTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var withStack = new Stack<string>();
            var branchStack = new Stack<string>();
            bool hasIf = false, hasLoop = false;
            int loopDepth = 0;
            int sloc = 0;
            int branchKeywords = 0;
            for (int k = i; k < Math.Min(lines.Length, end - 1); k++)
            {
                var originalLine = lines[k];
                var commentIndex = originalLine.IndexOf('\'');
                var line = commentIndex >= 0 ? originalLine.Substring(0, commentIndex) : originalLine;
                var seenLineCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? CurrentBranchLabel()
                {
                    var parts = new List<string>();
                    if (loopDepth > 0) parts.Add("loop");
                    if (branchStack.Count > 0) parts.Add(branchStack.Peek());
                    return parts.Count > 0 ? string.Join("|", parts) : null;
                }
                void RecordCall(string target, bool isDynamic = false)
                {
                    if (string.IsNullOrWhiteSpace(target)) return;
                    var branchLabel = CurrentBranchLabel();
                    var key = $"{target}|{isDynamic}|{branchLabel}";
                    if (!seenLineCalls.Add(key)) return;
                    calls.Add(new CallIr
                    {
                        Target = target,
                        IsDynamic = isDynamic,
                        Branch = branchLabel,
                        Site = new SiteIr { Module = moduleName, File = filePath, Line = k + 1 }
                    });
                }

                var trimmed = line.Trim();
                var trimmedOriginal = originalLine.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedOriginal) && !trimmedOriginal.StartsWith("'", StringComparison.Ordinal))
                {
                    sloc++;
                    var upper = trimmedOriginal.ToUpperInvariant();
                    if (Regex.IsMatch(upper, @"(^|[^A-Z])IF\b")) branchKeywords++;
                    if (Regex.IsMatch(upper, @"\bELSEIF\b")) branchKeywords++;
                    if (Regex.IsMatch(upper, @"\bSELECT\s+CASE\b")) branchKeywords++;
                    if (Regex.IsMatch(upper, @"\bCASE\b") && !Regex.IsMatch(upper, @"\bSELECT\s+CASE\b")) branchKeywords++;
                    if (Regex.IsMatch(upper, @"\bFOR\b")) branchKeywords++;
                    if (Regex.IsMatch(upper, @"\bDO\b")) branchKeywords++;
                    if (Regex.IsMatch(upper, @"\bWHILE\b")) branchKeywords++;
                    if (Regex.IsMatch(upper, @"\bUNTIL\b")) branchKeywords++;
                }
                if (!hasIf && Regex.IsMatch(trimmed, @"^\s*If\b", RegexOptions.IgnoreCase)) hasIf = true;
                if (!hasLoop && Regex.IsMatch(trimmed, @"^\s*(For\b|Do\b|While\b)", RegexOptions.IgnoreCase)) hasLoop = true;

                if (Regex.IsMatch(trimmed, @"^\s*End\s+If\b", RegexOptions.IgnoreCase))
                {
                    if (branchStack.Count > 0) branchStack.Pop();
                }
                else if (Regex.IsMatch(trimmed, @"^\s*ElseIf\b.*Then\b", RegexOptions.IgnoreCase))
                {
                    if (branchStack.Count > 0) branchStack.Pop();
                    branchStack.Push("then");
                }
                else if (Regex.IsMatch(trimmed, @"^\s*Else\b", RegexOptions.IgnoreCase))
                {
                    if (branchStack.Count > 0) branchStack.Pop();
                    branchStack.Push("else");
                }
                else if (Regex.IsMatch(trimmed, @"^\s*If\b.*Then\b", RegexOptions.IgnoreCase))
                {
                    branchStack.Push("then");
                }

                if (Regex.IsMatch(trimmed, @"^\s*(Next\b|Loop\b|End\s+Do\b|Wend\b)", RegexOptions.IgnoreCase))
                {
                    if (loopDepth > 0) loopDepth--;
                }

                if (Regex.IsMatch(trimmed, @"^\s*(For\b|Do\b|While\b)", RegexOptions.IgnoreCase))
                {
                    loopDepth++;
                }

                // Track variable declarations/types
                var dim = Regex.Match(line, @"^\s*(?:Dim|Public|Private)\s+(?<nm>[A-Za-z_][A-Za-z0-9_]*)\s+As\s+(?<ty>[A-Za-z_][A-Za-z0-9_\.]+)", RegexOptions.IgnoreCase);
                if (dim.Success) varTypes[dim.Groups["nm"].Value] = dim.Groups["ty"].Value;
                // Dim x As New Type
                var dimNew = Regex.Match(line, @"^\s*(?:Dim|Public|Private)\s+(?<nm>[A-Za-z_][A-Za-z0-9_]*)\s+As\s+New\s+(?<ty>[A-Za-z_][A-Za-z0-9_\.]+)", RegexOptions.IgnoreCase);
                if (dimNew.Success) varTypes[dimNew.Groups["nm"].Value] = dimNew.Groups["ty"].Value;
                var inst = Regex.Match(line, @"^\s*Set\s+(?<nm>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*New\s+(?<ty>[A-Za-z_][A-Za-z0-9_\.]+)", RegexOptions.IgnoreCase);
                if (inst.Success) varTypes[inst.Groups["nm"].Value] = inst.Groups["ty"].Value;
                var aliasAssign = Regex.Match(line, @"^\s*Set\s+(?<nm>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<rhs>.+)$", RegexOptions.IgnoreCase);
                if (aliasAssign.Success)
                {
                    var lhs = aliasAssign.Groups["nm"].Value;
                    var rhsRaw = aliasAssign.Groups["rhs"].Value;
                    var commentSplit = rhsRaw.Split('\'');
                    rhsRaw = commentSplit.Length > 0 ? commentSplit[0].Trim() : rhsRaw.Trim();
                    if (!rhsRaw.Contains("(") && !rhsRaw.Contains("."))
                    {
                        var resolved = ResolveReferenceType(varTypes, rhsRaw);
                        if (!string.IsNullOrEmpty(resolved)) varTypes[lhs] = resolved!;
                    }
                    else
                    {
                        var exprType = ResolveExpressionType(rhsRaw, varTypes);
                        if (!string.IsNullOrWhiteSpace(exprType)) varTypes[lhs] = exprType!;
                    }
                }
                var withStart = Regex.Match(line, @"^\s*With\s+(?<nm>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase);
                if (withStart.Success) withStack.Push(withStart.Groups["nm"].Value);
                if (Regex.IsMatch(line, @"^\s*End\s+With\b", RegexOptions.IgnoreCase) && withStack.Count > 0) withStack.Pop();
                // Bind 'Me' to current module (for Class/Form)
                if (!varTypes.ContainsKey("Me")) varTypes["Me"] = moduleName;
                // Qualified or chained calls: Obj.Member or Obj.Prop().Member
                foreach (Match cm in Regex.Matches(line, @"(?<chain>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*(?:\(\s*\))?)+)\s*(?=\(|\b)", RegexOptions.IgnoreCase))
                {
                    var chain = cm.Groups["chain"].Value;
                    var segments = chain.Split('.', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrEmpty(s))
                                        .ToArray();
                    if (segments.Length < 2) continue;
                    var method = TrimToken(segments[^1]);
                    if (string.IsNullOrWhiteSpace(method)) continue;
                    var qualifierExpr = string.Join(".", segments.Take(segments.Length - 1));
                    var qualifier = ResolveExpressionType(qualifierExpr, varTypes);
                    if (string.IsNullOrWhiteSpace(qualifier))
                        qualifier = ResolveReferenceType(varTypes, TrimToken(segments[0])) ?? TrimToken(segments[0]);
                    RecordCall((qualifier ?? moduleName) + "." + method);
                }
                // Unqualified at line start: (Set var = )?(Call )?ProcName(
                var uq = Regex.Match(line, @"^\s*(?:Set\s+\w+\s*=\s*)?(?:Call\s+)?(?<p>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.IgnoreCase);
                if (uq.Success)
                {
                    var pname = uq.Groups["p"].Value;
                    var kw = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "If", "While", "Do", "For", "With", "Select", "Call", "Set" };
                    if (!kw.Contains(pname))
                        RecordCall(moduleName + "." + pname);
                }
                // Call Module.Proc(...)
                var callQual = Regex.Match(line, @"\bCall\s+([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase);
                if (callQual.Success)
                {
                    RecordCall(callQual.Groups[1].Value + "." + callQual.Groups[2].Value);
                }
                // Within With-block: .Method(
                if (withStack.Count > 0)
                {
                    var exprMatch = Regex.Match(line, @"^\s*\.(?<expr>[A-Za-z_][A-Za-z0-9_]*(?:\(\s*\))?(?:\.[A-Za-z_][A-Za-z0-9_]*(?:\(\s*\))?)*)", RegexOptions.IgnoreCase);
                    if (exprMatch.Success)
                    {
                        var expr = exprMatch.Groups["expr"].Value;
                        var segments = expr.Split('.', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(s => s.Trim())
                                           .Where(s => !string.IsNullOrEmpty(s))
                                           .ToArray();
                        if (segments.Length > 0)
                        {
                            var baseVar = withStack.Peek();
                            var baseType = ResolveReferenceType(varTypes, baseVar) ?? baseVar;
                            var firstSegment = segments[0];
                            if (firstSegment.Contains("("))
                            {
                                var firstMethod = TrimToken(firstSegment);
                                RecordCall(baseType + "." + firstMethod);
                            }
                            string? qualifierType = baseType;
                            if (segments.Length > 1)
                            {
                                var qualifierExpr = baseType + "." + string.Join(".", segments.Take(segments.Length - 1));
                                qualifierType = ResolveExpressionType(qualifierExpr, varTypes) ?? qualifierType;
                            }
                            var method = TrimToken(segments[^1]);
                            if (!string.IsNullOrWhiteSpace(method))
                            {
                                RecordCall((qualifierType ?? baseType) + "." + method);
                            }
                        }
                    }
                }
                if (Regex.IsMatch(line, @"\b(CallByName|Application\.Run)\b", RegexOptions.IgnoreCase))
                {
                    RecordCall("~unknown", true);
                }
            }

            var tags = new List<string>();
            if (hasIf) tags.Add("hasIf");
            if (hasLoop) tags.Add("hasLoop");

            var metrics = includeMetrics
                ? new MetricsIr
                {
                    Lines = end - start + 1,
                    Sloc = sloc,
                    Cyclomatic = Math.Max(1, 1 + branchKeywords)
                }
                : null;

            result.Add(new ProcedureIr
            {
                Id = moduleName + "." + name,
                Name = name,
                Kind = kind,
                Access = access,
                Static = isStatic ? true : null,
                Params = @params.Count > 0 ? @params : null,
                Returns = returns,
                Locs = new LocsIr { File = filePath, StartLine = start, EndLine = end },
                Source = new SourceIr { File = filePath, Module = moduleName, Line = start },
                Calls = calls.Count > 0 ? calls : null,
                Metrics = metrics,
                Tags = tags.Count > 0 ? tags : null
            });
            i = end - 1;
        }
        return result;
    }

    private static Dictionary<string, string?> BuildReturnTypeMap(List<(string file, string[] lines, string name, string kind)> modules)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var sig = new Regex(@"^(?<indent>\s*)(?<access>Public|Private|Friend)?\s*(?<static>Static\s*)?(?<kind>Sub|Function|Property\s+Get|Property\s+Let|Property\s+Set)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<rest>.*)$", RegexOptions.IgnoreCase);
        foreach (var module in modules)
        {
            for (int i = 0; i < module.lines.Length; i++)
            {
                var m = sig.Match(module.lines[i]);
                if (!m.Success) continue;
                var kindRaw = m.Groups["kind"].Value.Trim();
                var kind = kindRaw.Equals("Sub", StringComparison.OrdinalIgnoreCase) ? "Sub" :
                           kindRaw.Equals("Function", StringComparison.OrdinalIgnoreCase) ? "Function" :
                           kindRaw.Contains("Get", StringComparison.OrdinalIgnoreCase) ? "PropertyGet" :
                           kindRaw.Contains("Let", StringComparison.OrdinalIgnoreCase) ? "PropertyLet" : "PropertySet";
                var name = m.Groups["name"].Value;
                string? returns = null;
                if (kind == "Function" || kind == "PropertyGet")
                {
                    var rest = m.Groups["rest"].Value;
                    var asMatch = Regex.Match(rest, @"\)\s*As\s+(?<rt>[A-Za-z_][A-Za-z0-9_\.]*)", RegexOptions.IgnoreCase);
                    if (!asMatch.Success)
                        asMatch = Regex.Match(rest, @"\bAs\s+(?<rt>[A-Za-z_][A-Za-z0-9_\.]*)", RegexOptions.IgnoreCase);
                    if (asMatch.Success) returns = asMatch.Groups["rt"].Value;
                }
                var key = module.name + "." + name;
                if (map.TryGetValue(key, out var existing))
                {
                    if (string.IsNullOrWhiteSpace(existing) && !string.IsNullOrWhiteSpace(returns))
                        map[key] = returns;
                }
                else
                {
                    map[key] = returns;
                }
            }
        }
        return map;
    }

    private static string MakeRelative(string root, string path)
    {
        try
        {
            var rel = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(rel) && !rel.StartsWith("..", StringComparison.Ordinal))
                return rel.Replace('\\', '/');
        }
        catch
        {
            // fall back to original path
        }
        return path;
    }

    private static int RunRender(string[] args)
    {
        string? inputFolder = null; string? outVsdx = null; string mode = "callgraph"; string? cliPath = null; string? diagramJson = null; string? diagnosticsJson = null; string? summaryLogPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--in", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--in requires folder"); inputFolder = args[++i]; }
            else if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--out requires .vsdx"); outVsdx = args[++i]; }
            else if (string.Equals(a, "--mode", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--mode requires callgraph|module-structure|module-callmap"); mode = args[++i]; }
            else if (string.Equals(a, "--cli", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--cli requires path to VDG.CLI.exe"); cliPath = args[++i]; }
            else if (string.Equals(a, "--diagram-json", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--diagram-json requires path"); diagramJson = args[++i]; }
            else if (string.Equals(a, "--diag-json", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--diag-json requires path"); diagnosticsJson = args[++i]; }
            else if (string.Equals(a, "--summary-log", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--summary-log requires path"); summaryLogPath = args[++i]; }
            else throw new UsageException($"Unknown option '{a}' for render.");
        }
        if (string.IsNullOrWhiteSpace(inputFolder) || string.IsNullOrWhiteSpace(outVsdx)) throw new UsageException("render requires --in <folder> and --out <diagram.vsdx>");
        var irPath = Path.Combine(Path.GetTempPath(), $"vdg_ir_{Guid.NewGuid():N}.json");
        var diagPath = diagramJson ?? Path.Combine(Path.GetTempPath(), $"vdg_diag_{Guid.NewGuid():N}.json");
        RunVba2Json(new[] { "--in", inputFolder!, "--out", irPath });
        var irArgs = new List<string> { "--in", irPath, "--out", diagPath, "--mode", mode };
        if (!string.IsNullOrWhiteSpace(summaryLogPath))
        {
            irArgs.Add("--summary-log");
            irArgs.Add(summaryLogPath!);
        }
        RunIr2Diagram(irArgs.ToArray());

        var cli = FindCli(cliPath);
        if (string.IsNullOrWhiteSpace(cli)) throw new UsageException("VDG.CLI.exe not found. Provide --cli <path> or set VDG_CLI env.");
        if (!string.IsNullOrWhiteSpace(diagnosticsJson))
        {
            var dir = Path.GetDirectoryName(diagnosticsJson);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        }

        var argsBuilder = new List<string>();
        if (!string.IsNullOrWhiteSpace(diagnosticsJson))
        {
            argsBuilder.Add("--diag-json");
            argsBuilder.Add($"\"{diagnosticsJson}\"");
        }
        argsBuilder.Add($"\"{diagPath}\"");
        argsBuilder.Add($"\"{outVsdx}\"");
        var psi = new System.Diagnostics.ProcessStartInfo(cli!, string.Join(" ", argsBuilder))
        {
            UseShellExecute = false
        };
        Console.WriteLine($"info: invoking {cli} {psi.Arguments}");
        using var p = System.Diagnostics.Process.Start(psi)!; p.WaitForExit();
        if (p.ExitCode != 0) throw new Exception($"VDG.CLI exited with {p.ExitCode}");
        Console.WriteLine($"Saved diagram: {outVsdx}");
        return ExitCodes.Ok;
    }

    private static string? FindCli(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;
        var env = Environment.GetEnvironmentVariable("VDG_CLI");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        // Search from CWD upwards for typical Debug/Release paths
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int depth = 0; dir != null && depth < 6; depth++, dir = dir.Parent)
        {
            var d1 = Path.Combine(dir.FullName, "src", "VDG.CLI", "bin", "Debug", "net48", "VDG.CLI.exe");
            if (File.Exists(d1)) return d1;
            var d2 = Path.Combine(dir.FullName, "src", "VDG.CLI", "bin", "Release", "net48", "VDG.CLI.exe");
            if (File.Exists(d2)) return d2;
        }
        // Try relative to the running CLI's base directory as a last resort
        var baseDir = AppContext.BaseDirectory;
        string CombineUp(string basePath, int ups, params string[] rest)
        {
            var cur = basePath;
            for (int i = 0; i < ups; i++) cur = Path.GetFullPath(Path.Combine(cur, ".."));
            return Path.Combine(new[] { cur }.Concat(rest).ToArray());
        }
        var cand = CombineUp(baseDir, 5, "src", "VDG.CLI", "bin", "Debug", "net48", "VDG.CLI.exe");
        if (File.Exists(cand)) return cand;
        cand = CombineUp(baseDir, 5, "src", "VDG.CLI", "bin", "Release", "net48", "VDG.CLI.exe");
        return File.Exists(cand) ? cand : null;
    }
}

// IR types
internal sealed class IrRoot
{
    [JsonPropertyName("irSchemaVersion")] public string IrSchemaVersion { get; set; } = "0.2";
    [JsonPropertyName("generator")] public GeneratorIr Generator { get; set; } = new();
    [JsonPropertyName("project")] public ProjectIr Project { get; set; } = new();
}
internal sealed class GeneratorIr { public string? Name { get; set; } public string? Version { get; set; } }
internal sealed class ProjectIr { public string Name { get; set; } = ""; public List<ModuleIr> Modules { get; set; } = new(); }
internal sealed class ModuleIr { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string? Kind { get; set; } public string? File { get; set; } public SourceIr? Source { get; set; } public MetricsIr? Metrics { get; set; } public List<ProcedureIr> Procedures { get; set; } = new(); }
internal sealed class ProcedureIr
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Kind { get; set; }
    public string? Access { get; set; }
    public bool? Static { get; set; }
    public List<ParamIr>? Params { get; set; }
    public string? Returns { get; set; }
    public LocsIr Locs { get; set; } = new();
    public SourceIr? Source { get; set; }
    public List<CallIr>? Calls { get; set; }
    public MetricsIr? Metrics { get; set; }
    public List<string>? Tags { get; set; }
}
internal sealed class ParamIr { public string Name { get; set; } = ""; public string? Type { get; set; } public bool ByRef { get; set; } }
internal sealed class LocsIr { public string File { get; set; } = ""; public int StartLine { get; set; } public int EndLine { get; set; } }
internal sealed class CallIr { public string Target { get; set; } = ""; public bool IsDynamic { get; set; } public SiteIr Site { get; set; } = new(); public string? Branch { get; set; } }
internal sealed class SiteIr { public string Module { get; set; } = ""; public string File { get; set; } = ""; public int Line { get; set; } }
internal sealed class SourceIr { public string File { get; set; } = ""; public string? Module { get; set; } public int? Line { get; set; } }
internal sealed class MetricsIr { public int? Cyclomatic { get; set; } public int? Lines { get; set; } public int? Sloc { get; set; } }
