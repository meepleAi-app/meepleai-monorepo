using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Tests for GetSharedLibraryQuery validation and share link behavior.
/// Note: Full handler tests require integration testing due to DbContext dependencies.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetSharedLibraryQueryHandlerTests
{
    private readonly Mock<ILibraryShareLinkRepository> _mockShareLinkRepository;

    public GetSharedLibraryQueryHandlerTests()
    {
        _mockShareLinkRepository = new Mock<ILibraryShareLinkRepository>();
    }

    #region Query Validation Tests

    [Fact]
    public void Query_WithValidToken_CreatesSuccessfully()
    {
        // Arrange & Act
        var query = new GetSharedLibraryQuery("12345678901234567890123456789012");

        // Assert
        query.Should().NotBeNull();
        query.ShareToken.Should().Be("12345678901234567890123456789012");
    }

    #endregion

    #region Share Link Validity Tests

    [Fact]
    public void ShareLink_WhenRevoked_IsInvalid()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            true,
            null);

        // Act
        shareLink.Revoke();

        // Assert
        shareLink.IsValid.Should().BeFalse();
        shareLink.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void ShareLink_WhenExpired_IsInvalid()
    {
        // Arrange - Create valid link first (domain validation prevents creating with past date)
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            true,
            null);

        // Use reflection to set ExpiresAt to a past date (simulating time passage)
        var expiresAtProperty = typeof(LibraryShareLink).GetProperty("ExpiresAt");
        expiresAtProperty!.SetValue(shareLink, DateTime.UtcNow.AddDays(-1)); // Expired yesterday

        // Assert
        shareLink.IsExpired.Should().BeTrue();
        shareLink.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ShareLink_WhenActive_IsValid()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            true,
            DateTime.UtcNow.AddDays(7)); // Expires in 7 days

        // Assert
        shareLink.IsValid.Should().BeTrue();
    }

    #endregion

    #region Repository Behavior Tests

    [Fact]
    public async Task Repository_GetByShareToken_ReturnsNullForNonExistent()
    {
        // Arrange
        var shareToken = "nonexistenttoken12345678901234";

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _mockShareLinkRepository.Object.GetByShareTokenAsync(
            shareToken, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Repository_GetByShareToken_ReturnsShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _mockShareLinkRepository.Object.GetByShareTokenAsync(
            shareToken, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ShareToken.Should().Be(shareToken);
        result.UserId.Should().Be(userId);
    }

    #endregion

    #region Visit Counting Tests (#4558)

    // La visita conta sé stessa con TryRecordAccessAsync, che controlla la validità nello stesso statement.
    // Non riscrive mai l'aggregato: prima di #4558 lo faceva, e una revoca concorrente tornava NULL.

    [Fact]
    public async Task Handle_LinkRevokedBeforeTheVisitCounts_ReturnsNullAndNeverRewritesTheLink()
    {
        var shareLink = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, true, null);
        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareLink.ShareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);
        _mockShareLinkRepository
            .Setup(r => r.TryRecordAccessAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        // Il proprietario esiste, e la libreria è un mock stretto: un handler che ignorasse l'esito del
        // conteggio andrebbe avanti fino a leggerla, e il test lo vedrebbe.
        var libraryRepository = new Mock<IUserLibraryRepository>(MockBehavior.Strict);
        using var db = CreateDbContext();
        db.Users.Add(new UserEntity { Id = shareLink.UserId, Email = "owner@example.com", DisplayName = "Owner" });
        await db.SaveChangesAsync();

        var result = await CreateHandler(libraryRepository.Object, db)
            .Handle(new GetSharedLibraryQuery(shareLink.ShareToken), CancellationToken.None);

        result.Should().BeNull("the link was valid when read but no longer when the visit counted itself: 404");
        _mockShareLinkRepository.Verify(r => r.UpdateAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()), Times.Never(),
            "a visit never writes the aggregate back: that write is what undid a concurrent revocation");
    }

    [Fact]
    public async Task Handle_LinkStillValid_CountsTheVisitOnceAndNeverRewritesTheLink()
    {
        var shareLink = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, true, null);
        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareLink.ShareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);
        _mockShareLinkRepository
            .Setup(r => r.TryRecordAccessAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        using var db = CreateDbContext();

        // Il proprietario non è nel database: l'handler si ferma dopo aver contato la visita.
        await CreateHandler(new Mock<IUserLibraryRepository>().Object, db)
            .Handle(new GetSharedLibraryQuery(shareLink.ShareToken), CancellationToken.None);

        _mockShareLinkRepository.Verify(r => r.TryRecordAccessAsync(shareLink.Id, It.IsAny<CancellationToken>()), Times.Once());
        _mockShareLinkRepository.Verify(r => r.UpdateAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_LinkAlreadyRevokedWhenRead_DoesNotCountTheVisit()
    {
        var shareLink = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, true, null);
        shareLink.Revoke();
        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareLink.ShareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);
        using var db = CreateDbContext();

        var result = await CreateHandler(new Mock<IUserLibraryRepository>().Object, db)
            .Handle(new GetSharedLibraryQuery(shareLink.ShareToken), CancellationToken.None);

        result.Should().BeNull();
        _mockShareLinkRepository.Verify(r => r.TryRecordAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private GetSharedLibraryQueryHandler CreateHandler(IUserLibraryRepository libraryRepository, MeepleAiDbContext db) =>
        new(_mockShareLinkRepository.Object, libraryRepository, db, NullLogger<GetSharedLibraryQueryHandler>.Instance);

    private static MeepleAiDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<MeepleAiDbContext>()
                .UseInMemoryDatabase($"GetSharedLibrary_{Guid.NewGuid()}")
                .Options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

    #endregion
}
