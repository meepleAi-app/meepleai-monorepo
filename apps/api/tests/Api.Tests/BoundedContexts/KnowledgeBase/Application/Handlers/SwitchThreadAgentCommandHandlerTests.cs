using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// #4998 — <c>PATCH /chat-threads/{id}/agent</c> answers with the thread it changed, intent included. Before, the
/// DTO left <c>AgentType</c> out, so the caller could not see the intent it had just written; the GET by id
/// returned it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4998")]
public sealed class SwitchThreadAgentCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _threadRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    [Fact]
    public async Task Handle_ReturnsTheThreadWithTheIntentItWrote_Normalized()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid(), gameId: Guid.NewGuid());
        _threadRepoMock.Setup(r => r.GetByIdAsync(thread.Id, It.IsAny<CancellationToken>())).ReturnsAsync(thread);
        var handler = new SwitchThreadAgentCommandHandler(_threadRepoMock.Object, _uowMock.Object);

        var result = await handler.Handle(new SwitchThreadAgentCommand(thread.Id, " Secca "), CancellationToken.None);

        result.AgentType.Should().Be("secca");
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
