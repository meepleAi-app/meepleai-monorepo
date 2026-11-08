using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4155. Il diniego RAG ha un tipo e un codice: chi lo riceve deve poter dire all'utente
/// «dichiara di possedere il gioco», non «An error occurred».
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4155")]
public sealed class RagAccessGuardTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    [Fact]
    public async Task EnsureRagAccessAsync_WhenDenied_ThrowsRagOwnershipRequired_CarryingTheGame()
    {
        var access = new Mock<IRagAccessService>();
        access.Setup(s => s.CanAccessRagAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => access.Object.EnsureRagAccessAsync(UserId, GameId, UserRole.User, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<RagOwnershipRequiredException>();
        thrown.Which.GameId.Should().Be(GameId);
        thrown.Which.ErrorCode.Should().Be("RAG_OWNERSHIP_REQUIRED");
        thrown.Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task EnsureRagAccessAsync_WhenAllowed_DoesNotThrow()
    {
        var access = new Mock<IRagAccessService>();
        access.Setup(s => s.CanAccessRagAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => access.Object.EnsureRagAccessAsync(UserId, GameId, UserRole.User, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void RagOwnershipRequiredException_IsAForbiddenException()
    {
        // Chi oggi intercetta ForbiddenException (endpoint con catch dedicati, test con
        // ThrowAsync<ForbiddenException>) deve continuare a vederlo come un 403.
        new RagOwnershipRequiredException(GameId).Should().BeAssignableTo<ForbiddenException>();
    }
}
