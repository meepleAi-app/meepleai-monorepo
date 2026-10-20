using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// #4552: <see cref="PlayRecordRepository"/> ricostruiva la riga con <c>PlayRecord.CreateWithGame</c> /
/// <c>CreateFreeForm</c>, le factory di una partita registrata NUOVA, e poi ne correggeva i campi via reflection.
/// Due cose sfuggivano alla correzione:
/// <list type="bullet">
///   <item>l'evento di creazione <see cref="PlayRecordCreatedEvent"/>, sollevato a ogni lettura e mai pulito:
///     al primo <c>UpdateAsync</c> il repository lo raccoglieva di nuovo, e il salvataggio lo scriveva nella
///     outbox come se la partita nascesse in quel momento;</item>
///   <item>i punteggi, ricaricati con <c>PlayRecord.RecordScore</c>, il metodo di un punteggio nuovo: a ogni
///     lettura un <see cref="RecordScoreRecordedEvent"/> per punteggio e <c>UpdatedAt</c> riportato ad "adesso",
///     dopo che la reflection lo aveva appena ripristinato.</item>
/// </list>
/// <para>
/// La guardia sulle colonne prende le colonne dal modello EF, con una sentinella diversa per colonna: una colonna
/// nuova senza mapping in entrambe le direzioni, o due colonne scambiate nel mapper, la fanno fallire col nome.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4552")]
public sealed class PlayRecordRepositoryTests : IDisposable
{
    // Nel passato: una data futura la factory di creazione la rifiuta, e una data ricostruita con UtcNow si vede.
    private static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static readonly string ScoringConfigJson = JsonSerializer.Serialize(new
    {
        EnabledDimensions = new[] { "points", "vp" },
        DimensionUnits = new Dictionary<string, string>(StringComparer.Ordinal) { ["points"] = "pts", ["vp"] = "vp" },
    });

