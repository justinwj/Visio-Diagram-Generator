using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using VDG.VBA.CLI;
using VDG.VBA.CLI.Semantics;
using Xunit;

public sealed class SemanticArtifactsBuilderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Fact]
    public void EmitsDeterministicTaxonomyAndFlows()
    {
        var repoRoot = LocateRepoRoot();
        var fixturesRoot = Path.Combine(repoRoot, "tests", "VDG.VBA.CLI.Tests", "Semantics", "Fixtures");
        var irPath = Path.Combine(fixturesRoot, "sample.ir.json");
        var taxonomyFixture = Path.Combine(fixturesRoot, "simple.taxonomy.json");
        var flowsFixture = Path.Combine(fixturesRoot, "simple.flows.json");

        var ir = JsonSerializer.Deserialize<IrRoot>(File.ReadAllText(irPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize IR fixture");

        var builder = new SemanticArtifactsBuilder();
        var artifacts = builder.Build(ir.Project.Modules, ir.Project.Name, "tests/VDG.VBA.CLI.Tests/Semantics/Fixtures/sample.ir.json", DateTimeOffset.Parse("2025-01-01T00:00:00Z"));

        var taxonomyJson = Serialize(artifacts.Taxonomy);
        var flowsJson = Serialize(artifacts.Flow);

        Assert.Equal(NormalizeJson(File.ReadAllText(taxonomyFixture)), taxonomyJson);
        Assert.Equal(NormalizeJson(File.ReadAllText(flowsFixture)), flowsJson);
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static string NormalizeJson(string content)
    {
        using var doc = JsonDocument.Parse(content);
        return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
    }

    private static string LocateRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !current.GetFiles("Visio-Diagram-Generator.sln").Any())
        {
            current = current.Parent;
        }
        if (current == null)
            throw new InvalidOperationException("Unable to locate repo root");
        return current.FullName;
    }
}
