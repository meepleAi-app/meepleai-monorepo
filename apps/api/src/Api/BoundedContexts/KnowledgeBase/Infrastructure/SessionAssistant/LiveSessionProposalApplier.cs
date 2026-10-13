using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// <see cref="IAssistantProposalApplier"/> sopra i comandi esistenti della sessione live (spec §5.3,
/// passo 4): <see cref="ConfirmScoreProposalCommand"/> verifica di nuovo l'host, scrive con
/// <c>RecordScore</c>, salva e trasmette ai partecipanti; <see cref="AdvanceLiveSessionTurnCommand"/>
/// passa il turno.
/// <para>
/// Ogni comando gira in uno scope suo: salva sul proprio <c>DbContext</c>, e se quel salvataggio fallisse
/// nello scope della richiesta le sue entità resterebbero tracciate, e il salvataggio successivo della
/// proposta le riproverebbe.
/// </para>
/// </summary>
internal sealed class LiveSessionProposalApplier(IServiceScopeFactory scopes) : IAssistantProposalApplier
{
    public Task ApplyScoreAsync(
        Guid liveSessionId, Guid confirmingUserId, Guid playerId, int round, string dimension, int value,
        CancellationToken cancellationToken) =>
        SendAsync(new ConfirmScoreProposalCommand(liveSessionId, confirmingUserId, playerId, round, dimension, value), cancellationToken);

    public Task AdvanceTurnAsync(Guid liveSessionId, CancellationToken cancellationToken) =>
        SendAsync(new AdvanceLiveSessionTurnCommand(liveSessionId), cancellationToken);

    private async Task SendAsync(IRequest command, CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using var disposal = scope.ConfigureAwait(false);
        try
        {
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, cancellationToken).ConfigureAwait(false);
        }
        // I rami qui sotto sono i rifiuti che i due comandi lanciano PRIMA di salvare: nulla è stato scritto.
        // Ogni altra eccezione risale così com'è, perché può essere arrivata dopo il salvataggio.
        catch (ConflictException ex)
        {
            // Il dominio rifiuta per lo stato della sessione: conclusa, non in corso.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.SessionStateInvalid, ex.Message, ex);
        }
        catch (NotFoundException ex)
        {
            // La sessione non c'è più.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.SessionStateInvalid, ex.Message, ex);
        }
        catch (ForbiddenException ex)
        {
            // Chi conferma non è più l'host della sessione.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.NotApplicable, ex.Message, ex);
        }
        catch (FluentValidation.ValidationException ex)
        {
            // Oggi i validator dei comandi senza risposta non girano; se un giorno girassero, il valore
            // rifiutato (per esempio una cella negativa) è una proposta non applicabile.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.NotApplicable, ex.Message, ex);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new AssistantProposalApplyException(
                AssistantProposalApplyException.SessionStateInvalid,
                "The live session was changed by someone else while the proposal was being applied.",
                ex);
        }
        catch (DomainException ex)
        {
            // Il giocatore non è più attivo, la dimensione non è più abilitata.
            throw new AssistantProposalApplyException(AssistantProposalApplyException.NotApplicable, ex.Message, ex);
        }
    }
}
