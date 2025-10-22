using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Tests.Common
{
    /// <summary>
    /// Utility that loads the fixture metadata snapshot emitted by tools/render-fixture.ps1
    /// and provides helpers for verifying SHA256 hashes and enumerating fixtures/modes.
    /// </summary>
    public static class FixtureSnapshotVerifier
    {
        public sealed record FixtureMode
        (
            string Fixture,
            string Mode,
            string GoldenIrPath,
            string GoldenDiagramPath,
            string GoldenDiagnosticsPath,
            string GoldenVsdxPath,
            string HashIr,
            string HashDiagram,
            string HashDiagnostics,
            string HashVsdx
        );

        public static IReadOnlyList<FixtureMode> LoadSnapshot(string repoRoot)
        {
            if (string.IsNullOrWhiteSpace(repoRoot)) throw new ArgumentException("repoRoot must be provided", nameof(repoRoot));
            var metadataPath = Path.Combine(repoRoot, "plan docs", "fixtures_metadata.json");
            if (!File.Exists(metadataPath)) throw new FileNotFoundException("Fixture metadata snapshot not found", metadataPath);

            using var doc = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var fixtures = doc.RootElement.GetProperty("fixtures");
            var list = new List<FixtureMode>();
            foreach (var fixture in fixtures.EnumerateArray())
            {
                var name = fixture.GetProperty("name").GetString() ?? string.Empty;
                foreach (var mode in fixture.GetProperty("modes").EnumerateArray())
                {
                    var modeName = mode.GetProperty("mode").GetString() ?? string.Empty;
                    var hashes = mode.GetProperty("hashes");
                    var golden = mode.GetProperty("paths").GetProperty("golden");

                    list.Add(new FixtureMode(
                        name,
                        modeName,
                        ResolveRepoPath(repoRoot, golden.GetProperty("ir").GetString()),
                        ResolveRepoPath(repoRoot, golden.GetProperty("diagram").GetString()),
                        ResolveRepoPath(repoRoot, golden.GetProperty("diagnostics").GetString()),
                        ResolveRepoPath(repoRoot, golden.GetProperty("vsdx").GetString()),
                        hashes.GetProperty("ir").GetString() ?? string.Empty,
                        hashes.GetProperty("diagram").GetString() ?? string.Empty,
                        hashes.GetProperty("diagnostics").GetString() ?? string.Empty,
                        hashes.GetProperty("vsdx").GetString() ?? string.Empty
                    ));
                }
            }
            return list;
        }

        public static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ResolveRepoPath(string repoRoot, string? relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) throw new ArgumentException("Relative path missing in fixture snapshot", nameof(relative));
            var normalized = relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(repoRoot, normalized));
        }
    }
}
