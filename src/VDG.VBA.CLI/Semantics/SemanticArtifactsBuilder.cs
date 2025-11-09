using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VDG.VBA.CLI.Semantics
{
    internal sealed class SemanticArtifactsBuilder
    {
        private static readonly string[] EventSuffixes =
        {
            "_Click", "_Change", "_Initialize", "_Terminate", "_BeforeUpdate", "_AfterUpdate",
            "_Activate", "_Deactivate", "_MouseDown", "_MouseUp", "_MouseMove", "_KeyDown",
            "_KeyUp", "_KeyPress", "_BeforeSave", "_Open", "_Close"
        };

        private static readonly (string Token, string Subsystem)[] SubsystemKeywordHints =
        {
            ("Import", "Data.ImportExport"),
            ("Export", "Data.ImportExport"),
            ("Snapshot", "Domain.Inventory"),
            ("Undo", "Domain.Inventory"),
            ("Inventory", "Domain.Inventory"),
            ("Auth", "Security.Auth"),
            ("Login", "Security.Auth"),
            ("Log", "Observability.Logging"),
            ("Telemetry", "Observability.Logging"),
            ("Worksheet", "UI.Sheets"),
            ("Sheet", "UI.Sheets"),
            ("Form", "UI.Forms"),
            ("UI", "UI.Forms"),
            ("Globals", "Infrastructure.Core"),
            ("Config", "Infrastructure.Core")
        };

        private static readonly Dictionary<string, string> SubsystemDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["UI.Forms"] = "User-facing VBA forms and controls.",
            ["UI.Sheets"] = "Worksheet code-behind and Excel event glue.",
            ["Domain.Inventory"] = "Inventory-specific orchestration and calculations.",
            ["Domain.TransactionSets"] = "Transaction-set launchers and tally flows.",
            ["Data.ImportExport"] = "Import/export pipelines and file adapters.",
            ["Observability.Logging"] = "Diagnostics, telemetry, and logging helpers.",
            ["Security.Auth"] = "Authentication and authorization helpers.",
            ["Infrastructure.Core"] = "Shared infrastructure, globals, and utilities.",
            ["Core.Modules"] = "Modules without a tighter subsystem classification."
        };

        private static readonly Dictionary<string, string> RoleDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EventHandler"] = "Responds directly to UI or workbook events.",
            ["Validator"] = "Guards invariants or validates inputs.",
            ["DataLoader"] = "Reads or hydrates data from storage.",
            ["Persistence"] = "Writes or persists data/state.",
            ["Initializer"] = "Bootstraps modules, forms, or long-lived flows.",
            ["Coordinator"] = "Dispatches or orchestrates other procedures.",
            ["Utility"] = "General-purpose helper or calculation."
        };

        private static readonly Dictionary<string, string> SubsystemTeams = new(StringComparer.OrdinalIgnoreCase)
        {
            ["UI.Forms"] = "UI",
            ["UI.Sheets"] = "UI",
            ["Domain.Inventory"] = "Inventory",
            ["Domain.TransactionSets"] = "Inventory",
            ["Data.ImportExport"] = "Data",
            ["Observability.Logging"] = "Telemetry",
            ["Security.Auth"] = "Security",
            ["Infrastructure.Core"] = "Platform",
            ["Core.Modules"] = "Core"
        };

        public SemanticArtifacts Build(IEnumerable<ModuleIr>? modules, string? projectName, string? sourceIrPath, DateTimeOffset? generatedAt = null)
        {
            var materializedModules = (modules ?? Array.Empty<ModuleIr>())
                .Where(static m => m is not null)
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var timestamp = generatedAt ?? DateTimeOffset.UtcNow;

            var moduleSemanticMap = new Dictionary<string, ModuleSemanticInfo>(StringComparer.OrdinalIgnoreCase);
            var procedureSemanticMap = new Dictionary<string, ProcedureSemanticInfo>(StringComparer.OrdinalIgnoreCase);
            var taxonomyModules = new List<TaxonomyModuleRecord>();
            var unresolved = new List<TaxonomyUnresolvedRecord>();
            var legendSubsystems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var legendRoles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var module in materializedModules)
            {
                if (string.IsNullOrWhiteSpace(module.Id))
                    continue;

                var tier = DetermineTier(module);
                var subsystem = ClassifySubsystem(module);
                var tags = InferModuleTags(subsystem.Primary);

                var ownership = new OwnershipInfo
                {
                    Team = SubsystemTeams.TryGetValue(subsystem.Primary, out var team) ? team : "Core",
                    Reviewer = null
                };

                var evidence = BuildModuleEvidence(module);
                var moduleRecord = new TaxonomyModuleRecord
                {
                    Id = module.Id,
                    Name = string.IsNullOrWhiteSpace(module.Name) ? module.Id : module.Name!,
                    Kind = module.Kind,
                    File = module.File,
                    Tier = tier,
                    Subsystem = subsystem,
                    Ownership = ownership,
                    Tags = tags,
                    Procedures = new List<TaxonomyProcedureRecord>(),
                    Roles = new List<string>(),
                    Evidence = evidence
                };

                taxonomyModules.Add(moduleRecord);
                moduleSemanticMap[module.Id] = new ModuleSemanticInfo(
                    module.Id,
                    subsystem.Primary,
                    subsystem.Secondary.ToArray(),
                    subsystem.Confidence,
                    tags.ToArray());

                legendSubsystems[subsystem.Primary] = DescribeSubsystem(subsystem.Primary);
                foreach (var secondary in subsystem.Secondary)
                    legendSubsystems[secondary] = DescribeSubsystem(secondary);

                var procedures = (module.Procedures ?? new List<ProcedureIr>())
                    .Where(static p => p is not null && !string.IsNullOrWhiteSpace(p.Id))
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (procedures.Count == 0)
                {
                    unresolved.Add(new TaxonomyUnresolvedRecord
                    {
                        Target = module.Id,
                        Reason = "Module contains no procedures",
                        SuggestedAction = "Confirm module should be part of taxonomy"
                    });
                }

                var moduleRoleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var procedure in procedures)
                {
                    var procClassification = ClassifyProcedure(module, procedure);
                    var record = new TaxonomyProcedureRecord
                    {
                        Id = procedure.Id,
                        Name = string.IsNullOrWhiteSpace(procedure.Name) ? procedure.Id : procedure.Name!,
                        Role = procClassification.Role,
                        Capabilities = procClassification.Capabilities,
                        Resources = procClassification.Resources,
                        Notes = procClassification.Notes
                    };

                    moduleRecord.Procedures.Add(record);
                    if (!string.IsNullOrWhiteSpace(procClassification.Role.Primary))
                        moduleRoleSet.Add(procClassification.Role.Primary);

                    var procedureSemantic = new ProcedureSemanticInfo(
                        procedure.Id,
                        procClassification.Role.Primary,
                        procClassification.Role.Secondary.ToArray(),
                        procClassification.Capabilities.FirstOrDefault(),
                        procClassification.Role.Confidence,
                        subsystem.Primary);

                    procedureSemanticMap[procedure.Id] = procedureSemantic;

                    legendRoles[procClassification.Role.Primary] = DescribeRole(procClassification.Role.Primary);
                    foreach (var secondary in procClassification.Role.Secondary)
                        legendRoles[secondary] = DescribeRole(secondary);

                    if (string.Equals(procClassification.Role.Primary, "Utility", StringComparison.OrdinalIgnoreCase))
                    {
                        unresolved.Add(new TaxonomyUnresolvedRecord
                        {
                            Target = procedure.Id,
                            Reason = "Procedure classified as generic utility",
                            SuggestedAction = "Review role heuristics or add overrides"
                        });
                    }
                }

                var orderedRoles = moduleRoleSet
                    .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                moduleRecord.Roles.Clear();
                foreach (var role in orderedRoles)
                    moduleRecord.Roles.Add(role);
            }

            var legend = new TaxonomyLegend
            {
                Subsystems = legendSubsystems
                    .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kv => new LegendEntry { Id = kv.Key, Description = kv.Value })
                    .ToList(),
                Roles = legendRoles
                    .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kv => new LegendEntry { Id = kv.Key, Description = kv.Value })
                    .ToList()
            };

            var taxonomy = new TaxonomyArtifact
            {
                GeneratedAt = timestamp,
                Project = new TaxonomyProjectInfo
                {
                    Name = string.IsNullOrWhiteSpace(projectName) ? "VBA Project" : projectName!,
                    SourceIr = sourceIrPath
                },
                Legend = legend,
                Modules = taxonomyModules,
                Unresolved = unresolved
            };

            var flow = BuildFlowArtifact(
                materializedModules,
                projectName,
                sourceIrPath,
                timestamp,
                moduleSemanticMap,
                procedureSemanticMap);

            return new SemanticArtifacts(taxonomy, flow, moduleSemanticMap, procedureSemanticMap);
        }

        private static FlowArtifact BuildFlowArtifact(
            IReadOnlyList<ModuleIr> modules,
            string? projectName,
            string? sourceIrPath,
            DateTimeOffset generatedAt,
            IReadOnlyDictionary<string, ModuleSemanticInfo> moduleSemantics,
            IReadOnlyDictionary<string, ProcedureSemanticInfo> procedureSemantics)
        {
            var residuals = new List<FlowResidual>();
            var procedureLookup = modules
                .Where(static m => m is not null)
                .SelectMany(m => (m.Procedures ?? new List<ProcedureIr>())
                    .Where(static p => p is not null && !string.IsNullOrWhiteSpace(p.Id))
                    .Select(p => (Module: m, Procedure: p)))
                .ToDictionary(x => x.Procedure.Id, x => x, StringComparer.OrdinalIgnoreCase);

            var buckets = new Dictionary<string, FlowBucket>(StringComparer.OrdinalIgnoreCase);

            foreach (var module in modules)
            {
                if (module?.Procedures == null) continue;

                foreach (var procedure in module.Procedures)
                {
                    if (procedure?.Calls == null || procedure.Calls.Count == 0) continue;
                    if (string.IsNullOrWhiteSpace(procedure.Id)) continue;

                    foreach (var call in procedure.Calls)
                    {
                        if (call == null) continue;

                        if (string.IsNullOrWhiteSpace(call.Target))
                        {
                            residuals.Add(new FlowResidual
                            {
                                Description = $"Flow from {procedure.Id} missing target",
                                Cause = "Call target omitted",
                                SuggestedAction = "Verify parser captured target procedure"
                            });
                            continue;
                        }

                        if (call.Target.Equals("~unknown", StringComparison.OrdinalIgnoreCase))
                        {
                            residuals.Add(new FlowResidual
                            {
                                Description = $"Flow from {procedure.Id} -> ~unknown",
                                Cause = "Dynamic call unresolved",
                                SuggestedAction = "Run ir2diagram with --include-unknown or add resolver"
                            });
                            continue;
                        }

                        if (!procedureLookup.TryGetValue(call.Target, out var destination))
                        {
                            residuals.Add(new FlowResidual
                            {
                                Description = $"Flow from {procedure.Id} -> {call.Target}",
                                Cause = "Target procedure not present in IR",
                                SuggestedAction = "Ensure module was exported and parsed"
                            });
                            continue;
                        }

                        var bucketId = $"{procedure.Id}->{call.Target}";
                        if (!buckets.TryGetValue(bucketId, out var bucket))
                        {
                            var sourceModuleInfo = moduleSemantics.TryGetValue(module.Id, out var srcModule)
                                ? srcModule
                                : new ModuleSemanticInfo(module.Id, "Core.Modules", Array.Empty<string>(), 0.3, Array.Empty<string>());
                            var targetModuleInfo = moduleSemantics.TryGetValue(destination.Module.Id, out var dstModule)
                                ? dstModule
                                : new ModuleSemanticInfo(destination.Module.Id, "Core.Modules", Array.Empty<string>(), 0.3, Array.Empty<string>());

                            procedureSemantics.TryGetValue(procedure.Id, out var sourceProcInfo);
                            procedureSemantics.TryGetValue(destination.Procedure.Id, out var targetProcInfo);

                            bucket = new FlowBucket(
                                bucketId,
                                procedure.Id,
                                destination.Procedure.Id,
                                module.Id,
                                destination.Module.Id,
                                sourceProcInfo,
                                targetProcInfo,
                                sourceModuleInfo,
                                targetModuleInfo);
                            buckets.Add(bucketId, bucket);
                        }

                        bucket.AddCall(call);
                    }
                }
            }

            var flows = buckets.Values
                .OrderBy(b => b.Id, StringComparer.OrdinalIgnoreCase)
                .Select(bucket => bucket.ToRecord())
                .ToList();

            return new FlowArtifact
            {
                GeneratedAt = generatedAt,
                Project = new TaxonomyProjectInfo
                {
                    Name = string.IsNullOrWhiteSpace(projectName) ? "VBA Project" : projectName!,
                    SourceIr = sourceIrPath
                },
                Flows = flows,
                Residuals = residuals
                    .OrderBy(r => r.Description, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private static string DescribeFlowPurpose(string? targetRole) =>
            string.IsNullOrWhiteSpace(targetRole)
                ? "Call sequence"
                : targetRole switch
                {
                    "Validator" => "Validate upstream input",
                    "Persistence" => "Persist data or state",
                    "DataLoader" => "Load or hydrate data",
                    "Initializer" => "Bootstrap downstream module",
                    _ => "Call sequence"
                };

        private static double DetermineConfidence(double? source, double? target)
        {
            var values = new List<double>();
            if (source.HasValue && source.Value > 0) values.Add(source.Value);
            if (target.HasValue && target.Value > 0) values.Add(target.Value);
            if (values.Count == 0) return 0.4;
            return Math.Round(values.Min(), 2, MidpointRounding.AwayFromZero);
        }

        private static IDictionary<string, string> BuildSampleMetadata(CallIr call)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["code.edge"] = "call"
            };

            if (call.IsDynamic)
            {
                metadata["code.dynamic"] = "true";
            }

            if (!string.IsNullOrWhiteSpace(call.Branch))
            {
                metadata["code.branch"] = call.Branch!;
            }

            if (!string.IsNullOrWhiteSpace(call.Site?.Module))
            {
                metadata["code.site.module"] = call.Site.Module!;
            }

            if (!string.IsNullOrWhiteSpace(call.Site?.File))
            {
                metadata["code.site.file"] = call.Site.File!;
            }

            if (call.Site?.Line > 0)
            {
                metadata["code.site.line"] = call.Site.Line.ToString(CultureInfo.InvariantCulture);
            }

            return metadata;
        }

        private sealed class FlowBucket
        {
            private const int SampleLimit = 5;
            private readonly List<FlowSampleCall> _samples = new();
            private readonly List<string> _reasons = new();

            public FlowBucket(
                string id,
                string sourceProcId,
                string targetProcId,
                string sourceModuleId,
                string targetModuleId,
                ProcedureSemanticInfo? sourceProcedure,
                ProcedureSemanticInfo? targetProcedure,
                ModuleSemanticInfo sourceModule,
                ModuleSemanticInfo targetModule)
            {
                Id = id;
                SourceProcId = sourceProcId;
                TargetProcId = targetProcId;
                SourceModuleId = sourceModuleId;
                TargetModuleId = targetModuleId;
                SourceProcedure = sourceProcedure;
                TargetProcedure = targetProcedure;
                SourceModule = sourceModule;
                TargetModule = targetModule;

                if (!string.IsNullOrWhiteSpace(sourceProcedure?.PrimaryRole))
                {
                    _reasons.Add($"source role={sourceProcedure.PrimaryRole}");
                }
                if (!string.IsNullOrWhiteSpace(targetProcedure?.PrimaryRole))
                {
                    _reasons.Add($"target role={targetProcedure.PrimaryRole}");
                }
            }

            public string Id { get; }
            public string SourceProcId { get; }
            public string TargetProcId { get; }
            public string SourceModuleId { get; }
            public string TargetModuleId { get; }
            public ProcedureSemanticInfo? SourceProcedure { get; }
            public ProcedureSemanticInfo? TargetProcedure { get; }
            public ModuleSemanticInfo SourceModule { get; }
            public ModuleSemanticInfo TargetModule { get; }
            public int EdgeCount { get; private set; }

            public void AddCall(CallIr call)
            {
                EdgeCount++;
                if (_samples.Count < SampleLimit)
                {
                    _samples.Add(new FlowSampleCall
                    {
                        From = SourceProcId,
                        To = TargetProcId,
                        Site = new FlowSampleSite
                        {
                            File = call.Site?.File,
                            Line = call.Site?.Line
                        },
                        Branch = string.IsNullOrWhiteSpace(call.Branch) ? null : call.Branch,
                        Metadata = BuildSampleMetadata(call)
                    });
                }
            }

            public FlowRecord ToRecord()
            {
                var sourceRole = SourceProcedure?.PrimaryRole ?? "Utility";
                var targetRole = TargetProcedure?.PrimaryRole ?? "Utility";
                var sourceSubsystem = SourceProcedure?.PrimarySubsystem ?? SourceModule.PrimarySubsystem ?? "Core.Modules";
                var targetSubsystem = TargetProcedure?.PrimarySubsystem ?? TargetModule.PrimarySubsystem ?? "Core.Modules";

                return new FlowRecord
                {
                    Id = $"{SourceProcId}->{TargetProcId}",
                    Channel = $"{sourceSubsystem}->{targetSubsystem}",
                    Purpose = DescribeFlowPurpose(targetRole),
                    Source = new FlowEndpoint
                    {
                        ModuleId = SourceModuleId,
                        ProcedureId = SourceProcId,
                        Role = sourceRole,
                        Subsystem = sourceSubsystem
                    },
                    Target = new FlowEndpoint
                    {
                        ModuleId = TargetModuleId,
                        ProcedureId = TargetProcId,
                        Role = targetRole,
                        Subsystem = targetSubsystem
                    },
                    EdgeCount = EdgeCount,
                    SampleCalls = _samples
                        .OrderBy(s => s.Site.Line ?? int.MaxValue)
                        .ToList(),
                    SharedResources = new List<SharedResource>(),
                    Confidence = DetermineConfidence(SourceProcedure?.Confidence, TargetProcedure?.Confidence),
                    Reasons = _reasons
                        .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            }
        }

        private static SubsystemClassification ClassifySubsystem(ModuleIr module)
        {
            var hints = new List<(string Id, double Confidence, string Reason)>();

            if (string.Equals(module.Kind, "Form", StringComparison.OrdinalIgnoreCase) ||
                (module.Name?.StartsWith("frm", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (module.File?.Contains("Forms", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                hints.Add(("UI.Forms", 0.9, "module.kind=Form"));
            }

            if (LooksLikeSheetModule(module))
            {
                hints.Add(("UI.Sheets", 0.85, "module resembles worksheet code-behind"));
            }

            if (module.Name?.StartsWith("modTS_", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                hints.Add(("Domain.TransactionSets", 0.82, "name prefixed with modTS_"));
            }

            foreach (var (token, subsystem) in SubsystemKeywordHints)
            {
                if ((module.Name?.IndexOf(token, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (module.File?.IndexOf(token, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                {
                    hints.Add((subsystem, 0.7, $"name/file contains '{token}'"));
                }
            }

            if (!hints.Any())
            {
                hints.Add(("Core.Modules", 0.35, "default classification"));
            }

            var best = hints
                .OrderByDescending(h => h.Confidence)
                .ThenBy(h => h.Id, StringComparer.OrdinalIgnoreCase)
                .First();

            var secondary = hints
                .Where(h => !string.Equals(h.Id, best.Id, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.Confidence)
                .ThenBy(h => h.Id, StringComparer.OrdinalIgnoreCase)
                .Select(h => h.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var reasons = hints
                .Where(h => string.Equals(h.Id, best.Id, StringComparison.OrdinalIgnoreCase) || secondary.Contains(h.Id))
                .Select(h => h.Reason)
                .Distinct()
                .ToList();

            return new SubsystemClassification
            {
                Primary = best.Id,
                Secondary = secondary,
                Confidence = Math.Round(best.Confidence, 2, MidpointRounding.AwayFromZero),
                Reasons = reasons
            };
        }

        private static TaxonomyProcedureRecord ClassifyProcedure(ModuleIr module, ProcedureIr procedure)
        {
            var role = DetermineRole(procedure);
            var capabilities = InferCapabilities(module, procedure, role.Primary);
            var resources = InferResources(procedure);
            string? notes = null;

            if (procedure.Tags?.Contains("hasLoop", StringComparer.OrdinalIgnoreCase) ?? false)
            {
                notes = "Procedure contains loop structures";
            }

            return new TaxonomyProcedureRecord
            {
                Id = procedure.Id,
                Name = string.IsNullOrWhiteSpace(procedure.Name) ? procedure.Id : procedure.Name!,
                Role = role,
                Capabilities = capabilities,
                Resources = resources,
                Notes = notes
            };
        }

        private static RoleClassification DetermineRole(ProcedureIr procedure)
        {
            var reasons = new List<string>();
            var secondary = new List<string>();
            string primary = "Utility";
            double confidence = 0.45;

            var name = procedure.Name ?? procedure.Id ?? string.Empty;

            if (EventSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                primary = "EventHandler";
                confidence = 0.9;
                reasons.Add("name matches common event suffix");
            }
            else if (ContainsAny(name, "Validate", "Ensure", "Check"))
            {
                primary = "Validator";
                confidence = 0.82;
                reasons.Add("name contains validation keyword");
            }
            else if (ContainsAny(name, "Save", "Persist", "Write", "Export"))
            {
                primary = "Persistence";
                confidence = 0.8;
                reasons.Add("name contains persistence keyword");
            }
            else if (ContainsAny(name, "Load", "Fetch", "Read", "Import", "Sync"))
            {
                primary = "DataLoader";
                confidence = 0.75;
                reasons.Add("name contains data load keyword");
            }
            else if (ContainsAny(name, "Init", "Initialize", "Setup", "Create", "Open"))
            {
                primary = "Initializer";
                confidence = 0.72;
                reasons.Add("name contains initialization keyword");
            }
            else if (ContainsAny(name, "Handle", "Dispatch", "Route", "Process"))
            {
                primary = "Coordinator";
                confidence = 0.7;
                reasons.Add("name contains orchestration keyword");
            }

            if ((procedure.Tags?.Contains("hasLoop", StringComparer.OrdinalIgnoreCase) ?? false) &&
                !string.Equals(primary, "Coordinator", StringComparison.OrdinalIgnoreCase))
            {
                secondary.Add("Coordinator");
            }

            if (ContainsAny(name, "Validate", "Ensure", "Check") &&
                !string.Equals(primary, "Validator", StringComparison.OrdinalIgnoreCase))
            {
                secondary.Add("Validator");
            }

            return new RoleClassification
            {
                Primary = primary,
                Secondary = secondary
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Confidence = Math.Round(confidence, 2, MidpointRounding.AwayFromZero),
                Reasons = reasons
            };
        }

        private static IList<string> InferCapabilities(ModuleIr module, ProcedureIr procedure, string primaryRole)
        {
            var capabilities = new List<string>();
            var name = procedure.Name ?? procedure.Id ?? string.Empty;
            var moduleName = module.Name ?? module.Id ?? string.Empty;

            void AddCapability(string id)
            {
                if (!capabilities.Contains(id, StringComparer.OrdinalIgnoreCase))
                    capabilities.Add(id);
            }

            if (name.IndexOf("Login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                moduleName.IndexOf("Auth", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddCapability("Auth.Login");
            }

            if (moduleName.StartsWith("modTS_", StringComparison.OrdinalIgnoreCase))
            {
                AddCapability("Inventory.TransactionSets");
            }

            if (ContainsAny(name, "Import", "Export"))
            {
                AddCapability("Data.Exchange");
            }

            if (string.Equals(primaryRole, "EventHandler", StringComparison.OrdinalIgnoreCase))
            {
                AddCapability("UI.EventRouting");
            }

            return capabilities
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IList<string> InferResources(ProcedureIr procedure)
        {
            var resources = new List<string>();
            if (procedure.Params != null && procedure.Params.Count > 3)
            {
                resources.Add("Parameters.Heavy");
            }

            if (procedure.Metrics?.Cyclomatic is { } cyclomatic and > 10)
            {
                resources.Add("Complexity.High");
            }

            return resources
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string DetermineTier(ModuleIr module)
        {
            if (module == null) return "Modules";
            if (string.Equals(module.Kind, "Form", StringComparison.OrdinalIgnoreCase)) return "Forms";

            if (string.Equals(module.Kind, "Class", StringComparison.OrdinalIgnoreCase))
            {
                if (LooksLikeSheetModule(module)) return "Sheets";
                return "Classes";
            }

            return "Modules";
        }

        private static bool LooksLikeSheetModule(ModuleIr module)
        {
            var candidates = new List<string?>();
            candidates.Add(module.Name);
            candidates.Add(module.Id);
            if (!string.IsNullOrWhiteSpace(module.File))
            {
                try
                {
                    candidates.Add(System.IO.Path.GetFileNameWithoutExtension(module.File));
                }
                catch
                {
                    candidates.Add(module.File);
                }
            }

            return candidates.Any(LooksLikeSheetName);
        }

        private static bool LooksLikeSheetName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.StartsWith("Sheet", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("Chart", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("ThisWorkbook", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("ThisDocument", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> InferModuleTags(string primarySubsystem)
        {
            var tags = new List<string>();
            switch (primarySubsystem)
            {
                case "UI.Forms":
                case "UI.Sheets":
                    tags.Add("ui");
                    break;
                case "Domain.Inventory":
                case "Domain.TransactionSets":
                    tags.Add("inventory");
                    break;
                case "Data.ImportExport":
                    tags.Add("data");
                    break;
                case "Observability.Logging":
                    tags.Add("telemetry");
                    break;
                case "Security.Auth":
                    tags.Add("security");
                    break;
            }

            return tags;
        }

        private static IDictionary<string, string> BuildModuleEvidence(ModuleIr module)
        {
            var evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(module.File))
            {
                evidence["path"] = module.File!;
            }

            if (module.Metrics?.Lines is { } lines)
            {
                evidence["metrics.lines"] = lines.ToString(CultureInfo.InvariantCulture);
            }

            if (module.Metrics?.Cyclomatic is { } cyclomatic)
            {
                evidence["metrics.cyclomatic"] = cyclomatic.ToString(CultureInfo.InvariantCulture);
            }

            return evidence;
        }

        private static string DescribeSubsystem(string id) =>
            SubsystemDescriptions.TryGetValue(id, out var description)
                ? description
                : "Subsystem classification";

        private static string DescribeRole(string id) =>
            RoleDescriptions.TryGetValue(id, out var description)
                ? description
                : "Procedure role classification";

        private static bool ContainsAny(string value, params string[] tokens) =>
            tokens.Any(token => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);

        // End helper methods.
    }
}
