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
/// InvalidOperationException which the middleware maps to 500. Issue #4998: the same for switching the intent
/// of a closed thread.
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

    [Fact]
    public async Task SwitchIntent_OnAClosedThread_ThrowsConflictException()
    {
        // #4998: same shape as the two above. The domain guard threw a bare InvalidOperationException, a 500.
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        thread.CloseThread();
        _threadRepoMock.Setup(r => r.GetByIdAsync(thread.Id, It.IsAny<CancellationToken>())).ReturnsAsync(thread);

        var handler = new SwitchThreadAgentCommandHandler(_threadRepoMock.Object, _uowMock.Object);
        Func<Task> act = () => handler.Handle(new SwitchThreadAgentCommand(thread.Id, "tutor"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*closed*");
        thread.AgentType.Should().BeNull("a refused switch leaves the intent as it was");
        _threadRepoMock.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
