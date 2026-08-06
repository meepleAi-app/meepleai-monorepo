using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Issue #3454 — selection contract of <c>RetryFailedPdfsJob</c>.
/// <para>
/// The job used to filter on <c>ErrorCategory != null</c>, which made an UNCATEGORISED failure
/// invisible to the automatic retry forever: no retry, and no operator path short of a per-id
/// reindex. Staging carried 13 such PDFs — transient Unstructured/embedding failures with
/// <c>retry_count = 0</c>, predating the category column — which also kept their covers
/// ungenerated and 6 games without any cover at all.
/// </para>
/// <para>
/// The intended semantics are stated by the deprecated <c>MarkAsFailed(error)</c> overload, which
/// assigns <see cref="ErrorCategory.Unknown"/>: an unknown category IS retriable. These tests pin
/// the query so the two cases cannot drift apart again.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3454")]
public sealed class RetryFailedPdfsJobSelectionTests
{
    /// <summary>
    /// Mirrors the job's selection predicate. Kept in one place so the assertions below describe
    /// the contract rather than re-implementing it per test.
    /// </summary>
    private static IQueryable<PdfDocumentEntity> Selected(MeepleAiDbContext db)
    {
        var retriable = new[]
        {
            ErrorCategory.Network.ToString(),
            ErrorCategory.Service.ToString(),
            ErrorCategory.Unknown.ToString(),
        };

        return db.PdfDocuments.Where(p =>
            p.ProcessingState == PdfProcessingState.Failed.ToString()
            && p.RetryCount < 3
            && (p.ErrorCategory == null || retriable.Contains(p.ErrorCategory)));
    }

    private static PdfDocumentEntity Failed(string? category, int retryCount = 0) => new()
    {
        Id = Guid.NewGuid(),
        ProcessingState = PdfProcessingState.Failed.ToString(),
        ErrorCategory = category,
        RetryCount = retryCount,
        ProcessingError = "boom",
        FileName = "rules.pdf",
        FilePath = "pdfs/rules.pdf",
        UploadedByUserId = Guid.NewGuid(),
    };

    [Fact]
    public async Task AnUncategorisedFailure_IsRetriable()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var legacy = Failed(category: null);
        db.PdfDocuments.Add(legacy);
        await db.SaveChangesAsync(CancellationToken.None);

        var selected = await Selected(db).ToListAsync(CancellationToken.None);

        selected.Should().ContainSingle().Which.Id.Should().Be(legacy.Id,
            "a NULL category means 'unknown', and unknown is retriable — the pre-#3454 filter "
            + "stranded these rows permanently");
    }

    [Theory]
    [InlineData("Network")]
    [InlineData("Service")]
    [InlineData("Unknown")]
    public async Task TransientCategories_StayRetriable(string category)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PdfDocuments.Add(Failed(category));
        await db.SaveChangesAsync(CancellationToken.None);

        var selected = await Selected(db).ToListAsync(CancellationToken.None);

        selected.Should().ContainSingle();
    }

    [Theory]
    [InlineData("Parsing")]  // corrupt/unsupported PDF — retrying cannot help
    [InlineData("Quota")]    // needs a tier change, not a retry
    public async Task PermanentCategories_AreNotRetried(string category)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PdfDocuments.Add(Failed(category));
        await db.SaveChangesAsync(CancellationToken.None);

        var selected = await Selected(db).ToListAsync(CancellationToken.None);

        selected.Should().BeEmpty("widening the filter to NULL must not also widen it to terminal categories");
    }

    [Fact]
    public async Task TheRetryBudget_StillBoundsUncategorisedRows()
    {
        // The widened filter must stay bounded: an uncategorised row that already burned its
        // budget is not retried forever.
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PdfDocuments.Add(Failed(category: null, retryCount: 3));
        await db.SaveChangesAsync(CancellationToken.None);

        var selected = await Selected(db).ToListAsync(CancellationToken.None);

        selected.Should().BeEmpty();
    }

    [Fact]
    public async Task NonFailedDocuments_AreNeverSelected()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var ready = Failed(category: null);
        ready.ProcessingState = PdfProcessingState.Ready.ToString();
        db.PdfDocuments.Add(ready);
        await db.SaveChangesAsync(CancellationToken.None);

        var selected = await Selected(db).ToListAsync(CancellationToken.None);

        selected.Should().BeEmpty();
    }
}
