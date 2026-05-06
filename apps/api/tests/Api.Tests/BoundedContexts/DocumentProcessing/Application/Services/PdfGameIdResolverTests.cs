using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
/// Verifies FK-correct resolution of <c>text_chunks.GameId</c> from a <see cref="PdfDocumentEntity"/>:
/// private path returns PrivateGameId; shared path looks up <c>games.Id</c> via SharedGameId.
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
    public async Task ResolveAsync_SharedGameWithoutMatchingGamesRow_ReturnsNull()
    {
        using var db = CreateDbContext();
        var sharedGameId = Guid.Parse("22222222-2222-2222-2222-222222222222");
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

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_SharedGameWithMatchingGamesRow_ReturnsGamesId()
    {
        using var db = CreateDbContext();
        var sharedGameId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var gamesId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        db.Games.Add(new GameEntity
        {
            Id = gamesId,
            Name = "Test",
            SharedGameId = sharedGameId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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

        result.Should().Be(gamesId);
    }

    [Fact]
    public async Task ResolveAsync_PrivateOverridesShared_ReturnsPrivateGameId()
    {
        using var db = CreateDbContext();
        var privateGameId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var sharedGameId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var gamesId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        db.Games.Add(new GameEntity
        {
            Id = gamesId,
            Name = "Should not be returned",
            SharedGameId = sharedGameId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
