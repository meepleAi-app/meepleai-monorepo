using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// #4347: <see cref="PdfDocumentRepository"/>.<c>UpdateAsync</c> rimette sul contesto un'entità costruita da
/// <c>MapToPersistence</c> con <c>DbSet.Update</c>, che segna come modificate tutte le colonne. Una colonna
/// che il mapper non scrive veniva quindi salvata al valore di default della classe: l'avanzamento, il
/// testo estratto, la versione dell'indicizzatore, la priorità, la licenza, a ogni transizione di stato e
/// a ogni cambio di metadati.
/// <para>
/// Il primo test non elenca le colonne: le prende dal modello EF. Una colonna aggiunta domani all'entità,
/// senza mapping e senza esclusione, lo fa fallire con il suo nome.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4347")]
public sealed class PdfDocumentRepositoryUnownedColumnsTests : IDisposable
{
    private static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    // Valori che l'aggregato valida in lettura: la sentinella deve essere ammessa, e diversa dal default
    // dell'entità, così una colonna riportata al default si vede.
    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        [nameof(PdfDocumentEntity.FileName)] = "sentinella.pdf",
        // L'aggregato la fissa a questo valore: non è un dato della riga.
        [nameof(PdfDocumentEntity.ContentType)] = "application/pdf",
        [nameof(PdfDocumentEntity.FileSizeBytes)] = 4242L,
        [nameof(PdfDocumentEntity.ProcessingState)] = nameof(PdfProcessingState.Ready),
        [nameof(PdfDocumentEntity.ErrorCategory)] = nameof(ErrorCategory.Network),
        [nameof(PdfDocumentEntity.FailedAtState)] = nameof(PdfProcessingState.Embedding),
        [nameof(PdfDocumentEntity.Language)] = "it",
        [nameof(PdfDocumentEntity.LanguageOverride)] = "de",
        [nameof(PdfDocumentEntity.DocumentType)] = "errata",
        [nameof(PdfDocumentEntity.DocumentCategory)] = nameof(DocumentCategory.Expansion),
        [nameof(PdfDocumentEntity.CoverGenerationStatus)] = nameof(PdfCoverGenerationStatus.Generated),
        [nameof(PdfDocumentEntity.IsActiveForRag)] = false,
    };

    private readonly MeepleAiDbContext _db;

    public PdfDocumentRepositoryUnownedColumnsTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PdfDocRepoUnowned_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private PdfDocumentRepository CreateRepository() =>
        new(_db, new Mock<IDomainEventCollector>().Object);

    // Le colonne della riga, escluso il token di concorrenza, che cambia a ogni scrittura per contratto.
    private IReadOnlyList<IProperty> Columns() =>
        _db.Model.FindEntityType(typeof(PdfDocumentEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null && p.Name != nameof(PdfDocumentEntity.Xmin))
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
            _ when type == typeof(long) => 4242L,
            _ when type == typeof(double) => 0.42,
            _ when type == typeof(bool) => true,
            _ when type == typeof(DateTime) => SentinelDate,
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ when type == typeof(List<string>) => new List<string> { "sentinella" },
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }

    private static bool SameValue(object? expected, object? actual) =>
        expected is IEnumerable<string> expectedList && actual is IEnumerable<string> actualList
            ? expectedList.SequenceEqual(actualList)
            : Equals(expected, actual);

    [Fact]
    public async Task UpdateAsync_WithNoChange_LeavesEveryColumnAsItWas()
    {
        // Arrange: ogni colonna con un valore diverso dal suo default.
        var seeded = new PdfDocumentEntity();
        foreach (var column in Columns())
        {
            column.PropertyInfo!.SetValue(seeded, SentinelFor(column));
        }

        _db.PdfDocuments.Add(seeded);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var expected = Columns().ToDictionary(c => c.Name, c => c.PropertyInfo!.GetValue(seeded), StringComparer.Ordinal);

        // Act: lettura e scrittura dal repository, senza cambiare niente.
        var repository = CreateRepository();
        var document = await repository.GetByIdAsync(seeded.Id);
        await repository.UpdateAsync(document!);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Assert
        var after = await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == seeded.Id);
        var changed = Columns()
            .Where(c => !SameValue(expected[c.Name], c.PropertyInfo!.GetValue(after)))
            .Select(c => $"{c.Name}: «{expected[c.Name]}» → «{c.PropertyInfo!.GetValue(after)}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty(
            "un UpdateAsync che non cambia niente non deve cambiare niente; una colonna che il mapper non " +
            "scrive va in PdfDocumentRepository.ColumnsNotOwnedByTheAggregate");
    }

    [Fact]
    public void ColumnsNotOwnedByTheAggregate_AreNotPropertiesOfTheAggregate()
    {
        // Una colonna che l'aggregato porta, e che il mapper scrive, non va esclusa: le sue modifiche
        // andrebbero perse in silenzio.
        var ownedByTheAggregate = PdfDocumentRepository.ColumnsNotOwnedByTheAggregate
            .Where(name => typeof(PdfDocument).GetProperty(name) is not null)
            .ToList();

        ownedByTheAggregate.Should().BeEmpty();
    }

    [Fact]
    public void ColumnsNotOwnedByTheAggregate_AreColumnsOfTheRow()
    {
        var columns = Columns().Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

        PdfDocumentRepository.ColumnsNotOwnedByTheAggregate.Should().OnlyContain(name => columns.Contains(name));
    }
}
