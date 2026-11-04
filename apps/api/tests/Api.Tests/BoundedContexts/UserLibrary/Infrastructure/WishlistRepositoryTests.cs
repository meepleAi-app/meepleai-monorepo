using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Infrastructure;

/// <summary>
/// #4552: <see cref="WishlistRepository"/> ricostruiva la riga con <see cref="WishlistItem.Create"/>, la factory di
/// un elemento nuovo, e correggeva id e date via reflection. La visibilità no: tornava sempre
/// <see cref="WishlistVisibility.Private"/>, mentre il mapper inverso la scrive, quindi il primo aggiornamento
/// l'avrebbe riportata a privata.
/// <para>
/// La guardia prende le colonne dal modello EF, con una sentinella diversa per colonna: una colonna nuova senza
/// mapping, o due colonne scambiate nel mapper, la fanno fallire col nome.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4552")]
public sealed class WishlistRepositoryTests : IDisposable
{
    private static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    // Valori che l'aggregato legge come enum: ammessi, e diversi dal default (0) e fra loro.
    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        [nameof(WishlistItemEntity.Priority)] = (int)WishlistPriority.High,
        [nameof(WishlistItemEntity.Visibility)] = (int)WishlistVisibility.Friends,
    };

    private readonly MeepleAiDbContext _db;

    public WishlistRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"WishlistRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private WishlistRepository CreateRepository() =>
        new(_db, new Mock<IDomainEventCollector>().Object);

    private IReadOnlyList<IProperty> Columns() =>
        _db.Model.FindEntityType(typeof(WishlistItemEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null)
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
            _ when type == typeof(decimal) => 10.5m + index,
            _ when type == typeof(DateTime) => SentinelDate.AddDays(index),
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }

    private async Task<WishlistItemEntity> SeedEveryColumnAsync()
    {
        var seeded = new WishlistItemEntity();
        var columns = Columns();
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].PropertyInfo!.SetValue(seeded, SentinelFor(columns[i], i));
        }

        _db.WishlistItems.Add(seeded);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return seeded;
    }

    public static TheoryData<string> ReadPaths => new()
    {
        nameof(WishlistRepository.GetByIdAsync),
        nameof(WishlistRepository.GetAllAsync),
        nameof(WishlistRepository.GetByUserAndGameAsync),
        nameof(WishlistRepository.GetUserWishlistAsync),
        nameof(WishlistRepository.GetHighlightsAsync),
    };

    private static async Task<WishlistItem?> ReadAsync(
        WishlistRepository repository, string readPath, WishlistItemEntity seeded) => readPath switch
        {
            nameof(WishlistRepository.GetByIdAsync) => await repository.GetByIdAsync(seeded.Id),
            nameof(WishlistRepository.GetAllAsync) => (await repository.GetAllAsync()).Single(),
            nameof(WishlistRepository.GetByUserAndGameAsync) => await repository.GetByUserAndGameAsync(seeded.UserId, seeded.GameId),
            nameof(WishlistRepository.GetUserWishlistAsync) => (await repository.GetUserWishlistAsync(seeded.UserId)).Single(),
            nameof(WishlistRepository.GetHighlightsAsync) => (await repository.GetHighlightsAsync(seeded.UserId)).Single(),
            _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
        };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath)
    {
        var seeded = await SeedEveryColumnAsync();

        var item = await ReadAsync(CreateRepository(), readPath, seeded);

        item.Should().NotBeNull();
        var lost = Columns()
            .Select(c => (Column: c.Name, Expected: c.PropertyInfo!.GetValue(seeded), Domain: typeof(WishlistItem).GetProperty(c.Name)))
            .Select(x => (x.Column, x.Expected, Actual: x.Domain is null ? "<assente dall'aggregato>" : x.Domain.GetValue(item)))
            .Where(x => !Equals(x.Expected, x.Actual is Enum e ? Convert.ToInt32(e, System.Globalization.CultureInfo.InvariantCulture) : x.Actual))
            .Select(x => $"{x.Column}: «{x.Expected}» → «{x.Actual}»");
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored. Before #4552 Visibility " +
            "came back as Private whatever was stored");
        item!.DomainEvents.Should().BeEmpty("reading a stored item is not adding it to the wishlist");
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_LeavesEveryColumnAsItWas()
    {
        var seeded = await SeedEveryColumnAsync();
        var expected = Columns().ToDictionary(c => c.Name, c => c.PropertyInfo!.GetValue(seeded), StringComparer.Ordinal);

        var repository = CreateRepository();
        var item = await repository.GetByIdAsync(seeded.Id);
        await repository.UpdateAsync(item!);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.WishlistItems.AsNoTracking().SingleAsync(w => w.Id == seeded.Id);
        var changed = Columns()
            .Where(c => !Equals(expected[c.Name], c.PropertyInfo!.GetValue(after)))
            .Select(c => $"{c.Name}: «{expected[c.Name]}» → «{c.PropertyInfo!.GetValue(after)}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty(
            "an UpdateAsync that changes nothing must change nothing. Before #4552 it wrote Visibility back as Private");
    }
}
