using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// #4455: alla chiusura della partita le proposte ancora in attesa scadono, con lo stesso UPDATE della scadenza
/// di inizio turno. Riaprendo la pagina, le card sono già «scadute».
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4455")]
public sealed class ExpireAssistantProposalsOnSessionCompletedHandlerTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly DateTime ClosedAt = new(2026, 10, 16, 21, 30, 0, DateTimeKind.Utc);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheClose_ExpiresThePendingProposalsOfThatSession_AtTheTimeOfTheClose()
    {
        var proposals = new Mock<IAssistantProposalRepository>();

        await new ExpireAssistantProposalsOnSessionCompletedHandler(proposals.Object).Handle(Completed(), Ct);

        proposals.Verify(p => p.ExpirePendingBySessionAsync(Session, ClosedAt, It.IsAny<CancellationToken>()), Times.Once);
        proposals.VerifyNoOtherCalls();
    }

    private static LiveSessionCompletedEvent Completed() => new(
        Session,
        ClosedAt,
        totalTurns: 12,
        gameId: null,
        gameName: "Azul",
        createdByUserId: Guid.NewGuid(),
        visibility: PlayRecordVisibility.Private,
        groupId: null,
        sessionDate: ClosedAt.AddHours(-2),
        startedAt: ClosedAt.AddHours(-2),
        notes: null,
        players: [],
        scores: []);
}
