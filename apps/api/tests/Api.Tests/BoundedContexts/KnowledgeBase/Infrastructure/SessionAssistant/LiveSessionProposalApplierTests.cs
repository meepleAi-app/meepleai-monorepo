using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// Spec 2026-10-11 §5.3, passo 4: la conferma applica attraverso i comandi esistenti della sessione
/// live, e traduce i loro rifiuti in un codice che la proposta può conservare.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4277")]
public sealed class LiveSessionProposalApplierTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<IMediator> _mediator = new();

    private LiveSessionProposalApplier Applier()
    {
        var services = new ServiceCollection().AddScoped(_ => _mediator.Object).BuildServiceProvider();
        return new LiveSessionProposalApplier(services.GetRequiredService<IServiceScopeFactory>());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AScore_GoesThroughTheHostConfirmationCommand()
    {
        await Applier().ApplyScoreAsync(Session, Owner, new AssistantScoreWrite(Marco, 2, "punti", 12, ExpectedCurrentValue: 7), Ct);

        _mediator.Verify(m => m.Send(
            It.Is<IRequest>(r => r.Equals(new ConfirmScoreProposalCommand(Session, Owner, Marco, 2, "punti", 12, 7))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ATurn_GoesThroughTheAdvanceTurnCommand()
    {
        await Applier().AdvanceTurnAsync(Session, 4, Ct);

        _mediator.Verify(m => m.Send(
            It.Is<IRequest>(r => r.Equals(new AdvanceLiveSessionTurnCommand(Session, 4))), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// I rifiuti che i due comandi della sessione lanciano PRIMA di salvare: nulla è stato scritto, quindi
    /// la proposta può chiudersi come fallita.
    /// </summary>
    public static TheoryData<Exception, string> Refusals() => new()
    {
        { new ConflictException("Cannot record scores in Completed status."), "session_state_invalid" },
        // #4277: la cella o il turno non sono più quelli attesi. Non è un rifiuto per stato: la proposta è superata.
        { new ConflictException(ConfirmScoreProposalCommand.ScoreChangedCode, "The score cell changed"), "proposal_stale" },
        { new ConflictException(AdvanceLiveSessionTurnCommand.TurnChangedCode, "The turn already moved"), "proposal_stale" },
        { new DbUpdateConcurrencyException("xmin"), "session_state_invalid" },
        { new NotFoundException("LiveGameSession", "a1"), "session_state_invalid" },
        { new DomainException("Active player not found in this session"), "proposal_not_applicable" },
        { new ForbiddenException("Only the session host can confirm score proposals"), "proposal_not_applicable" },
        { new FluentValidation.ValidationException("Value must be greater than or equal to 0"), "proposal_not_applicable" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task ARefusalOfTheSession_BecomesAFailureCode(Exception refusal, string expectedCode)
    {
        _mediator.Setup(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(refusal);

        var act = () => Applier().AdvanceTurnAsync(Session, 4, Ct);

        var thrown = (await act.Should().ThrowAsync<AssistantProposalApplyException>()).Which;
        thrown.FailureCode.Should().Be(expectedCode);
        thrown.InnerException.Should().BeSameAs(refusal);
    }

    /// <summary>
    /// Per esempio la trasmissione ai partecipanti che fallisce DOPO il salvataggio: non si sa se la partita
    /// è stata scritta, quindi non è un rifiuto. L'errore risale e la proposta resta Applying (spec §5.3).
    /// </summary>
    [Fact]
    public async Task AnythingElse_IsNotARefusal_AndPassesThrough()
    {
        _mediator.Setup(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the hub is down"));

        var act = () => Applier().AdvanceTurnAsync(Session, 4, Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
