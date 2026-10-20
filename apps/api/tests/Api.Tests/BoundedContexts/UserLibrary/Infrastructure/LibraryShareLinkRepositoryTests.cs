using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Infrastructure;

/// <summary>
/// #4544: <see cref="LibraryShareLinkRepository"/> ricostruiva la riga con <see cref="LibraryShareLink.Create"/>,
/// la factory di un link nuovo, che rifiuta una scadenza passata. Un link scaduto diventava illeggibile:
/// ogni caricamento lanciava <see cref="ArgumentException"/>.
/// <para>
/// I test sul giro completo non elencano le colonne: le prendono dal modello EF. Una colonna aggiunta
/// domani all'entità senza mapping in entrambe le direzioni li fa fallire con il suo nome.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4544")]
public sealed class LibraryShareLinkRepositoryTests : IDisposable
{
    // Nel passato: la riga è quella di un link scaduto, il caso che la factory di creazione rifiuta. Ogni
    // colonna data riceve un'ora diversa a partire da questa base, sempre nel passato.
    private static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    // Valori che l'aggregato legge come tipo di dominio: la sentinella deve essere ammessa, e diversa dal
    // default dell'entità, così una colonna riportata al default si vede.
    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        [nameof(LibraryShareLinkEntity.PrivacyLevel)] = 1, // Unlisted
    };

    private readonly MeepleAiDbContext _db;

    public LibraryShareLinkRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"LibraryShareLinkRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private LibraryShareLinkRepository CreateRepository() =>
        new(_db, new Mock<IDomainEventCollector>().Object);

    private IReadOnlyList<IProperty> Columns() =>
        _db.Model.FindEntityType(typeof(LibraryShareLinkEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null)
            .ToList();

    // Una sentinella diversa per ogni colonna, ricavata dalla sua posizione nel modello: due colonne dello
    // stesso tipo con lo stesso valore lascerebbero verde un mapper che le scambia (revokedAt: entity.ExpiresAt).
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
            _ when type == typeof(DateTime) => SentinelDate.AddHours(index),
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }

    private async Task<LibraryShareLinkEntity> SeedEveryColumnAsync()
    {
        var seeded = new LibraryShareLinkEntity();
        var columns = Columns();
        for (var index = 0; index < columns.Count; index++)
        {
            columns[index].PropertyInfo!.SetValue(seeded, SentinelFor(columns[index], index));
        }

        // Con due sentinelle uguali il confronto per colonna non vedrebbe uno scambio fra quelle due.
        columns.Select(c => c.PropertyInfo!.GetValue(seeded)).Should().OnlyHaveUniqueItems(
            "setup: every column needs its own sentinel, or a mapper that swaps two columns stays green");

        _db.LibraryShareLinks.Add(seeded);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return seeded;
    }

    [Fact]
    public async Task GetByShareTokenAsync_ExpiredLink_IsReconstitutedAsExpired()
    {
        var seeded = await SeedEveryColumnAsync();

        var link = await CreateRepository().GetByShareTokenAsync(seeded.ShareToken);

        link.Should().NotBeNull();
        link!.IsExpired.Should().BeTrue("the row expired on {0:O}", seeded.ExpiresAt);
        link.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ReconstitutesEveryColumnOfTheRow()
    {
        var seeded = await SeedEveryColumnAsync();

        var link = await CreateRepository().GetByIdAsync(seeded.Id);

        link.Should().NotBeNull();
        var lost = Columns()
            .Select(c => (Column: c.Name, Expected: c.PropertyInfo!.GetValue(seeded), Domain: typeof(LibraryShareLink).GetProperty(c.Name)))
            .Select(x => (x.Column, x.Expected, Actual: x.Domain is null ? "<assente dall'aggregato>" : x.Domain.GetValue(link)))
            .Where(x => !Equals(x.Expected, x.Actual is Enum e ? Convert.ToInt32(e, System.Globalization.CultureInfo.InvariantCulture) : x.Actual))
            .Select(x => $"{x.Column}: «{x.Expected}» → «{x.Actual}»");
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored");
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_LeavesEveryColumnAsItWas()
    {
        var seeded = await SeedEveryColumnAsync();
        var expected = Columns().ToDictionary(c => c.Name, c => c.PropertyInfo!.GetValue(seeded), StringComparer.Ordinal);

        var repository = CreateRepository();
        var link = await repository.GetByIdAsync(seeded.Id);
        await repository.UpdateAsync(link!);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.LibraryShareLinks.AsNoTracking().SingleAsync(l => l.Id == seeded.Id);
        var changed = Columns()
            .Where(c => !Equals(expected[c.Name], c.PropertyInfo!.GetValue(after)))
            .Select(c => $"{c.Name}: «{expected[c.Name]}» → «{c.PropertyInfo!.GetValue(after)}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty(
            "an UpdateAsync that changes nothing must change nothing: a column MapToPersistence does not " +
            "write is saved as its default (DbSet.Update marks every column modified)");
    }
}
