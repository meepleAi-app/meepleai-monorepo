using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Infrastructure.Entities.SystemConfiguration;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.SystemConfiguration;

/// <summary>
/// #4179: il seed del catalogo su Postgres vero, a partire dal catalogo che il seed precedente a
/// #4164 lasciava. È lo scenario in cui un modello cambia due volte nello stesso giro: Llama 3.3 70B
/// a pagamento diventa primario al posto della variante «:free» e riceve il prezzo verificato.
/// </summary>
/// <remarks>
/// Il repository attacca un'istanza nuova a ogni <c>UpdateAsync</c>: due aggiornamenti dello stesso
/// modello nella stessa unit of work fanno lanciare EF. Il doppio dei test unitari lo imita, ma è
/// questo test a dire che l'imitazione è giusta.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4179")]
public sealed class SeedAiModelsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public SeedAiModelsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"seed_ai_models_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await using var context = _fixture.CreateDbContext(_connectionString);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _fixture.DropIsolatedDatabaseAsync(_databaseName);

    private static AiModelConfiguration Row(
        string modelId, string provider, int priority, ModelPricing pricing, bool isActive, bool isPrimary = false) =>
        AiModelConfiguration.Create(modelId, modelId, provider, priority,
            new ModelSettings(4096, 0.7m, pricing), isActive, isPrimary);

    private async Task SeedAsync(CancellationToken ct)
    {
        await using var context = _fixture.CreateDbContext(_connectionString);
        var handler = new SeedAiModelsCommandHandler(
            new EfAiModelConfigurationRepository(context, new DomainEventCollector()),
            new UnitOfWork(context),
            NullLogger<SeedAiModelsCommandHandler>.Instance);
        await handler.Handle(new SeedAiModelsCommand(), ct);
    }

    [Fact]
    public async Task Pre4164Catalog_IsRepricedAndRetired_InOneSave()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var context = _fixture.CreateDbContext(_connectionString))
        {
            var repository = new EfAiModelConfigurationRepository(context, new DomainEventCollector());
            await repository.AddRangeAsync(
            [
                Row("meta-llama/llama-3.3-70b-instruct:free", "OpenRouter", 1, ModelPricing.Free, isActive: true, isPrimary: true),
                Row("meta-llama/llama-3.3-70b-instruct", "OpenRouter", 2, new ModelPricing(0.10m, 0.32m), isActive: true),
                Row("google/gemini-2.0-flash-exp:free", "OpenRouter", 3, ModelPricing.Free, isActive: true),
            ], ct);
            await context.SaveChangesAsync(ct);
        }

        await SeedAsync(ct);

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var catalog = await new EfAiModelConfigurationRepository(verification, new DomainEventCollector()).GetAllAsync(ct);
        var llama = catalog.Single(m => m.ModelId == "meta-llama/llama-3.3-70b-instruct");
        llama.IsPrimary.Should().BeTrue("il primario di OpenRouter passa al sostituto della variante «:free»");
        llama.Settings.Pricing.InputPricePerMillion.Should().Be(0.22m, "e nello stesso giro riceve il prezzo verificato");
        catalog.Single(m => m.ModelId == "meta-llama/llama-3.3-70b-instruct:free").IsActive.Should().BeFalse();
        catalog.Single(m => m.ModelId == "google/gemini-2.0-flash-exp:free").IsActive.Should().BeFalse();
        catalog.Where(m => m.Provider == "OpenRouter" && m.IsPrimary).Should().ContainSingle();
    }

    [Fact]
    public async Task SeedingTwice_ChangesNothingTheSecondTime()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedAsync(ct);

        await using var before = _fixture.CreateDbContext(_connectionString);
        var first = await before.Set<AiModelConfigurationEntity>()
            .AsNoTracking().Select(m => new { m.ModelId, m.UpdatedAt }).ToListAsync(ct);

        await SeedAsync(ct);

        await using var after = _fixture.CreateDbContext(_connectionString);
        var second = await after.Set<AiModelConfigurationEntity>()
            .AsNoTracking().Select(m => new { m.ModelId, m.UpdatedAt }).ToListAsync(ct);
        second.Should().BeEquivalentTo(first);
    }
}
