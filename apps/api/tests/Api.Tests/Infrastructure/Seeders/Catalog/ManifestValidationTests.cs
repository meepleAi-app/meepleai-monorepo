using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class ManifestValidationTests
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    [Fact]
    public void Deserialize_ValidManifest_ParsesAllFields()
    {
        var yaml = """
            profile: dev
            catalog:
              games:
                - title: "Catan"
                  bggId: 13
                  language: en
                  pdf: "catan_rulebook.pdf"
                  seedAgent: true
                - title: "Wingspan"
                  bggId: 266192
                  language: en
                  seedAgent: false
              defaultAgent:
                name: "MeepleAssistant POC"
                model: "anthropic/claude-3-haiku"
                temperature: 0.3
                maxTokens: 2048
            """;

        var manifest = Deserializer.Deserialize<SeedManifest>(yaml);

        manifest.Profile.Should().Be("dev");
        manifest.Catalog.Games.Should().HaveCount(2);
        manifest.Catalog.Games[0].Title.Should().Be("Catan");
        manifest.Catalog.Games[0].BggId.Should().Be(13);
        manifest.Catalog.Games[0].SeedAgent.Should().BeTrue();
        manifest.Catalog.Games[1].Pdf.Should().BeNull();
        manifest.Catalog.DefaultAgent.Should().NotBeNull();
        manifest.Catalog.DefaultAgent!.Name.Should().Be("MeepleAssistant POC");
        manifest.Catalog.DefaultAgent.Temperature.Should().Be(0.3);
    }

    [Fact]
    public void Validate_DuplicateBggIds_ReturnsError()
    {
        var manifest = new SeedManifest
        {
            Profile = "dev",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new() { Title = "Catan", BggId = 13, Language = "en" },
                    new() { Title = "Catan Duplicate", BggId = 13, Language = "en" }
                }
            }
        };

        var errors = manifest.Validate();
        errors.Should().Contain(e => e.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MissingTitle_ReturnsError()
    {
        var manifest = new SeedManifest
        {
            Profile = "dev",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new() { Title = "", BggId = 13, Language = "en" }
                }
            }
        };

        var errors = manifest.Validate();
        errors.Should().Contain(e => e.Contains("title", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_SeedAgentTrueButNoDefaultAgent_ReturnsError()
    {
        var manifest = new SeedManifest
        {
            Profile = "dev",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new() { Title = "Catan", BggId = 13, Language = "en", SeedAgent = true }
                },
                DefaultAgent = null
            }
        };

        var errors = manifest.Validate();
        errors.Should().Contain(e => e.Contains("defaultAgent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ProfileMismatch_ReturnsError()
    {
        var manifest = new SeedManifest { Profile = "staging" };
        var errors = manifest.Validate(expectedProfile: SeedProfile.Dev);
        errors.Should().Contain(e => e.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ValidManifest_ReturnsNoErrors()
    {
        var manifest = new SeedManifest
        {
            Profile = "dev",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new() { Title = "Catan", BggId = 13, Language = "en", SeedAgent = true }
                },
                DefaultAgent = new SeedManifestAgent
                {
                    Name = "MeepleAssistant POC",
                    Model = "anthropic/claude-3-haiku",
                    Temperature = 0.3,
                    MaxTokens = 2048
                }
            }
        };

        var errors = manifest.Validate(expectedProfile: SeedProfile.Dev);
        errors.Should().BeEmpty();
    }
}
