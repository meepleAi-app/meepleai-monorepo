using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// #4585: <see cref="SharedGameRepository"/>.<c>Update</c> rimette sul contesto un'entità costruita da
/// <c>MapToEntity</c> con <c>DbSet.Update</c>, che segna come modificate tutte le colonne di
/// <c>shared_games</c>. Una colonna che il mapper non scrive veniva quindi salvata al valore di default
/// della classe: <c>has_knowledge_base</c>, <c>BggRawData</c> e <c>test_run_id</c> a ogni comando admin
/// che salva un gioco, e <c>is_rag_public</c> anche quando l'aggregato lo portava a <c>true</c>.
/// <para>
/// Il primo test non elenca le colonne: le prende dal modello EF. Una colonna aggiunta domani all'entità,
/// senza mapping e senza esclusione, lo fa fallire con il suo nome. Stesso schema di
/// <c>PdfDocumentRepositoryUnownedColumnsTests</c> (#4347).
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4585")]
public sealed class SharedGameRepositoryUnownedColumnsTests : IDisposable
{
    private static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    // Valori che l'aggregato valida o filtra in lettura: la sentinella deve essere ammessa. Dove il valore
    // ammesso coincide con il default dell'entità, la colonna non si distingue dal default, e va bene così:
    // è un filtro (IsDeleted), non un dato da preservare.
    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        // GetByIdAsync non restituisce i giochi cancellati.
        [nameof(SharedGameEntity.IsDeleted)] = false,
        // Valori degli enum dell'aggregato diversi dal default dell'entità.
        [nameof(SharedGameEntity.Status)] = (int)GameStatus.Published,
        [nameof(SharedGameEntity.GameDataStatus)] = (int)GameDataStatus.Enriched,
        // GameRules.Create: lingua ISO 639-1 e URL https assoluto.
        [nameof(SharedGameEntity.RulesLanguage)] = "it",
        [nameof(SharedGameEntity.RulesExternalUrl)] = "https://example.org/regole.pdf",
    };

    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();

    public void Dispose() => _db.Dispose();

    private SharedGameRepository CreateRepository() => new(_db, Mock.Of<IDomainEventCollector>());

    // Le colonne della riga, escluso il token di concorrenza, che cambia a ogni scrittura per contratto.
    private IReadOnlyList<IProperty> Columns() =>
        _db.Model.FindEntityType(typeof(SharedGameEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null && p.Name != nameof(SharedGameEntity.Xmin))
            .ToList();

    private static object? SentinelFor(IProperty property)
    {
        if (LegalSentinels.TryGetValue(property.Name, out var legal))
        {
            return legal;
        }

        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        return type switch
        {
            _ when type == typeof(string) => $"sentinella-{property.Name}",
            _ when type == typeof(int) => 7,
            _ when type == typeof(decimal) => 3.5m,
            _ when type == typeof(bool) => true,
            _ when type == typeof(DateTime) => SentinelDate,
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }

    private static bool SameValue(object? expected, object? actual) => Equals(expected, actual);

    [Fact]
    public async Task Update_WithNoChange_LeavesEveryColumnAsItWas()
    {
        // Se il modello non si carica, non c'è nessuna colonna da confrontare e il test passerebbe a vuoto.
        Columns().Should().NotBeEmpty("le colonne vengono dal modello EF, che deve avere SharedGameEntity");

        // Arrange: ogni colonna con un valore diverso dal suo default.
        var seeded = new SharedGameEntity();
        foreach (var column in Columns())
        {
            column.PropertyInfo!.SetValue(seeded, SentinelFor(column));
        }

        _db.SharedGames.Add(seeded);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();
        var expected = Columns().ToDictionary(c => c.Name, c => c.PropertyInfo!.GetValue(seeded), StringComparer.Ordinal);

        // Act: lettura e scrittura dal repository, senza cambiare niente.
        var repository = CreateRepository();
        var game = await repository.GetByIdAsync(seeded.Id, TestContext.Current.CancellationToken);
        repository.Update(game!);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        // Assert
        var after = await _db.SharedGames.AsNoTracking().SingleAsync(g => g.Id == seeded.Id, TestContext.Current.CancellationToken);
        var changed = Columns()
            .Where(c => !SameValue(expected[c.Name], c.PropertyInfo!.GetValue(after)))
            .Select(c => $"{c.Name}: «{expected[c.Name]}» → «{c.PropertyInfo!.GetValue(after)}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty(
            "un Update che non cambia niente non deve cambiare niente; una colonna che il mapper non " +
            "scrive va in SharedGameRepository.ColumnsNotOwnedByTheAggregate");
    }

    [Fact]
    public void ColumnsNotOwnedByTheAggregate_AreNotPropertiesOfTheAggregate()
    {
        // Una colonna che l'aggregato porta, e che il mapper scrive, non va esclusa: le sue modifiche
        // andrebbero perse in silenzio (è il caso di IsRagPublic).
        var ownedByTheAggregate = SharedGameRepository.ColumnsNotOwnedByTheAggregate
            .Where(name => typeof(SharedGame).GetProperty(name) is not null)
            .ToList();

        ownedByTheAggregate.Should().BeEmpty();
    }

    [Fact]
    public void ColumnsNotOwnedByTheAggregate_AreColumnsOfTheRow()
    {
        var columns = Columns().Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        SharedGameRepository.ColumnsNotOwnedByTheAggregate.Should().OnlyContain(name => columns.Contains(name));
    }

    /// <summary>
    /// <c>SetRagPublicAccess</c> cambia l'aggregato, ma la colonna restava com'era: <c>MapToDomain</c> non
    /// la leggeva e <c>MapToEntity</c> non la scriveva, quindi <c>PUT …/rag-access</c> con <c>true</c>
    /// salvava <c>false</c>. Parte da una riga con il valore opposto a quello impostato.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetRagPublicAccess_ThroughTheRepository_WritesTheColumn(bool isRagPublic)
    {
        var seeded = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Catan",
            Description = "desc",
            Status = (int)GameStatus.Published,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            IsRagPublic = !isRagPublic,
        };
        _db.SharedGames.Add(seeded);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var repository = CreateRepository();
        var game = await repository.GetByIdAsync(seeded.Id, TestContext.Current.CancellationToken);
        game!.IsRagPublic.Should().Be(!isRagPublic, "l'aggregato deve conoscere il valore della riga");
        game.SetRagPublicAccess(isRagPublic);
        repository.Update(game);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var after = await _db.SharedGames.AsNoTracking().SingleAsync(g => g.Id == seeded.Id, TestContext.Current.CancellationToken);
        after.IsRagPublic.Should().Be(isRagPublic);
    }
}
