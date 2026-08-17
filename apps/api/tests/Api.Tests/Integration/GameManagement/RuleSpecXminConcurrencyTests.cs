using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Concorrenza ottimistica su <c>rule_specs</c> (#3651, ultima entità del lotto).
///
/// <para>
/// <b>È l'unica entità del lotto il cui token esce dal boundary HTTP</b>: viene esposto come
/// <c>ETag</c> in <c>RuleSpecDto</c> e <c>GameDto</c>, e i client lo rimandano indietro in
/// <c>UpdateRuleSpecCommand.ExpectedETag</c> per l'editing collaborativo introdotto da #2055.
/// </para>
/// <para>
/// <b>Che cosa non ha mai funzionato.</b> <c>UpdateRuleSpecCommandHandler:98</c> confronta l'ETag
/// solo <i>dentro</i> una guardia:
/// <code>if (latestSpec != null &amp;&amp; latestSpec.RowVersion != null)</code>
/// Con il token <c>byte[]</c> su colonna <c>bytea</c> — che Postgres non popola — quella condizione
/// è <b>sempre falsa</b>. Il <c>ConflictException</c> «RuleSpec has been modified by another user»
/// non è mai stato sollevato, e l'ETag restituito ai client è sempre stato <c>null</c>.
/// </para>
/// <para>
/// Per questo qui i test sono due e coprono due piani diversi: il conflitto a livello di database
/// (come nel resto del lotto) e il <b>contratto</b> — un ETag che esiste davvero.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "3651")]
public sealed class RuleSpecXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public RuleSpecXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"rulespec_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task<(Guid gameId, Guid specId)> SeedSpecAsync()
    {
        var gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = gameId,
            Title = "Gioco con regolamento",
        });

        var specId = Guid.NewGuid();
        _dbContext.RuleSpecs.Add(new RuleSpecEntity
        {
            Id = specId,
            GameId = gameId,
            Version = "v1",
            CreatedAt = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync();
        return (gameId, specId);
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var (_, specId) = await SeedSpecAsync();

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);

        var seenByFirst = await firstContext.RuleSpecs.FirstAsync(r => r.Id == specId);
        var seenBySecond = await secondContext.RuleSpecs.FirstAsync(r => r.Id == specId);

        seenByFirst.Version = "v2";
        await firstContext.SaveChangesAsync();

        // Il secondo editor salva partendo dalla v1: senza conflitto rilevato sovrascrive la v2,
        // che è precisamente il «lost update» che #2055 dichiarava di prevenire.
        seenBySecond.Version = "v2-alt";

        var secondWrite = async () => await secondContext.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due editor concorrenti sullo stesso regolamento devono escludersi (#3651)");
    }

    [Fact]
    public async Task GetRuleSpec_ExposesANonNullETag()
    {
        var (gameId, _) = await SeedSpecAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var handler = new GetRuleSpecQueryHandler(context);

        var dto = await handler.Handle(new GetRuleSpecQuery(gameId), CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.ETag.Should().NotBeNullOrEmpty(
            "l'ETag è il contratto con cui i client rilevano le modifiche concorrenti (#2055): "
            + "finché il token è stato una bytea mai popolata, questo campo è sempre stato null e "
            + "l'editing collaborativo non ha mai potuto funzionare");
    }
}
