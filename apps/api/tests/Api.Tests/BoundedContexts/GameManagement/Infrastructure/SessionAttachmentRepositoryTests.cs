using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
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
/// #4552: <see cref="SessionAttachmentRepository"/> ricostruiva la riga con <see cref="SessionAttachment.Create"/>,
/// la factory di un allegato nuovo, senza ripristinare nulla: ogni lettura restituiva un id che nel database non
/// esiste e la data di creazione ad "adesso". Effetti: gli id in elenco non si potevano cancellare (404), finivano
/// così nello snapshot salvato, e <c>SessionAttachmentCleanupJob</c> cancellava per un id inesistente, quindi
/// rileggeva lo stesso lotto di allegati scaduti senza fine.
/// <para>
/// La guardia prende le colonne dal modello EF, con una sentinella diversa per colonna: una colonna nuova senza
/// mapping, o due colonne scambiate nel mapper, la fanno fallire col nome.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4552")]
public sealed class SessionAttachmentRepositoryTests : IDisposable
{
    // Nel passato, oltre la soglia di conservazione: la riga è quella che il job di pulizia legge.
    private static readonly DateTime SentinelDate = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    // Valori che la factory di creazione ammette (così il test fallisce per ciò che perde, non per una
    // validazione), e diversi dal default dell'entità.
    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        [nameof(SessionAttachmentEntity.AttachmentType)] = (int)AttachmentType.CharacterSheet,
        [nameof(SessionAttachmentEntity.ContentType)] = "image/png",
        [nameof(SessionAttachmentEntity.FileSizeBytes)] = 4096L,
        // Il filtro di query nasconde le righe cancellate: la sentinella è il valore che si legge.
        [nameof(SessionAttachmentEntity.IsDeleted)] = false,
    };

    private readonly MeepleAiDbContext _db;

    public SessionAttachmentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"SessionAttachmentRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private SessionAttachmentRepository CreateRepository() =>
        new(_db, new Mock<IDomainEventCollector>().Object);

    private IReadOnlyList<IProperty> Columns() =>
        _db.Model.FindEntityType(typeof(SessionAttachmentEntity))!
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
            _ when type == typeof(int) => 7 + index,
            _ when type == typeof(long) => 2048L + index,
            _ when type == typeof(bool) => true,
            _ when type == typeof(DateTime) => SentinelDate.AddDays(-index),
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }

    private async Task<SessionAttachmentEntity> SeedEveryColumnAsync()
    {
        var seeded = new SessionAttachmentEntity();
        var columns = Columns();
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].PropertyInfo!.SetValue(seeded, SentinelFor(columns[i], i));
        }

        _db.SessionAttachments.Add(seeded);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return seeded;
    }

    public static TheoryData<string> ReadPaths => new()
    {
        nameof(SessionAttachmentRepository.GetByIdAsync),
        nameof(SessionAttachmentRepository.GetBySessionIdAsync),
        nameof(SessionAttachmentRepository.GetBySnapshotAsync),
        nameof(SessionAttachmentRepository.GetExpiredAttachmentsAsync),
    };

    private static async Task<SessionAttachment?> ReadAsync(
        SessionAttachmentRepository repository, string readPath, SessionAttachmentEntity seeded) => readPath switch
        {
            nameof(SessionAttachmentRepository.GetByIdAsync) => await repository.GetByIdAsync(seeded.Id),
            nameof(SessionAttachmentRepository.GetBySessionIdAsync) => (await repository.GetBySessionIdAsync(seeded.SessionId)).Single(),
            nameof(SessionAttachmentRepository.GetBySnapshotAsync) =>
                (await repository.GetBySnapshotAsync(seeded.SessionId, seeded.SnapshotIndex!.Value)).Single(),
            nameof(SessionAttachmentRepository.GetExpiredAttachmentsAsync) =>
                (await repository.GetExpiredAttachmentsAsync(DateTime.UtcNow, batchSize: 10)).Single(),
            _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
        };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath)
    {
        var seeded = await SeedEveryColumnAsync();

        var attachment = await ReadAsync(CreateRepository(), readPath, seeded);

        attachment.Should().NotBeNull();
        var lost = Columns()
            .Select(c => (Column: c.Name, Expected: c.PropertyInfo!.GetValue(seeded), Domain: typeof(SessionAttachment).GetProperty(c.Name)))
            .Select(x => (x.Column, x.Expected, Actual: x.Domain is null ? "<assente dall'aggregato>" : x.Domain.GetValue(attachment)))
            .Where(x => !Equals(x.Expected, x.Actual is Enum e ? Convert.ToInt32(e, System.Globalization.CultureInfo.InvariantCulture) : x.Actual))
            .Select(x => $"{x.Column}: «{x.Expected}» → «{x.Actual}»");
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the attachment as it was stored: an attachment built by " +
            "the factory of a NEW one has a new id and the current time");
    }

    [Fact]
    public async Task SoftDeleteAsync_WithTheIdOfAnExpiredAttachment_DeletesTheRow()
    {
        // Il giro di SessionAttachmentCleanupJob: legge gli allegati scaduti e li cancella per id.
        var seeded = await SeedEveryColumnAsync();
        var repository = CreateRepository();

        var expired = await repository.GetExpiredAttachmentsAsync(DateTime.UtcNow, batchSize: 10);
        var deleted = await repository.SoftDeleteAsync(expired.Single().Id);
        await repository.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        deleted.Should().BeTrue(
            "the id of a read attachment must be the id of its row. Before #4552 it was a new one, the delete " +
            "found nothing, and the cleanup job read the same batch again forever");
        (await repository.GetExpiredAttachmentsAsync(DateTime.UtcNow, batchSize: 10)).Should().BeEmpty(
            "a deleted attachment leaves the batch, so the cleanup loop ends");
        var row = await _db.SessionAttachments.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == seeded.Id);
        row.IsDeleted.Should().BeTrue();
    }
}
