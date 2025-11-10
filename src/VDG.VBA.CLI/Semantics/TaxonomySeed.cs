using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VDG.VBA.CLI.Semantics
{
    internal enum SeedMergeMode
    {
        Merge,
        Strict
    }

    internal sealed class TaxonomySeedDocument
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private readonly Dictionary<string, TaxonomySeedModule> _modules;
        private readonly Dictionary<string, TaxonomySeedProcedure> _procedures;
        private readonly Dictionary<string, SeedSubsystemDefaults> _subsystemDefaults;
        private readonly HashSet<string> _usedModuleKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _usedProcedureKeys = new(StringComparer.OrdinalIgnoreCase);

        private TaxonomySeedDocument(
            string schemaVersion,
            Dictionary<string, TaxonomySeedModule> modules,
            Dictionary<string, TaxonomySeedProcedure> procedures,
            Dictionary<string, SeedSubsystemDefaults> subsystemDefaults)
        {
            SeedSchemaVersion = string.IsNullOrWhiteSpace(schemaVersion) ? "1.0" : schemaVersion!;
            _modules = modules ?? new(StringComparer.OrdinalIgnoreCase);
            _procedures = procedures ?? new(StringComparer.OrdinalIgnoreCase);
            _subsystemDefaults = subsystemDefaults ?? new(StringComparer.OrdinalIgnoreCase);
        }

        public string SeedSchemaVersion { get; }

        public IReadOnlyDictionary<string, TaxonomySeedModule> Modules => _modules;

        public IReadOnlyDictionary<string, TaxonomySeedProcedure> Procedures => _procedures;

        public IReadOnlyDictionary<string, SeedSubsystemDefaults> SubsystemDefaults => _subsystemDefaults;

        public static TaxonomySeedDocument Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty.", nameof(path));
            var json = File.ReadAllText(path);
            var payload = JsonSerializer.Deserialize<TaxonomySeedPayload>(json, SerializerOptions)
                          ?? throw new InvalidOperationException("Failed to parse taxonomy seed file.");
            return new TaxonomySeedDocument(
                payload.SeedSchemaVersion ?? "1.0",
                payload.Modules?.Where(kv => kv.Value is not null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, TaxonomySeedModule>(StringComparer.OrdinalIgnoreCase),
                payload.Procedures?.Where(kv => kv.Value is not null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, TaxonomySeedProcedure>(StringComparer.OrdinalIgnoreCase),
                payload.Defaults?.Subsystems?.Where(kv => kv.Value is not null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, SeedSubsystemDefaults>(StringComparer.OrdinalIgnoreCase));
        }

        public bool TryGetModule(string? moduleId, string? moduleName, out TaxonomySeedModule module)
        {
            return TryGetOverride(_modules, _usedModuleKeys, moduleId, moduleName, out module);
        }

        public bool TryGetProcedure(string? moduleId, string? procedureId, out TaxonomySeedProcedure procedure)
        {
            var compositeId = string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(procedureId)
                ? null
                : $"{moduleId}.{procedureId}";
            return TryGetOverride(_procedures, _usedProcedureKeys, procedureId, compositeId, out procedure);
        }

        public IReadOnlyList<string> GetUnmatchedModules() =>
            _modules.Keys.Where(k => !_usedModuleKeys.Contains(k)).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

        public IReadOnlyList<string> GetUnmatchedProcedures() =>
            _procedures.Keys.Where(k => !_usedProcedureKeys.Contains(k)).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

        private static bool TryGetOverride<T>(
            IDictionary<string, T> map,
            ISet<string> tracker,
            string? primaryKey,
            string? secondaryKey,
            out T value)
        {
            if (!string.IsNullOrWhiteSpace(primaryKey) && map.TryGetValue(primaryKey, out value))
            {
                tracker.Add(primaryKey);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(secondaryKey) && map.TryGetValue(secondaryKey, out value))
            {
                tracker.Add(secondaryKey);
                return true;
            }

            value = default!;
            return false;
        }

        private sealed class TaxonomySeedPayload
        {
            [JsonPropertyName("seedSchemaVersion")]
            public string? SeedSchemaVersion { get; init; }

            [JsonPropertyName("modules")]
            public Dictionary<string, TaxonomySeedModule>? Modules { get; init; }

            [JsonPropertyName("procedures")]
            public Dictionary<string, TaxonomySeedProcedure>? Procedures { get; init; }

            [JsonPropertyName("defaults")]
            public SeedDefaults? Defaults { get; init; }
        }
    }

    internal sealed class TaxonomySeedModule
    {
        [JsonPropertyName("primarySubsystem")]
        public string? PrimarySubsystem { get; init; }

        [JsonPropertyName("primaryRole")]
        public string? PrimaryRole { get; init; }

        [JsonPropertyName("owner")]
        public string? Owner { get; init; }

        [JsonPropertyName("tags")]
        public IList<string>? Tags { get; init; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }

        [JsonPropertyName("metadata")]
        public IDictionary<string, string>? Metadata { get; init; }
    }

    internal sealed class TaxonomySeedProcedure
    {
        [JsonPropertyName("primaryRole")]
        public string? PrimaryRole { get; init; }

        [JsonPropertyName("owner")]
        public string? Owner { get; init; }

        [JsonPropertyName("tags")]
        public IList<string>? Tags { get; init; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }

        [JsonPropertyName("metadata")]
        public IDictionary<string, string>? Metadata { get; init; }

        [JsonPropertyName("primarySubsystem")]
        public string? PrimarySubsystem { get; init; }
    }

    internal sealed class SeedSubsystemDefaults
    {
        [JsonPropertyName("owner")]
        public string? Owner { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }

    internal sealed class SeedDefaults
    {
        [JsonPropertyName("subsystems")]
        public Dictionary<string, SeedSubsystemDefaults>? Subsystems { get; init; }
    }
}
