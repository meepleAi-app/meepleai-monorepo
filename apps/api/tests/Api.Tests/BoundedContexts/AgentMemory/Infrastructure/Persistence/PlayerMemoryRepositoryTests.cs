using System.Text.Json;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Infrastructure.Entities;
using Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Infrastructure.Persistence;

/// <summary>
/// #4552: <see cref="PlayerMemoryRepository"/> ricostruiva la riga con <see cref="PlayerMemory.CreateForUser"/> /
/// <see cref="PlayerMemory.CreateForGuest"/>, le factory di una memoria NUOVA, e poi correggeva id e date via
/// reflection. La scelta fra le due dipendeva dal solo <c>UserId</c>: la memoria di un ospite reclamato (che ha
/// sia <c>UserId</c> sia <c>GuestName</c>) passava da <c>CreateForUser</c> e perdeva <c>GuestName</c>. Il primo
/// aggiornamento delle statistiche dopo il reclamo (<c>OnSessionCompletedUpdateStatsHandler</c>) lo scriveva
/// a NULL nel database.
/// <para>
/// La guardia prende le colonne dal modello EF, con una sentinella diversa per colonna: una colonna nuova senza
/// mapping, o due colonne scambiate nel mapper, la fanno fallire col nome.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
[Trait("Issue", "4552")]
public sealed class PlayerMemoryRepositoryTests : IDisposable
{
    private static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly Guid StatsGameId = Guid.Parse("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0");

    // Stessa forma di PlayerMemoryRepository.PlayerGameStatsDto, nello stesso ordine.
    private static readonly string GameStatsJson = JsonSerializer.Serialize(new[]
    {
        new { GameId = StatsGameId, Wins = 3, Losses = 2, TotalPlayed = 5, BestScore = (int?)42 },
    });

    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        [nameof(PlayerMemoryEntity.GameStatsJson)] = GameStatsJson,
    };

    // Colonne che il dominio non porta, con il perché. UpdatedAt non ha una proprietà sull'aggregato e nessun
    // codice la scrive: resta NULL in ogni riga. Non è un effetto della factory di creazione (#4552).
    private static readonly Dictionary<string, string> NotOnTheAggregate = new(StringComparer.Ordinal)
    {
        [nameof(PlayerMemoryEntity.UpdatedAt)] = "nessun codice la scrive: è sempre NULL",
    };

    private readonly MeepleAiDbContext _db;

    public PlayerMemoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PlayerMemoryRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private PlayerMemoryRepository CreateRepository() =>
        new(_db, new Mock<IDomainEventCollector>().Object);

    private IReadOnlyList<IProperty> Columns() =>
        _db.Model.FindEntityType(typeof(PlayerMemoryEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null && !NotOnTheAggregate.ContainsKey(p.Name))
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
            _ when type == typeof(DateTime) => SentinelDate.AddDays(index),
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }

    /// <summary>
    /// Ogni colonna valorizzata: <c>UserId</c> e <c>GuestName</c> insieme sono la memoria di un ospite reclamato.
    /// </summary>
    private async Task<PlayerMemoryEntity> SeedClaimedGuestAsync()
    {
        var seeded = new PlayerMemoryEntity();
        var columns = Columns();
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].PropertyInfo!.SetValue(seeded, SentinelFor(columns[i], i));
        }

        _db.PlayerMemories.Add(seeded);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return seeded;
    }

    private static object? DomainValue(string column, PlayerMemory memory)
    {
        if (string.Equals(column, nameof(PlayerMemoryEntity.GameStatsJson), StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(memory.GameStats
                .Select(s => new { s.GameId, s.Wins, s.Losses, s.TotalPlayed, s.BestScore })
                .ToArray());
        }

        var property = typeof(PlayerMemory).GetProperty(column);
        return property is null ? "<assente dall'aggregato>" : property.GetValue(memory);
    }

    public static TheoryData<string> ReadPaths => new()
    {
        nameof(PlayerMemoryRepository.GetByIdAsync),
        nameof(PlayerMemoryRepository.GetByUserIdAsync),
        nameof(PlayerMemoryRepository.GetByGroupIdAsync),
        nameof(PlayerMemoryRepository.GetAllByUserIdAsync),
    };

    private static async Task<PlayerMemory?> ReadAsync(
        PlayerMemoryRepository repository, string readPath, PlayerMemoryEntity seeded) => readPath switch
        {
            nameof(PlayerMemoryRepository.GetByIdAsync) => await repository.GetByIdAsync(seeded.Id),
            nameof(PlayerMemoryRepository.GetByUserIdAsync) => await repository.GetByUserIdAsync(seeded.UserId!.Value),
            nameof(PlayerMemoryRepository.GetByGroupIdAsync) => (await repository.GetByGroupIdAsync(seeded.GroupId!.Value)).Single(),
            nameof(PlayerMemoryRepository.GetAllByUserIdAsync) => (await repository.GetAllByUserIdAsync(seeded.UserId!.Value)).Single(),
            _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
        };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath)
    {
        var seeded = await SeedClaimedGuestAsync();

        var memory = await ReadAsync(CreateRepository(), readPath, seeded);

        memory.Should().NotBeNull();
        var lost = Columns()
            .Select(c => (Column: c.Name, Expected: c.PropertyInfo!.GetValue(seeded), Actual: DomainValue(c.Name, memory!)))
            .Where(x => !Equals(x.Expected, x.Actual))
            .Select(x => $"{x.Column}: «{x.Expected}» → «{x.Actual}»");
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored. Before #4552 a claimed " +
            "guest went through CreateForUser and lost GuestName");
    }

    [Fact]
    public async Task UpdateAsync_AfterAStatsUpdate_KeepsTheGuestNameOfAClaimedGuest()
    {
        // Il giro di OnSessionCompletedUpdateStatsHandler: legge per utente, aggiorna le statistiche, salva.
        var seeded = await SeedClaimedGuestAsync();
        var repository = CreateRepository();

        var memory = await repository.GetByUserIdAsync(seeded.UserId!.Value);
        memory!.UpdateGameStats(StatsGameId, won: true, score: 50);
        await repository.UpdateAsync(memory);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.PlayerMemories.AsNoTracking().SingleAsync(m => m.Id == seeded.Id);
        after.GuestName.Should().Be(seeded.GuestName,
            "the guest name of a claimed guest is data, and a stats update does not change it. Before #4552 the " +
            "update wrote it as NULL");
        after.UserId.Should().Be(seeded.UserId);
        after.ClaimedAt.Should().Be(seeded.ClaimedAt);
        after.CreatedAt.Should().Be(seeded.CreatedAt);
    }
}