    // Valori che l'aggregato legge come tipo di dominio: ammessi, e diversi dal default dell'entità.
    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        [nameof(PlayRecordEntity.Visibility)] = 1, // Group: con GroupId valorizzato più sotto
        [nameof(PlayRecordEntity.Status)] = 2, // Completed
        [nameof(PlayRecordEntity.ScoringConfigJson)] = ScoringConfigJson,
        // Il filtro di query nasconde le righe cancellate: una riga cancellata non si legge, quindi la sentinella
        // è il valore che si legge.
        [nameof(PlayRecordEntity.IsDeleted)] = false,
    };

    // xmin è una colonna di sistema di Postgres: la scrive il database, non il mapper (ADR-060), e il provider
    // InMemory non la imita. Il suo giro è coperto da ConcurrencyTokenWritePathInventoryTests.
    private static readonly HashSet<string> NotRoundTripped = new(StringComparer.Ordinal)
    {
        nameof(PlayRecordEntity.Xmin),
    };

    private readonly MeepleAiDbContext _db;

    public PlayRecordRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PlayRecordRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private PlayRecordRepository CreateRepository(IDomainEventCollector? collector = null) =>
        new(_db, collector ?? new Mock<IDomainEventCollector>().Object);

    private IReadOnlyList<IProperty> Columns() =>
        _db.Model.FindEntityType(typeof(PlayRecordEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null && !NotRoundTripped.Contains(p.Name))
            .ToList();

    private static object? SentinelFor(IProperty property, int index)
    {
        if (LegalSentinels.TryGetValue(property.Name, out var legal))
        {
            return legal;
        }

        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        return type switch
        {
            _ when type == typeof(string) => $"sentinella-{property.Name}",
            _ when type == typeof(int) => 7 + index,
            _ when type == typeof(bool) => true,
            _ when type == typeof(DateTime) => SentinelDate.AddDays(-index),
            _ when type == typeof(TimeSpan) => TimeSpan.FromMinutes(30 + index),
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }

    private PlayRecordEntity EveryColumnRow()
    {
        var row = new PlayRecordEntity();
        var columns = Columns();
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].PropertyInfo!.SetValue(row, SentinelFor(columns[i], i));
        }

        return row;
    }

    private async Task<PlayRecordEntity> SeedAsync(PlayRecordEntity row)
    {
        _db.PlayRecords.Add(row);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return row;
    }

    // L'aggregato porta visibilità e stato come enum, la riga come intero, e la configurazione dei punteggi come
    // oggetto, la riga come JSON.
    private static object? DomainValue(string column, PlayRecord record)
    {
        if (string.Equals(column, nameof(PlayRecordEntity.ScoringConfigJson), StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new
            {
                record.ScoringConfig.EnabledDimensions,
                DimensionUnits = record.ScoringConfig.DimensionUnits.ToDictionary(k => k.Key, k => k.Value, StringComparer.Ordinal),
            });
        }

        var property = typeof(PlayRecord).GetProperty(column);
        if (property is null)
        {
            return "<assente dall'aggregato>";
        }

        var value = property.GetValue(record);
        return value is Enum e ? Convert.ToInt32(e, System.Globalization.CultureInfo.InvariantCulture) : value;
    }

    [Fact]
    public async Task GetByIdAsync_ReconstitutesEveryColumnOfTheRow()
    {
        var seeded = await SeedAsync(EveryColumnRow());

        var record = await CreateRepository().GetByIdAsync(seeded.Id);

        record.Should().NotBeNull();
        var lost = Columns()
            .Select(c => (Column: c.Name, Expected: c.PropertyInfo!.GetValue(seeded), Actual: DomainValue(c.Name, record!)))
            .Where(x => !Equals(x.Expected, x.Actual))
            .Select(x => $"{x.Column}: «{x.Expected}» → «{x.Actual}»");
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored");
    }

    [Fact]
    public async Task GetByIdAsync_RaisesNoDomainEvent()
    {
        var seeded = await SeedAsync(EveryColumnRow());

        var record = await CreateRepository().GetByIdAsync(seeded.Id);

        record!.DomainEvents.Select(e => e.GetType().Name).Should().BeEmpty(
            "reading a stored record is not creating it. Before #4552 every read raised PlayRecordCreatedEvent");
    }

    [Fact]
    public async Task UpdateAsync_AfterARead_CollectsOnlyTheEventsOfTheChange()
    {
        var seeded = await SeedAsync(EveryColumnRow());
        var collected = new List<string>();
        var collector = new Mock<IDomainEventCollector>();
        collector
            .Setup(c => c.CollectEventsFrom(It.IsAny<Api.SharedKernel.Domain.Interfaces.IAggregateRoot>()))
            .Callback<Api.SharedKernel.Domain.Interfaces.IAggregateRoot>(a => collected.AddRange(a.DomainEvents.Select(e => e.GetType().Name)));

        var repository = CreateRepository(collector.Object);
        var record = await repository.GetByIdAsync(seeded.Id);
        record!.UpdateDetails(notes: "nota corretta");
        await repository.UpdateAsync(record);

        // Il repository passa al collettore ciò che l'aggregato ha sollevato, e il salvataggio lo scrive nella
        // outbox: un evento di creazione qui verrebbe consegnato a ogni modifica della partita.
        collected.Should().Equal(nameof(PlayRecordUpdatedEvent));
    }

    [Fact]
    public async Task GetByIdAsync_RecordWithScores_KeepsStoredUpdatedAtAndScores()
    {
        var row = EveryColumnRow();
        var player = new RecordPlayerEntity { Id = Guid.NewGuid(), PlayRecordId = row.Id, DisplayName = "Giocatrice" };
        player.Scores.Add(new RecordScoreEntity { Id = Guid.NewGuid(), RecordPlayerId = player.Id, Dimension = "points", Value = 42, Unit = "pts" });
        player.Scores.Add(new RecordScoreEntity { Id = Guid.NewGuid(), RecordPlayerId = player.Id, Dimension = "vp", Value = 9, Unit = "vp" });
        row.Players.Add(player);
        var seeded = await SeedAsync(row);

        var record = await CreateRepository().GetByIdAsync(seeded.Id);

        record!.UpdatedAt.Should().Be(seeded.UpdatedAt,
            "restoring the stored scores is not recording them now. Before #4552 the mapper went through " +
            "PlayRecord.RecordScore, which stamps UpdatedAt with the current time");
        record.DomainEvents.Should().BeEmpty();
        var restored = record.Players.Should().ContainSingle().Subject;
        restored.Id.Should().Be(player.Id);
        restored.Scores.Select(s => (s.Dimension, s.Value, s.Unit))
            .Should().BeEquivalentTo(new[] { ("points", 42, (string?)"pts"), ("vp", 9, (string?)"vp") });
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_LeavesEveryColumnAsItWas()
    {
        var seeded = await SeedAsync(EveryColumnRow());
        var expected = Columns().ToDictionary(c => c.Name, c => c.PropertyInfo!.GetValue(seeded), StringComparer.Ordinal);

        var repository = CreateRepository();
        var record = await repository.GetByIdAsync(seeded.Id);
        await repository.UpdateAsync(record!);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.PlayRecords.AsNoTracking().SingleAsync(r => r.Id == seeded.Id);
        var changed = Columns()
            .Where(c => !Equals(expected[c.Name], c.PropertyInfo!.GetValue(after)))
            .Select(c => $"{c.Name}: «{expected[c.Name]}» → «{c.PropertyInfo!.GetValue(after)}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty(
            "an UpdateAsync that changes nothing must change nothing: DbSet.Update marks every column modified, " +
            "so a column the mapper loses is saved as its default");
    }
}
