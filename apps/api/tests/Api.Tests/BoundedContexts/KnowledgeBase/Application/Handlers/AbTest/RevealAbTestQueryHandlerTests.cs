using Api.BoundedContexts.KnowledgeBase.Application.Queries.AbTest;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AbTest;

/// <summary>
/// Unit tests for RevealAbTestQueryHandler.
/// Issue #3210: revealing an A/B test before evaluation is complete is a conflict with the
/// current resource state (409), not an internal server error (500). Before this fix the
/// handler threw a bare <see cref="InvalidOperationException"/> which the middleware mapped to 500.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3210")]
public sealed class RevealAbTestQueryHandlerTests
{
    private readonly Mock<IAbTestSessionRepository> _repoMock = new();
    private static readonly Guid UserId = Guid.NewGuid();

    private RevealAbTestQueryHandler CreateSut() => new(_repoMock.Object);

    [Fact]
    public async Task Handle_SessionNotEvaluated_ThrowsConflictException()
    {
        // A freshly created session is in Draft status (not Evaluated).
        var session = AbTestSession.Create(UserId, "Which model answers better?");
        _repoMock.Setup(r => r.GetByIdWithVariantsAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var sut = CreateSut();
        Func<Task> act = () => sut.Handle(new RevealAbTestQuery(session.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*before evaluation*");
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByIdWithVariantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AbTestSession?)null);

        var sut = CreateSut();
        var result = await sut.Handle(new RevealAbTestQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }
}
