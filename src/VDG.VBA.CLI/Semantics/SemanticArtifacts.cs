using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VDG.VBA.CLI.Semantics
{
    internal sealed record SemanticArtifacts(
        TaxonomyArtifact Taxonomy,
        FlowArtifact Flow,
        IReadOnlyDictionary<string, ModuleSemanticInfo> Modules,
        IReadOnlyDictionary<string, ProcedureSemanticInfo> Procedures);

    internal sealed record ModuleSemanticInfo(
        string ModuleId,
        string? PrimarySubsystem,
        IReadOnlyList<string> SecondarySubsystems,
        double Confidence,
        IReadOnlyList<string> Tags);

    internal sealed record ProcedureSemanticInfo(
        string ProcedureId,
        string? PrimaryRole,
        IReadOnlyList<string> SecondaryRoles,
        string? Capability,
        double Confidence,
        string? PrimarySubsystem);

    internal sealed record TaxonomyArtifact
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("generatedAt")]
        public DateTimeOffset GeneratedAt { get; init; }

        [JsonPropertyName("project")]
        public TaxonomyProjectInfo Project { get; init; } = new();

        [JsonPropertyName("legend")]
        public TaxonomyLegend Legend { get; init; } = new();

        [JsonPropertyName("modules")]
        public IList<TaxonomyModuleRecord> Modules { get; init; } = new List<TaxonomyModuleRecord>();

        [JsonPropertyName("unresolved")]
        public IList<TaxonomyUnresolvedRecord> Unresolved { get; init; } = new List<TaxonomyUnresolvedRecord>();
    }

    internal sealed record TaxonomyProjectInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("sourceIr")]
        public string? SourceIr { get; init; }

        [JsonPropertyName("generator")]
        public string Generator { get; init; } = "VDG.VBA.CLI/ir2diagram";
    }

    internal sealed record TaxonomyLegend
    {
        [JsonPropertyName("subsystems")]
        public IList<LegendEntry> Subsystems { get; init; } = new List<LegendEntry>();

        [JsonPropertyName("roles")]
        public IList<LegendEntry> Roles { get; init; } = new List<LegendEntry>();
    }

    internal sealed record LegendEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;
    }

    internal sealed record TaxonomyModuleRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("kind")]
        public string? Kind { get; init; }

        [JsonPropertyName("file")]
        public string? File { get; init; }

        [JsonPropertyName("tier")]
        public string Tier { get; init; } = "Modules";

        [JsonPropertyName("subsystem")]
        public SubsystemClassification Subsystem { get; init; } = new();

        [JsonPropertyName("ownership")]
        public OwnershipInfo Ownership { get; init; } = new();

        [JsonPropertyName("tags")]
        public IList<string> Tags { get; init; } = new List<string>();

        [JsonPropertyName("roles")]
        public IList<string> Roles { get; init; } = new List<string>();

        [JsonPropertyName("procedures")]
        public IList<TaxonomyProcedureRecord> Procedures { get; init; } = new List<TaxonomyProcedureRecord>();

        [JsonPropertyName("evidence")]
        public IDictionary<string, string> Evidence { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed record SubsystemClassification
    {
        [JsonPropertyName("primary")]
        public string Primary { get; init; } = "Core.Modules";

        [JsonPropertyName("secondary")]
        public IList<string> Secondary { get; init; } = new List<string>();

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }

        [JsonPropertyName("reasons")]
        public IList<string> Reasons { get; init; } = new List<string>();
    }

    internal sealed record OwnershipInfo
    {
        [JsonPropertyName("team")]
        public string? Team { get; init; }

        [JsonPropertyName("reviewer")]
        public string? Reviewer { get; init; }
    }

    internal sealed record TaxonomyProcedureRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("role")]
        public RoleClassification Role { get; init; } = new();

        [JsonPropertyName("capabilities")]
        public IList<string> Capabilities { get; init; } = new List<string>();

        [JsonPropertyName("resources")]
        public IList<string> Resources { get; init; } = new List<string>();

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }
    }

    internal sealed record RoleClassification
    {
        [JsonPropertyName("primary")]
        public string Primary { get; init; } = "Utility";

        [JsonPropertyName("secondary")]
        public IList<string> Secondary { get; init; } = new List<string>();

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }

        [JsonPropertyName("reasons")]
        public IList<string> Reasons { get; init; } = new List<string>();
    }

    internal sealed record TaxonomyUnresolvedRecord
    {
        [JsonPropertyName("target")]
        public string Target { get; init; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = string.Empty;

        [JsonPropertyName("suggestedAction")]
        public string SuggestedAction { get; init; } = "Review manually";
    }

    internal sealed record FlowArtifact
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("generatedAt")]
        public DateTimeOffset GeneratedAt { get; init; }

        [JsonPropertyName("project")]
        public TaxonomyProjectInfo Project { get; init; } = new();

        [JsonPropertyName("flows")]
        public IList<FlowRecord> Flows { get; init; } = new List<FlowRecord>();

        [JsonPropertyName("residuals")]
        public IList<FlowResidual> Residuals { get; init; } = new List<FlowResidual>();
    }

    internal sealed record FlowRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("channel")]
        public string Channel { get; init; } = string.Empty;

        [JsonPropertyName("purpose")]
        public string Purpose { get; init; } = string.Empty;

        [JsonPropertyName("source")]
        public FlowEndpoint Source { get; init; } = new();

        [JsonPropertyName("target")]
        public FlowEndpoint Target { get; init; } = new();

        [JsonPropertyName("edgeCount")]
        public int EdgeCount { get; init; }

        [JsonPropertyName("sampleCalls")]
        public IList<FlowSampleCall> SampleCalls { get; init; } = new List<FlowSampleCall>();

        [JsonPropertyName("sharedResources")]
        public IList<SharedResource> SharedResources { get; init; } = new List<SharedResource>();

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }

        [JsonPropertyName("reasons")]
        public IList<string> Reasons { get; init; } = new List<string>();
    }

    internal sealed record FlowEndpoint
    {
        [JsonPropertyName("moduleId")]
        public string ModuleId { get; init; } = string.Empty;

        [JsonPropertyName("procedureId")]
        public string ProcedureId { get; init; } = string.Empty;

        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("subsystem")]
        public string? Subsystem { get; init; }
    }

    internal sealed record FlowSampleCall
    {
        [JsonPropertyName("from")]
        public string From { get; init; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; init; } = string.Empty;

        [JsonPropertyName("site")]
        public FlowSampleSite Site { get; init; } = new();

        [JsonPropertyName("branch")]
        public string? Branch { get; init; }

        [JsonPropertyName("metadata")]
        public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed record FlowSampleSite
    {
        [JsonPropertyName("file")]
        public string? File { get; init; }

        [JsonPropertyName("line")]
        public int? Line { get; init; }
    }

    internal sealed record SharedResource
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; init; } = string.Empty;
    }

    internal sealed record FlowResidual
    {
        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("cause")]
        public string Cause { get; init; } = string.Empty;

        [JsonPropertyName("suggestedAction")]
        public string SuggestedAction { get; init; } = "Inspect IR or enable include-unknown";
    }
}
