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
        Console.Error.WriteLine("  vba2json --in <folder> [--out <ir.json>] [--project-name <name>]");
        Console.Error.WriteLine("  ir2diagram --in <ir.json> [--out <diagram.json>] [--mode <callgraph|module-structure|module-callmap|event-wiring|proc-cfg>]");
        Console.Error.WriteLine("  render --in <folder> --out <diagram.vsdx> [--mode <callgraph|module-structure|module-callmap>] [--cli <VDG.CLI.exe>] [--diagram-json <path>]");
    }

    private static int RunVba2Json(string[] args)
    {
        string? input = null; string? output = null; string? projectName = null;
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
            else throw new UsageException($"Unknown option '{a}' for vba2json.");
        }

        if (string.IsNullOrWhiteSpace(input)) throw new UsageException("vba2json requires --in <folder>.");
        var folder = Path.GetFullPath(input!);
        if (!Directory.Exists(folder)) throw new UsageException($"Input folder not found: {folder}");

        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                             .Where(f => f.EndsWith(".bas", StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".cls", StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".frm", StringComparison.OrdinalIgnoreCase))
                             .ToArray();
        // First pass: gather module names/kinds
        var firstPass = new List<(string file, string[] lines, string name, string kind)>();
        var moduleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files)
        {
            var lines = LoadLogicalLines(f);
            var modName = TryMatch(lines, "Attribute\\s+VB_Name\\s*=\\s*\\\"([^\\\"]+)\\\"") ?? Path.GetFileNameWithoutExtension(f);
            var kind = KindFromExt(Path.GetExtension(f));
            firstPass.Add((f, lines, modName, kind));
            moduleSet.Add(modName);
        }
        // Second pass: parse with symbol table awareness
        var returnTypeMap = BuildReturnTypeMap(firstPass);
        var modules = new List<ModuleIr>();
        foreach (var it in firstPass)
        {
            var procs = ParseProcedures(it.lines, it.name, MakeRelative(folder, it.file), moduleSet, returnTypeMap);
            modules.Add(new ModuleIr { Id = it.name, Name = it.name, Kind = it.kind, File = MakeRelative(folder, it.file), Procedures = procs });
        }

        var ir = new IrRoot
        {
            IrSchemaVersion = "0.1",
            Generator = new() { Name = "vba2json", Version = "0.1.0" },
            Project = new() { Name = string.IsNullOrWhiteSpace(projectName) ? Path.GetFileName(folder) : projectName!, Modules = modules }
        };

        var json = JsonSerializer.Serialize(ir, JsonOpts);
        if (string.IsNullOrWhiteSpace(output)) Console.WriteLine(json);
        else { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!)) ?? "."); File.WriteAllText(output!, json); }
        return ExitCodes.Ok;
    }

    private static int RunIr2Diagram(string[] args)
    {
        string? input = null; string? output = null; string mode = "callgraph";
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--in", StringComparison.OrdinalIgnoreCase))
            { if (i + 1 >= args.Length) throw new UsageException("--in requires a json path."); input = args[++i]; }
            else if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase))
            { if (i + 1 >= args.Length) throw new UsageException("--out requires a json path."); output = args[++i]; }
            else if (string.Equals(a, "--mode", StringComparison.OrdinalIgnoreCase))
            { if (i + 1 >= args.Length) throw new UsageException("--mode requires callgraph|module-structure|module-callmap."); mode = args[++i]; }
            else throw new UsageException($"Unknown option '{a}' for ir2diagram.");
        }
        if (string.IsNullOrWhiteSpace(input)) throw new UsageException("ir2diagram requires --in <ir.json>.");
        if (!File.Exists(input!)) throw new UsageException($"IR file not found: {input}");

        var root = JsonSerializer.Deserialize<IrRoot>(File.ReadAllText(input!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new UsageException("Invalid IR JSON.");

        var tiers = new[] { "Forms", "Classes", "Modules" };
        string TierFor(string k) => string.Equals(k, "Form", StringComparison.OrdinalIgnoreCase) ? "Forms"
                                            : string.Equals(k, "Class", StringComparison.OrdinalIgnoreCase) ? "Classes"
                                            : "Modules";

        var nodes = new List<object>();
        var edges = new List<object>();
        var containers = new List<object>();

        foreach (var m in root.Project.Modules)
        {
            var tier = TierFor(m.Kind ?? "Module");
            containers.Add(new { id = m.Id, label = m.Name, tier });
            foreach (var p in m.Procedures ?? new())
            {
                var nodeMeta = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(m.Name)) nodeMeta["code.module"] = m.Name;
                if (!string.IsNullOrWhiteSpace(p.Name)) nodeMeta["code.proc"] = p.Name;
                if (!string.IsNullOrWhiteSpace(p.Kind)) nodeMeta["code.kind"] = p.Kind!;
                if (!string.IsNullOrWhiteSpace(p.Access)) nodeMeta["code.access"] = p.Access!;
                var label = mode.Equals("module-structure", StringComparison.OrdinalIgnoreCase) ? (p.Name ?? p.Id) : p.Id;
                nodes.Add(new { id = p.Id, label, tier, containerId = m.Id, metadata = nodeMeta });
                if (!mode.Equals("callgraph", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var c in p.Calls ?? new())
                {
                    if (!string.IsNullOrWhiteSpace(c.Target) && c.Target != "~unknown")
                    {
                        var edgeMeta = new Dictionary<string, string> { ["code.edge"] = "call" };
                        edges.Add(new { sourceId = p.Id, targetId = c.Target, label = "call", metadata = edgeMeta });
                    }
                }
            }
        }

        if (mode.Equals("event-wiring", StringComparison.OrdinalIgnoreCase))
        {
            nodes.Clear(); edges.Clear(); containers.Clear();
            var formModules = root.Project.Modules.Where(m => string.Equals(m.Kind, "Form", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var m in formModules)
            {
                containers.Add(new { id = m.Id, label = m.Name, tier = "Forms" });
                foreach (var p in m.Procedures ?? new())
                {
                    if (string.IsNullOrWhiteSpace(p.Name)) continue;
                    var mm = Regex.Match(p.Name, @"^(?<ctl>[A-Za-z_][A-Za-z0-9_]*)_(?<evt>[A-Za-z_][A-Za-z0-9_]*)$");
                    if (!mm.Success) continue;
                    var ctl = mm.Groups["ctl"].Value;
                    var srcId = m.Id + "." + ctl;
                    // Source (control) node
                    nodes.Add(new { id = srcId, label = srcId, tier = "Forms", containerId = m.Id });
                    // Handler node
                    nodes.Add(new { id = p.Id, label = p.Id, tier = "Forms", containerId = m.Id });
                    var meta = new Dictionary<string, string> { ["code.edge"] = "event" };
                    edges.Add(new { sourceId = srcId, targetId = p.Id, label = mm.Groups["evt"].Value, metadata = meta });
                }
            }
        }

        if (mode.Equals("proc-cfg", StringComparison.OrdinalIgnoreCase))
        {
            nodes.Clear(); edges.Clear(); containers.Clear();
            foreach (var m in root.Project.Modules)
            {
                var tier = TierFor(m.Kind ?? "Module");
                foreach (var p in m.Procedures ?? new())
                {
                    // Container per procedure
                    var contId = p.Id + "#proc";
                    containers.Add(new { id = contId, label = p.Id, tier });
                    // Sequence nodes: Start -> each call -> End (MVP CFG)
                    var startId = p.Id + "#start";
                    nodes.Add(new { id = startId, label = "Start", tier, containerId = contId });
                    string prev = startId;
                    bool hasIf = (p.Tags?.Contains("hasIf") ?? false);
                    bool hasLoop = (p.Tags?.Contains("hasLoop") ?? false);
                    if (hasIf && hasLoop)
                    {
                        var loopId = p.Id + "#loop";
                        nodes.Add(new { id = loopId, label = "Loop", tier, containerId = contId });
                        edges.Add(new { sourceId = prev, targetId = loopId, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });

                        var decId = p.Id + "#dec";
                        nodes.Add(new { id = decId, label = "Decision", tier, containerId = contId });
                        edges.Add(new { sourceId = loopId, targetId = decId, label = "iter", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });

                        var thenId = p.Id + "#then";
                        nodes.Add(new { id = thenId, label = "Then", tier, containerId = contId });
                        edges.Add(new { sourceId = decId, targetId = thenId, label = "True", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });

                        string thenPrev = thenId;
                        foreach (var c in p.Calls ?? new())
                        {
                            if (string.IsNullOrWhiteSpace(c.Target) || c.Target == "~unknown") continue;
                            var nid = p.Id + "#call:" + c.Target;
                            nodes.Add(new { id = nid, label = c.Target, tier, containerId = contId });
                            edges.Add(new { sourceId = thenPrev, targetId = nid, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                            thenPrev = nid;
                        }

                        edges.Add(new { sourceId = thenPrev, targetId = loopId, label = "back", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });

                        var endId = p.Id + "#end";
                        nodes.Add(new { id = endId, label = "End", tier, containerId = contId });
                        edges.Add(new { sourceId = decId, targetId = endId, label = "False", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                        edges.Add(new { sourceId = loopId, targetId = endId, label = "exit", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                    }
                    else if (hasIf)
                    {
                        var decId = p.Id + "#dec";
                        nodes.Add(new { id = decId, label = "Decision", tier, containerId = contId });
                        edges.Add(new { sourceId = prev, targetId = decId, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                        var thenId = p.Id + "#then";
                        nodes.Add(new { id = thenId, label = "Then", tier, containerId = contId });
                        edges.Add(new { sourceId = decId, targetId = thenId, label = "True", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                        prev = decId; // for false path to End
                        // Represent calls along then path only for MVP
                        string thenPrev = thenId;
                        foreach (var c in p.Calls ?? new())
                        {
                            if (string.IsNullOrWhiteSpace(c.Target) || c.Target == "~unknown") continue;
                            var nid = p.Id + "#call:" + c.Target;
                            nodes.Add(new { id = nid, label = c.Target, tier, containerId = contId });
                            edges.Add(new { sourceId = thenPrev, targetId = nid, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                            thenPrev = nid;
                        }
                        var endId = p.Id + "#end";
                        nodes.Add(new { id = endId, label = "End", tier, containerId = contId });
                        edges.Add(new { sourceId = thenPrev, targetId = endId, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                        edges.Add(new { sourceId = prev, targetId = endId, label = "False", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                    }
                    else if (hasLoop)
                    {
                        var loopId = p.Id + "#loop";
                        nodes.Add(new { id = loopId, label = "Loop", tier, containerId = contId });
                        edges.Add(new { sourceId = prev, targetId = loopId, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                        string loopPrev = loopId;
                        foreach (var c in p.Calls ?? new())
                        {
                            if (string.IsNullOrWhiteSpace(c.Target) || c.Target == "~unknown") continue;
                            var nid = p.Id + "#call:" + c.Target;
                            nodes.Add(new { id = nid, label = c.Target, tier, containerId = contId });
                            edges.Add(new { sourceId = loopPrev, targetId = nid, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                            loopPrev = nid;
                        }
                        edges.Add(new { sourceId = loopPrev, targetId = loopId, label = "back", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                        var endId = p.Id + "#end";
                        nodes.Add(new { id = endId, label = "End", tier, containerId = contId });
                        edges.Add(new { sourceId = loopId, targetId = endId, label = "exit", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                    }
                    else
                    {
                        foreach (var c in p.Calls ?? new())
                        {
                            if (string.IsNullOrWhiteSpace(c.Target) || c.Target == "~unknown") continue;
                            var nid = p.Id + "#call:" + c.Target;
                            nodes.Add(new { id = nid, label = c.Target, tier, containerId = contId });
                            edges.Add(new { sourceId = prev, targetId = nid, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                            prev = nid;
                        }
                        var endId = p.Id + "#end";
                        nodes.Add(new { id = endId, label = "End", tier, containerId = contId });
                        edges.Add(new { sourceId = prev, targetId = endId, label = "seq", metadata = new Dictionary<string, string> { ["code.edge"] = "flow" } });
                    }
                }
            }
        }

        if (mode.Equals("module-callmap", StringComparison.OrdinalIgnoreCase))
        {
            // Replace with module-level call map
            nodes.Clear(); edges.Clear(); containers.Clear();
            var modKinds = root.Project.Modules.ToDictionary(m => m.Id, m => m.Kind ?? "Module", StringComparer.OrdinalIgnoreCase);
            foreach (var m in root.Project.Modules)
                nodes.Add(new { id = m.Id, label = m.Name, tier = TierFor(m.Kind ?? "Module") });
            var agg = new Dictionary<(string src, string dst), int>();
            foreach (var m in root.Project.Modules)
            {
                foreach (var p in m.Procedures ?? new())
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

        var diagram = new
        {
            schemaVersion = "1.2",
            layout = new
            {
                tiers,
                spacing = new { horizontal = 1.2, vertical = 0.6 },
                page = new { heightIn = 8.5, marginIn = 0.5 },
                containers = new { paddingIn = 0.0, cornerIn = 0.12 }
            },
            nodes,
            edges,
            containers
        };

        var json = JsonSerializer.Serialize(diagram, JsonOpts);
        if (string.IsNullOrWhiteSpace(output)) Console.WriteLine(json);
        else { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!)) ?? "."); File.WriteAllText(output!, json); }
        return ExitCodes.Ok;
    }

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

    private static List<ProcedureIr> ParseProcedures(string[] lines, string moduleName, string filePath, HashSet<string> moduleSet, Dictionary<string, string?> returnTypeMap)
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
            bool hasIf = false, hasLoop = false;
            for (int k = i; k < Math.Min(lines.Length, end - 1); k++)
            {
                var line = lines[k];
                var seenLineCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void RecordCall(string target, bool isDynamic = false)
                {
                    if (string.IsNullOrWhiteSpace(target)) return;
                    var key = $"{target}|{isDynamic}";
                    if (!seenLineCalls.Add(key)) return;
                    calls.Add(new CallIr { Target = target, IsDynamic = isDynamic, Site = new SiteIr { Module = moduleName, File = filePath, Line = k + 1 } });
                }

                if (!hasIf && Regex.IsMatch(lines[k], @"^\s*If\b", RegexOptions.IgnoreCase)) hasIf = true;
                if (!hasLoop && Regex.IsMatch(lines[k], @"^\s*(For\b|Do\b|While\b)", RegexOptions.IgnoreCase)) hasLoop = true;
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

            var proc = new ProcedureIr
            {
                Id = moduleName + "." + name,
                Name = name,
                Kind = kind,
                Access = access,
                Static = isStatic,
                Params = @params,
                Returns = returns,
                Locs = new LocsIr { File = filePath, StartLine = start, EndLine = end },
                Calls = calls,
                Metrics = new MetricsIr { Lines = end - start + 1 },
                Tags = new List<string>()
            };
            if (hasIf) proc.Tags.Add("hasIf");
            if (hasLoop) proc.Tags.Add("hasLoop");
            result.Add(proc);
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
        var r = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(r, StringComparison.OrdinalIgnoreCase) ? path.Substring(r.Length) : path;
    }

    private static int RunRender(string[] args)
    {
        string? inputFolder = null; string? outVsdx = null; string mode = "callgraph"; string? cliPath = null; string? diagramJson = null;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--in", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--in requires folder"); inputFolder = args[++i]; }
            else if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--out requires .vsdx"); outVsdx = args[++i]; }
            else if (string.Equals(a, "--mode", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--mode requires callgraph|module-structure|module-callmap"); mode = args[++i]; }
            else if (string.Equals(a, "--cli", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--cli requires path to VDG.CLI.exe"); cliPath = args[++i]; }
            else if (string.Equals(a, "--diagram-json", StringComparison.OrdinalIgnoreCase)) { if (i + 1 >= args.Length) throw new UsageException("--diagram-json requires path"); diagramJson = args[++i]; }
            else throw new UsageException($"Unknown option '{a}' for render.");
        }
        if (string.IsNullOrWhiteSpace(inputFolder) || string.IsNullOrWhiteSpace(outVsdx)) throw new UsageException("render requires --in <folder> and --out <diagram.vsdx>");
        var irPath = Path.Combine(Path.GetTempPath(), $"vdg_ir_{Guid.NewGuid():N}.json");
        var diagPath = diagramJson ?? Path.Combine(Path.GetTempPath(), $"vdg_diag_{Guid.NewGuid():N}.json");
        RunVba2Json(new[] { "--in", inputFolder!, "--out", irPath });
        RunIr2Diagram(new[] { "--in", irPath, "--out", diagPath, "--mode", mode });

        var cli = FindCli(cliPath);
        if (string.IsNullOrWhiteSpace(cli)) throw new UsageException("VDG.CLI.exe not found. Provide --cli <path> or set VDG_CLI env.");
        var psi = new System.Diagnostics.ProcessStartInfo(cli!, $"\"{diagPath}\" \"{outVsdx}\"") { UseShellExecute = false };
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
    [JsonPropertyName("irSchemaVersion")] public string IrSchemaVersion { get; set; } = "0.1";
    [JsonPropertyName("generator")] public GeneratorIr Generator { get; set; } = new();
    [JsonPropertyName("project")] public ProjectIr Project { get; set; } = new();
}
internal sealed class GeneratorIr { public string? Name { get; set; } public string? Version { get; set; } }
internal sealed class ProjectIr { public string Name { get; set; } = ""; public List<ModuleIr> Modules { get; set; } = new(); }
internal sealed class ModuleIr { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string? Kind { get; set; } public string? File { get; set; } public List<ProcedureIr> Procedures { get; set; } = new(); }
internal sealed class ProcedureIr { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string? Kind { get; set; } public string? Access { get; set; } public bool Static { get; set; } public List<ParamIr> Params { get; set; } = new(); public string? Returns { get; set; } public LocsIr Locs { get; set; } = new(); public List<CallIr> Calls { get; set; } = new(); public MetricsIr Metrics { get; set; } = new(); public List<string> Tags { get; set; } = new(); }
internal sealed class ParamIr { public string Name { get; set; } = ""; public string? Type { get; set; } public bool ByRef { get; set; } }
internal sealed class LocsIr { public string File { get; set; } = ""; public int StartLine { get; set; } public int EndLine { get; set; } }
internal sealed class CallIr { public string Target { get; set; } = ""; public bool IsDynamic { get; set; } public SiteIr Site { get; set; } = new(); }
internal sealed class SiteIr { public string Module { get; set; } = ""; public string File { get; set; } = ""; public int Line { get; set; } }
internal sealed class MetricsIr { public int? Cyclomatic { get; set; } public int? Lines { get; set; } }
