using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// #4613: <see cref="ChatSession.Reconstitute"/> rimette in piedi una chat salvata, senza passare dal costruttore
/// di una chat nuova.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4613")]
public sealed class ChatSessionReconstituteTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static ChatSession Reconstitute(
        bool isArchived = false,
        string? title = "titolo",
        IEnumerable<SessionChatMessage>? messages = null) =>
        ChatSession.Reconstitute(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            userLibraryEntryId: Guid.NewGuid(),
            agentSessionId: Guid.NewGuid(),
            agentId: Guid.NewGuid(),
            agentType: "tutor",
            agentName: "Tutor",
            title: title,
            agentConfigJson: "{\"a\":1}",
            createdAt: CreatedAt,
            lastMessageAt: CreatedAt.AddMinutes(9),
            isArchived: isArchived,
            messages: messages ?? []);

    [Fact]
    public void Reconstitute_KeepsEveryValueAsStored()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var message = SessionChatMessage.FromPersistence(Guid.NewGuid(), "ciao", "user", 0, CreatedAt, null);

        var session = ChatSession.Reconstitute(
            id, userId, gameId, userLibraryEntryId: null, agentSessionId: null, agentId: null, agentType: "auto",
            agentName: null, title: " titolo con spazi ", agentConfigJson: "{}", createdAt: CreatedAt,
            lastMessageAt: CreatedAt.AddMinutes(9), isArchived: true, messages: [message]);

        session.Id.Should().Be(id);
        session.UserId.Should().Be(userId);
        session.GameId.Should().Be(gameId);
        session.AgentType.Should().Be("auto");
        session.Title.Should().Be(" titolo con spazi ", "the constructor would have trimmed it: the row says what it says");
        session.CreatedAt.Should().Be(CreatedAt, "the constructor would have said now");
        session.LastMessageAt.Should().Be(CreatedAt.AddMinutes(9));
        session.IsArchived.Should().BeTrue("the constructor would have said false");
        session.Messages.Should().ContainSingle().Which.Should().BeSameAs(message);
    }

    [Fact]
    public void Reconstitute_RaisesNoEvent()
    {
        Reconstitute().DomainEvents.Should().BeEmpty("a chat that is read was created long ago");
    }

    [Fact]
    public void Reconstitute_DoesNotValidateWhatWasAlreadyStored()
    {
        // The constructor refuses an empty user id and an empty game id. A row written under an older rule has to
        // stay readable: reading is not the place to re-judge it.
        var act = () => ChatSession.Reconstitute(
            Guid.NewGuid(), Guid.Empty, Guid.Empty, null, null, null, null, null, null, "{}", CreatedAt, CreatedAt, false, []);

        act.Should().NotThrow();
    }

    [Fact]
    public void Reconstitute_ThenAMessage_RaisesOnlyTheMessageAddedEvent()
    {
        var session = Reconstitute();

        session.AddUserMessage("ciao");

        session.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ChatSessionMessageAddedEvent>();
    }

    [Fact]
    public void Reconstitute_OfAnArchivedChat_StillRefusesNewMessages()
    {
        var session = Reconstitute(isArchived: true);

        var act = () => session.AddUserMessage("ciao");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TheConstructor_StillRaisesTheCreatedEvent()
    {
        var session = new ChatSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "titolo");

        session.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ChatSessionCreatedEvent>();
    }
}
