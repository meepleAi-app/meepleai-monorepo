using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class AgentSeederTests
{
    [Fact]
    public void ManifestGame_WithSeedAgentTrue_RequiresDefaultAgent()
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
    public void ManifestGame_WithSeedAgentFalse_DoesNotRequireDefaultAgent()
    {
        var manifest = new SeedManifest
        {
            Profile = "dev",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new() { Title = "Wingspan", BggId = 266192, Language = "en", SeedAgent = false }
                },
                DefaultAgent = null
            }
        };

        var errors = manifest.Validate();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ManifestGame_WithSeedAgentTrue_AndValidDefaultAgent_PassesValidation()
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

        var errors = manifest.Validate();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ManifestGame_WithSeedAgentTrue_DefaultAgentMissingName_ReturnsError()
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
                    Name = "",
                    Model = "anthropic/claude-3-haiku"
                }
            }
        };

        var errors = manifest.Validate();
        errors.Should().Contain(e => e.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManifestGame_WithSeedAgentTrue_DefaultAgentMissingModel_ReturnsError()
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
                    Name = "Test Agent",
                    Model = ""
                }
            }
        };

        var errors = manifest.Validate();
        errors.Should().Contain(e => e.Contains("model", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManifestGame_MixedSeedAgentFlags_OnlyAgentGamesRequireDefaultAgent()
    {
        var manifest = new SeedManifest
        {
            Profile = "dev",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new() { Title = "Catan", BggId = 13, Language = "en", SeedAgent = true },
                    new() { Title = "Wingspan", BggId = 266192, Language = "en", SeedAgent = false },
                    new() { Title = "Azul", BggId = 230802, Language = "en", SeedAgent = false }
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

        var errors = manifest.Validate();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ManifestGame_AllSeedAgentFalse_NoDefaultAgentNeeded()
    {
        var manifest = new SeedManifest
        {
            Profile = "dev",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new() { Title = "Wingspan", BggId = 266192, Language = "en", SeedAgent = false },
                    new() { Title = "Azul", BggId = 230802, Language = "en", SeedAgent = false }
                },
                DefaultAgent = null
            }
        };

        var errors = manifest.Validate();
        errors.Should().BeEmpty();
    }
}
