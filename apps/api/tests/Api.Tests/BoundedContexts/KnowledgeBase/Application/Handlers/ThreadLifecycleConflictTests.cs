using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Issue #3224: closing an already-closed thread / reopening an already-active thread must map to
/// 409 Conflict, not 500. Before the fix the handlers let the domain guard throw a bare
/// InvalidOperationException which the middleware maps to 500.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3224")]
public sealed class ThreadLifecycleConflictTests
{
    private readonly Mock<IChatThreadRepository> _threadRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    [Fact]
    public async Task CloseThread_AlreadyClosed_ThrowsConflictException()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        thread.CloseThread(); // now Closed
        _threadRepoMock.Setup(r => r.GetByIdAsync(thread.Id, It.IsAny<CancellationToken>())).ReturnsAsync(thread);

        var handler = new CloseThreadCommandHandler(_threadRepoMock.Object, _uowMock.Object);
        Func<Task> act = () => handler.Handle(new CloseThreadCommand(thread.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already closed*");
        _threadRepoMock.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReopenThread_AlreadyActive_ThrowsConflictException()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid()); // Active by default
        _threadRepoMock.Setup(r => r.GetByIdAsync(thread.Id, It.IsAny<CancellationToken>())).ReturnsAsync(thread);

        var handler = new ReopenThreadCommandHandler(_threadRepoMock.Object, _uowMock.Object);
        Func<Task> act = () => handler.Handle(new ReopenThreadCommand(thread.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already active*");
        _threadRepoMock.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
