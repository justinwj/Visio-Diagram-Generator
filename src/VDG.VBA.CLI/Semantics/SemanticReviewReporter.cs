using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace VDG.VBA.CLI.Semantics
{
    internal sealed class SemanticReviewSummary
    {
        public SemanticReviewSummary()
        {
            SubsystemCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            RoleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Info = new List<string>();
            Warnings = new List<string>();
            Suggestions = new List<string>();
        }

        public Dictionary<string, int> SubsystemCounts { get; }
        public Dictionary<string, int> RoleCounts { get; }
        public List<string> Info { get; }
        public List<string> Warnings { get; }
        public List<string> Suggestions { get; }
        public int ModuleCount { get; set; }
        public int ProcedureCount { get; set; }
        public int FlowResidualCount { get; set; }
        public int ModulesWithoutSubsystem { get; set; }
        public int ProceduresWithoutRole { get; set; }
        public int LowConfidenceModules { get; set; }
    }

    internal static class SemanticReviewReporter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static SemanticReviewSummary Build(SemanticArtifacts artifacts)
        {
            var summary = new SemanticReviewSummary
            {
                ModuleCount = artifacts.Modules?.Count ?? 0,
                ProcedureCount = artifacts.Procedures?.Count ?? 0,
                FlowResidualCount = artifacts.Flow?.Residuals?.Count ?? 0
            };

            if (artifacts.Modules != null)
            {
                foreach (var kvp in artifacts.Modules)
                {
                    var info = kvp.Value;
                    var subsystem = string.IsNullOrWhiteSpace(info.PrimarySubsystem)
                        ? "~unspecified"
                        : info.PrimarySubsystem!;
                    Increment(summary.SubsystemCounts, subsystem);

                    if (string.IsNullOrWhiteSpace(info.PrimarySubsystem))
                    {
                        summary.ModulesWithoutSubsystem++;
                    }
                    if (info.Confidence < 0.55)
                    {
                        summary.LowConfidenceModules++;
                    }
                    if (info.Tags != null && info.Tags.Count > 0)
                    {
                        if (info.Tags.Count(tag => string.Equals(tag, "ui", StringComparison.OrdinalIgnoreCase)) > 0 &&
                            info.Tags.Count > 4)
                        {
                            summary.Suggestions.Add($"Module '{info.ModuleId}' spans {info.Tags.Count} capability tags; consider splitting responsibilities.");
                        }
                    }
                }
            }

            if (artifacts.Procedures != null)
            {
                foreach (var kvp in artifacts.Procedures)
                {
                    var proc = kvp.Value;
                    if (string.IsNullOrWhiteSpace(proc.PrimaryRole))
                    {
                        summary.ProceduresWithoutRole++;
                        continue;
                    }
                    Increment(summary.RoleCounts, proc.PrimaryRole!);
                }
            }

            summary.Info.Add($"Modules analysed: {summary.ModuleCount}");
            summary.Info.Add($"Procedures analysed: {summary.ProcedureCount}");

            if (summary.SubsystemCounts.Count > 0)
            {
                var formatted = FormatCounts(summary.SubsystemCounts);
                summary.Info.Add($"Subsystem distribution: {formatted}");
            }

            if (summary.RoleCounts.Count > 0)
            {
                summary.Info.Add($"Primary roles detected: {FormatCounts(summary.RoleCounts)}");
            }

            if (summary.ModulesWithoutSubsystem > 0)
            {
                summary.Warnings.Add($"{summary.ModulesWithoutSubsystem} module(s) missing subsystem classification.");
            }
            if (summary.LowConfidenceModules > 0)
            {
                summary.Warnings.Add($"{summary.LowConfidenceModules} module classification(s) below confidence threshold.");
            }
            if (summary.ProceduresWithoutRole > 0)
            {
                summary.Warnings.Add($"{summary.ProceduresWithoutRole} procedure(s) missing role detection.");
            }
            if (summary.FlowResidualCount > 0)
            {
                summary.Warnings.Add($"{summary.FlowResidualCount} unresolved flow(s) detected. See flows.json residuals.");
            }

            var dominantSubsystem = summary.SubsystemCounts
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(dominantSubsystem.Key) &&
                summary.ModuleCount > 0 &&
                dominantSubsystem.Value >= Math.Max(5, summary.ModuleCount / 2))
            {
                summary.Suggestions.Add($"Subsystem '{dominantSubsystem.Key}' dominates ({dominantSubsystem.Value}/{summary.ModuleCount} modules). Consider splitting or validating ownership.");
            }

            if (summary.RoleCounts.TryGetValue("EventHandler", out var handlerCount) && handlerCount > 10)
            {
                summary.Suggestions.Add($"High volume of EventHandler procedures detected ({handlerCount}). Verify UI lanes have headroom.");
            }

            return summary;
        }

        public static void EmitToConsole(SemanticReviewSummary summary)
        {
            if (summary == null) return;
            Console.WriteLine();
            Console.WriteLine("review: semantic & planner insights");
            foreach (var info in summary.Info)
            {
                Console.WriteLine($"  info: {info}");
            }
            foreach (var warning in summary.Warnings)
            {
                Console.WriteLine($"warning: {warning}");
            }
            foreach (var suggestion in summary.Suggestions)
            {
                Console.WriteLine($"suggestion: {suggestion}");
            }
        }

        public static string Serialize(SemanticReviewSummary summary)
        {
            var payload = new
            {
                modules = summary.ModuleCount,
                procedures = summary.ProcedureCount,
                subsystems = summary.SubsystemCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                roles = summary.RoleCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                warnings = summary.Warnings.ToArray(),
                suggestions = summary.Suggestions.ToArray(),
                info = summary.Info.ToArray(),
                residualFlows = summary.FlowResidualCount
            };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        public static string[] BuildTextReport(SemanticReviewSummary summary)
        {
            var lines = new List<string>
            {
                "Semantic & Planner Review Summary",
                "================================="
            };

            lines.Add($"Modules analysed : {summary.ModuleCount}");
            lines.Add($"Procedures analysed : {summary.ProcedureCount}");
            if (summary.SubsystemCounts.Count > 0)
            {
                lines.Add($"Subsystems        : {FormatCounts(summary.SubsystemCounts)}");
            }
            if (summary.RoleCounts.Count > 0)
            {
                lines.Add($"Roles             : {FormatCounts(summary.RoleCounts)}");
            }
            if (summary.Info.Count > 0)
            {
                lines.Add("");
                lines.Add("Info:");
                foreach (var info in summary.Info)
                {
                    lines.Add($"  - {info}");
                }
            }
            if (summary.Warnings.Count > 0)
            {
                lines.Add("");
                lines.Add("Warnings:");
                foreach (var warning in summary.Warnings)
                {
                    lines.Add($"  - {warning}");
                }
            }
            if (summary.Suggestions.Count > 0)
            {
                lines.Add("");
                lines.Add("Suggestions:");
                foreach (var suggestion in summary.Suggestions)
                {
                    lines.Add($"  - {suggestion}");
                }
            }

            return lines.ToArray();
        }

        private static void Increment(IDictionary<string, int> map, string key)
        {
            if (map.TryGetValue(key, out var value))
            {
                map[key] = value + 1;
            }
            else
            {
                map[key] = 1;
            }
        }

        private static string FormatCounts(IReadOnlyDictionary<string, int> counts)
        {
            return string.Join(", ",
                counts
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }
    }
}
