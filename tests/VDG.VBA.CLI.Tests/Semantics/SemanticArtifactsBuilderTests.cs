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

    [Fact]
    public void AppliesSeedOverrides()
    {
        var repoRoot = LocateRepoRoot();
        var fixturesRoot = Path.Combine(repoRoot, "tests", "VDG.VBA.CLI.Tests", "Semantics", "Fixtures");
        var irPath = Path.Combine(fixturesRoot, "sample.ir.json");
        var seedPath = Path.Combine(fixturesRoot, "sample.seed.json");

        var ir = JsonSerializer.Deserialize<IrRoot>(File.ReadAllText(irPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize IR fixture");

        var seed = TaxonomySeedDocument.Load(seedPath);
        var builder = new SemanticArtifactsBuilder();
        var artifacts = builder.Build(
            ir.Project.Modules,
            ir.Project.Name,
            "tests/VDG.VBA.CLI.Tests/Semantics/Fixtures/sample.ir.json",
            DateTimeOffset.Parse("2025-01-01T00:00:00Z"),
            seed,
            SeedMergeMode.Merge);

        var loginModule = artifacts.Taxonomy.Modules.First(m => m.Id == "frmLogin");
        Assert.Equal("UI.Forms", loginModule.Subsystem.Primary);
        Assert.Equal("Forms Guild", loginModule.Ownership.Team);
        Assert.Equal("Manual override for login form", loginModule.Notes);
        Assert.NotNull(loginModule.Metadata);
        Assert.Equal("true", loginModule.Metadata!["seeded"]);

        var submitProc = loginModule.Procedures.First(p => p.Id == "frmLogin.Submit_Click");
        Assert.Equal("Validator", submitProc.Role.Primary);
        Assert.Equal("Seed override", submitProc.Notes);
        Assert.NotNull(submitProc.Metadata);
        Assert.Equal("true", submitProc.Metadata!["seeded"]);

        Assert.True(artifacts.Modules["frmLogin"].IsSeeded);
        Assert.True(artifacts.Procedures["frmLogin.Submit_Click"].IsSeeded);
        var securityLegend = artifacts.Taxonomy.Legend.Subsystems.First(entry => entry.Id == "Security.Auth");
        Assert.Equal("Seeded security subsystem", securityLegend.Description);
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
