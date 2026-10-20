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
[Trait("Issue", "4558")]
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

    // revoked: false lascia revoked_at a NULL. UpdateAsync scrive solo su un link non revocato (#4558), quindi
    // i test di scrittura partono da lì; quelli di lettura tengono la sentinella anche su revoked_at.
    private async Task<LibraryShareLinkEntity> SeedEveryColumnAsync(bool revoked = true)
    {
        var seeded = new LibraryShareLinkEntity();
        var columns = Columns();
        for (var index = 0; index < columns.Count; index++)
        {
            columns[index].PropertyInfo!.SetValue(seeded, SentinelFor(columns[index], index));
        }

        if (!revoked)
        {
            seeded.RevokedAt = null;
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
        var seeded = await SeedEveryColumnAsync(revoked: false);
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

    // #4558: una scrittura del proprietario non deve cancellare ciò che un'altra richiesta ha salvato fra la
    // sua lettura e la sua scrittura. Qui la richiesta "altra" è simulata scrivendo la riga direttamente.

    [Fact]
    public async Task UpdateAsync_DoesNotWriteBackTheVisitCounters()
    {
        var seeded = await SeedEveryColumnAsync(revoked: false);
        var repository = CreateRepository();
        var link = await repository.GetByIdAsync(seeded.Id);

        // Una visita contata dopo la lettura del proprietario.
        var visitedAt = SentinelDate.AddDays(1);
        await ChangeRowAsync(seeded.Id, row =>
        {
            row.ViewCount = seeded.ViewCount + 1;
            row.LastAccessedAt = visitedAt;
        });

        link!.UpdateIncludeNotes(!seeded.IncludeNotes);
        await repository.UpdateAsync(link);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.LibraryShareLinks.AsNoTracking().SingleAsync(l => l.Id == seeded.Id);
        after.IncludeNotes.Should().Be(!seeded.IncludeNotes, "the owner's change is saved");
        after.ViewCount.Should().Be(seeded.ViewCount + 1,
            "the counters belong to the visit (TryRecordAccessAsync): the owner's write must not bring them back to what it read");
        after.LastAccessedAt.Should().Be(visitedAt);
    }

    [Fact]
    public async Task UpdateAsync_OnALinkRevokedAfterTheRead_IsRefusedAndTheRevocationStays()
    {
        var seeded = await SeedEveryColumnAsync(revoked: false);
        var repository = CreateRepository();
        var link = await repository.GetByIdAsync(seeded.Id);

        var revokedAt = SentinelDate.AddDays(2);
        await ChangeRowAsync(seeded.Id, row => row.RevokedAt = revokedAt);

        link!.UpdateIncludeNotes(!seeded.IncludeNotes);
        await repository.UpdateAsync(link);
        var save = () => _db.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the owner read a link that was not revoked, and the link was revoked before the write: writing it " +
            "back would set revoked_at to NULL again");
        _db.ChangeTracker.Clear();

        var after = await _db.LibraryShareLinks.AsNoTracking().SingleAsync(l => l.Id == seeded.Id);
        after.RevokedAt.Should().Be(revokedAt, "a revocation is never undone");
        after.IncludeNotes.Should().Be(seeded.IncludeNotes, "the refused write changes nothing");
    }

    // AsTracking: MeepleAiDbContext legge NoTracking per default, e una modifica a una riga non tracciata
    // non verrebbe salvata — il test passerebbe o fallirebbe senza che la "richiesta concorrente" sia mai avvenuta.
    private async Task ChangeRowAsync(Guid id, Action<LibraryShareLinkEntity> change)
    {
        var row = await _db.LibraryShareLinks.AsTracking().SingleAsync(l => l.Id == id);
        change(row);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }
}
