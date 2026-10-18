using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.ReferenceQuestions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.ReferenceQuestions;

/// <summary>
/// #4470: i percorsi della layer che non toccano il database. Il database è nullo apposta: se la layer lo
/// usasse, la <c>NullReferenceException</c> finirebbe nel log come errore, e i test lo vedrebbero.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4470")]
public sealed class ReferenceQuestionsSeedLayerUnitTests
{
    [Fact]
    public async Task WithTheSkipFlag_ItDoesNothing()
    {
        var logger = new CapturingLogger();
        var context = Context(logger, new Dictionary<string, string?> { ["SKIP_REFERENCE_QUESTIONS_SEED"] = "true" });

        await new ReferenceQuestionsSeedLayer().SeedAsync(context, TestContext.Current.CancellationToken);

        logger.At(LogLevel.Error).Should().BeEmpty();
        logger.At(LogLevel.Information).Should().ContainSingle().Which.Should().Contain("SKIP_REFERENCE_QUESTIONS_SEED");
    }

    [Fact]
    public async Task AnInvalidSet_IsLoggedAsAnError_InsteadOfStoppingTheLayersAfterIt()
    {
        var logger = new CapturingLogger();
        var layer = new ReferenceQuestionsSeedLayer(
            () => throw new InvalidDataException("id 'CAT-01' appears 2 times"));

        var seed = () => layer.SeedAsync(Context(logger), TestContext.Current.CancellationToken);

        await seed.Should().NotThrowAsync("l'orchestratore non isola le layer: un'eccezione fermerebbe quelle dopo");
        logger.At(LogLevel.Error).Should().ContainSingle().Which.Should().Contain("CAT-01");
    }

    [Fact]
    public void ItRunsInStagingAndDev_NotInProd()
    {
        var layer = new ReferenceQuestionsSeedLayer();

        layer.MinimumProfile.Should().Be(SeedProfile.Staging, "i giochi li crea CatalogSeedLayer, che è Staging");
        layer.Name.Should().Be("ReferenceQuestions");
    }

    private static SeedContext Context(CapturingLogger logger, Dictionary<string, string?>? config = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config ?? new()).Build())
            .BuildServiceProvider();

        return new SeedContext(
            Profile: SeedProfile.Staging,
            DbContext: null!,
            Services: services,
            Logger: logger,
            SystemUserId: Guid.Empty);
    }
}
