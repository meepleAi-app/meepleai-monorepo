using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SystemConfiguration;

/// <summary>
/// Consegna «expand» del ritiro del limite di agenti dai tier (#4138).
///
/// <para>
/// <c>tier_definitions.max_agents</c> e <c>token_tiers.max_agents_created</c> sono
/// <c>integer NOT NULL</c> senza default. Il codice non le legge più, ma restano nello schema finché
/// una consegna successiva non le elimina, perché la versione precedente del codice le mappa come
/// <c>int</c> non nullabile.
/// </para>
/// <para>
/// <b>Perché non nullable, a differenza delle colonne stringa ritirate ieri.</b> Rendere nullable un
/// intero farebbe nascere righe con <c>NULL</c>, e dopo un rollback il codice precedente lancerebbe
/// leggendole. Restano quindi shadow property <c>int</c>: misurato, EF le scrive a 0 in INSERT,
/// così il vincolo è soddisfatto senza alcuna migrazione (il modello coincide con lo snapshot).
/// </para>
/// <para>
/// Il test fissa quel contratto: se la colonna smettesse di essere mappata mentre esiste ancora
/// <c>NOT NULL</c>, ogni creazione di un tier fallirebbe con 23502. Verificato per perturbazione:
/// togliendo il mapping della shadow property il test fallisce.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4138")]
public sealed class TierRetiredMaxAgentsColumnsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public TierRetiredMaxAgentsColumnsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"tier_maxagents_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task CreatingATierDefinition_SatisfiesTheRetiredNotNullColumn()
    {
        var tier = TierDefinition.Create($"tier-{Guid.NewGuid():N}", "Tier di prova", TierLimits.FreeTier, "free");
        _dbContext.TierDefinitions.Add(tier);

        var save = async () => await _dbContext.SaveChangesAsync();
        await save.Should().NotThrowAsync(
            "max_agents è ancora NOT NULL nello schema: un INSERT che la omettesse fallirebbe con 23502");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var stored = await verification.TierDefinitions
            .Where(t => t.Id == tier.Id)
            .Select(t => EF.Property<int>(t.Limits, "MaxAgents"))
            .SingleAsync();
        stored.Should().Be(0);
    }

    [Fact]
    public async Task CreatingATokenTier_SatisfiesTheRetiredNotNullColumn()
    {
        var tier = TokenTier.CreateFreeTier();
        _dbContext.TokenTiers.Add(tier);

        var save = async () => await _dbContext.SaveChangesAsync();
        await save.Should().NotThrowAsync(
            "max_agents_created è ancora NOT NULL nello schema: un INSERT che la omettesse fallirebbe con 23502");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var stored = await verification.TokenTiers
            .Where(t => t.Id == tier.Id)
            .Select(t => EF.Property<int>(t.Limits, "MaxAgentsCreated"))
            .SingleAsync();
        stored.Should().Be(0);
    }
}
