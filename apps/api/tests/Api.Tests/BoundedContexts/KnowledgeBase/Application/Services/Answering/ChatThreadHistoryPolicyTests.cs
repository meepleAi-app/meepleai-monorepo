using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// Issue #4242: whose thread history may enter the prompt of a question.
/// The answer paths loaded the thread named by the request and checked only its game. A caller
/// who sent someone else's thread id, on the same game, got an answer built on that person's
/// conversation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ChatThreadHistoryPolicyTests
{
    private static readonly Guid Asker = Guid.NewGuid();
    private static readonly Guid Game = Guid.NewGuid();

    private static ChatThread ThreadOf(Guid owner, Guid? game) => new(Guid.NewGuid(), owner, game);

    [Fact]
    public void TheAskersOwnThread_OnTheSameGame_MayBeUsed()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOf(Asker, Game), Asker, Game).Should().BeNull();
    }

    [Fact]
    public void TheAskersOwnThread_WithNoGame_MayBeUsed()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOf(Asker, game: null), Asker, Game).Should().BeNull();
    }

    [Fact]
    public void SomeoneElsesThread_OnTheSameGame_IsRefused()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOf(Guid.NewGuid(), Game), Asker, Game)
            .Should().Be(ChatThreadHistoryRefusal.NotTheOwner);
    }

    /// <summary>
    /// No identity, no history. A caller that cannot say who is asking does not get to read a
    /// conversation by naming its id.
    /// </summary>
    [Fact]
    public void AQuestionWithNoIdentity_IsRefused()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOf(Asker, Game), askingUserId: null, Game)
            .Should().Be(ChatThreadHistoryRefusal.NotTheOwner);
    }

    [Fact]
    public void TheAskersOwnThread_OnAnotherGame_IsRefused()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOf(Asker, Guid.NewGuid()), Asker, Game)
            .Should().Be(ChatThreadHistoryRefusal.AnotherGame);
    }

    /// <summary>
    /// The game of the question may not be a usable id (the streaming path receives it as text).
    /// A thread that has a game then cannot match it.
    /// </summary>
    [Fact]
    public void TheAskersOwnThread_WhenTheQuestionHasNoUsableGameId_IsRefused()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOf(Asker, Game), Asker, questionGameId: null)
            .Should().Be(ChatThreadHistoryRefusal.AnotherGame);
    }

    /// <summary>
    /// Ownership is decided first: a stranger's thread is refused as a stranger's thread, whatever
    /// its game, so the reason never tells a stranger which game a thread is about.
    /// </summary>
    [Fact]
    public void SomeoneElsesThread_OnAnotherGame_IsRefusedForOwnership()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOf(Guid.NewGuid(), Guid.NewGuid()), Asker, Game)
            .Should().Be(ChatThreadHistoryRefusal.NotTheOwner);
    }
}
