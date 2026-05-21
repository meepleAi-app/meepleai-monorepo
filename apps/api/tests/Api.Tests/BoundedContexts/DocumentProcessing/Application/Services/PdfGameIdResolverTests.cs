using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for <see cref="PdfGameIdResolver"/>.
/// Verifies resolution of <c>text_chunks.GameId</c> from a <see cref="PdfDocumentEntity"/>:
/// private path returns PrivateGameId; shared path returns SharedGameId directly.
/// Post-Phase2d (#1345): legacy <c>games</c> table is gone; no lookup needed — resolver
/// returns the PDF's SharedGameId/PrivateGameId directly.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfGameIdResolverTests
{
    private static MeepleAiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    [Fact]
    public async Task ResolveAsync_PrivateGameSet_ReturnsPrivateGameId()
    {
        using var db = CreateDbContext();
        var privateGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = privateGameId,
            FileName = "x.pdf",
            FilePath = "/tmp/x.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        };

        var result = await PdfGameIdResolver.ResolveAsync(db, pdf, TestContext.Current.CancellationToken);

        result.Should().Be(privateGameId);
    }

    [Fact]
    public async Task ResolveAsync_NoPrivateNoShared_ReturnsNull()
    {
        using var db = CreateDbContext();
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "x.pdf",
            FilePath = "/tmp/x.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        };

        var result = await PdfGameIdResolver.ResolveAsync(db, pdf, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_SharedGameIdSet_ReturnsSharedGameId()
    {
        // Post-Phase2d (#1345): resolver returns SharedGameId directly, no DB lookup.
        using var db = CreateDbContext();
        var sharedGameId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "x.pdf",
            FilePath = "/tmp/x.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        };

        var result = await PdfGameIdResolver.ResolveAsync(db, pdf, TestContext.Current.CancellationToken);

        result.Should().Be(sharedGameId);
    }

    [Fact]
    public async Task ResolveAsync_PrivateOverridesShared_ReturnsPrivateGameId()
    {
        using var db = CreateDbContext();
        var privateGameId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var sharedGameId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = privateGameId,
            SharedGameId = sharedGameId,
            FileName = "x.pdf",
            FilePath = "/tmp/x.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        };

        var result = await PdfGameIdResolver.ResolveAsync(db, pdf, TestContext.Current.CancellationToken);

        result.Should().Be(privateGameId);
    }

    [Fact]
    public async Task ResolveAsync_NullDb_Throws()
    {
        var pdf = new PdfDocumentEntity { Id = Guid.NewGuid() };

        var act = async () => await PdfGameIdResolver.ResolveAsync(null!, pdf, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ResolveAsync_NullPdf_Throws()
    {
        using var db = CreateDbContext();

        var act = async () => await PdfGameIdResolver.ResolveAsync(db, null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
