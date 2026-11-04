using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ValidationException = Api.SharedKernel.Domain.Exceptions.ValidationException;

#pragma warning disable MA0048 // File name must match type name - command, validator and handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>
/// Spec 2026-10-11 §5.3: l'host conferma una proposta dell'assistente, con l'eventuale incremento corretto.
/// È l'unico ponte verso le scritture della partita.
/// </summary>
internal sealed record ConfirmAssistantProposalCommand(Guid LiveSessionId, Guid ProposalId, Guid UserId, int? CorrectedPoints)
    : ICommand<AssistantProposalDto>;

internal sealed class ConfirmAssistantProposalCommandValidator : AbstractValidator<ConfirmAssistantProposalCommand>
{
    public ConfirmAssistantProposalCommandValidator()
    {
        RuleFor(c => c.LiveSessionId).NotEmpty();
        RuleFor(c => c.ProposalId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CorrectedPoints)
            .InclusiveBetween(-AssistantProposal.MaxPointsPerProposal, AssistantProposal.MaxPointsPerProposal)
            .When(c => c.CorrectedPoints.HasValue);
    }
}

internal sealed class ConfirmAssistantProposalCommandHandler
    : ICommandHandler<ConfirmAssistantProposalCommand, AssistantProposalDto>
{
    public const string StaleCode = "proposal_stale";

    /// <summary>Il <c>failure_code</c> di una proposta che, dopo la prenotazione, non ha potuto leggere la partita.</summary>
    public const string StateUnavailableCode = "session_state_unavailable";

    private const string StaleMessage = "The game has changed since the assistant made this proposal. Ask again.";

    private readonly IAssistantProposalRepository _proposals;
    private readonly IAssistantSessionDirectory _directory;
    private readonly IAssistantProposalApplier _applier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ConfirmAssistantProposalCommandHandler(
        IAssistantProposalRepository proposals,
        IAssistantSessionDirectory directory,
        IAssistantProposalApplier applier,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _proposals = proposals ?? throw new ArgumentNullException(nameof(proposals));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _applier = applier ?? throw new ArgumentNullException(nameof(applier));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AssistantProposalDto> Handle(ConfirmAssistantProposalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Proprietario: il filtro degli endpoint live controlla solo la partecipazione.
        var access = await _directory.GetAccessAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        if (access.OwnerUserId != command.UserId)
        {
            throw new ForbiddenException("Only the session host can confirm an assistant proposal.");
        }

        var proposal = await _proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null || proposal.LiveSessionId != command.LiveSessionId)
        {
            throw new NotFoundException("AssistantProposal", command.ProposalId.ToString());
        }

        if (command.CorrectedPoints.HasValue && proposal.Kind != AssistantProposalKind.Score)
        {
            throw new ValidationException(nameof(command.CorrectedPoints), "Only a score proposal can be corrected.");
        }

        if (proposal.Status != AssistantProposalStatus.Pending)
        {
            return CurrentStateOf(proposal);
        }

        // #4455: a partita chiusa nessuna card agisce. Prima della prenotazione: il comando della sessione la
        // rifiuterebbe, e la proposta finirebbe Failed, contata come un'applicazione fallita.
        if (!access.AcceptsQuestions)
        {
            var current = await ClosedSessionProposals
                .ExpireAsync(proposal, _proposals, _unitOfWork, Now(), cancellationToken).ConfigureAwait(false);
            return current is null ? throw ClosedSessionProposals.Expired() : CurrentStateOf(current);
        }

        // Il valore da scrivere si calcola PRIMA della prenotazione: una correzione che l'aggregato rifiuta
        // lancia qui, con la proposta ancora Pending. Calcolato dopo, la lascerebbe Applying senza nessuno
        // che la chiuda. Non dipende dallo stato della partita: è la cella attesa più l'incremento.
        int? valueToWrite = proposal.Kind == AssistantProposalKind.Score
            ? proposal.ScoreValueToWrite(command.CorrectedPoints)
            : null;

        // 2. Prenotazione: da Pending ad Applying, salvata con xmin. È l'idempotenza della conferma.
        proposal.BeginApplying(command.UserId);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Un altro tap, o la scadenza di inizio turno, è arrivato prima: nessuna scrittura, e lo stato
            // attuale. L'entità tracciata qui è ormai vecchia: si rilegge senza tracciarla.
            var current = await _proposals.GetReadOnlyAsync(command.ProposalId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException("AssistantProposal", command.ProposalId.ToString());
            return CurrentStateOf(current);
        }

        // Da qui la conferma non segue più l'annullamento della richiesta: una proposta prenotata va
        // portata a un esito anche se l'host chiude la pagina.
        return await CompleteAsync(proposal, command, valueToWrite).ConfigureAwait(false);
    }

    private async Task<AssistantProposalDto> CompleteAsync(
        AssistantProposal proposal, ConfirmAssistantProposalCommand command, int? valueToWrite)
    {
        // 3. Stato atteso: se la cella o l'indice di turno sono cambiati, la proposta è superata. È un
        // primo controllo, che evita una scrittura inutile: quello che decide lo fa il comando della
        // sessione al passo 4, sull'aggregato che sta per salvare.
        AssistantSessionState state;
        try
        {
            state = await _directory.CreateReader(command.LiveSessionId, command.UserId)
                .GetStateAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nulla è stato scritto: la proposta non deve restare Applying, con una card che non si può più
            // né confermare né rifiutare. L'errore originale risale.
            proposal.MarkFailed(StateUnavailableCode, Now());
            await SaveOutcomeAsync(proposal).ConfigureAwait(false);
            throw;
        }

        if (!MatchesWhatTheProposalExpected(proposal, state))
        {
            proposal.MarkStale(Now());
            await SaveOutcomeAsync(proposal).ConfigureAwait(false);
            throw new ConflictException(StaleCode, StaleMessage);
        }

        // 4. Applicazione, attraverso i comandi della sessione live.
        try
        {
            if (valueToWrite is { } value)
            {
                await _applier.ApplyScoreAsync(
                    command.LiveSessionId,
                    command.UserId,
                    new AssistantScoreWrite(
                        proposal.Payload.PlayerId ?? Guid.Empty,
                        proposal.Payload.Round ?? 1,
                        proposal.Payload.Dimension ?? string.Empty,
                        value,
                        ExpectedCurrentValue: proposal.Expected.CellValue ?? 0),
                    CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                await _applier.AdvanceTurnAsync(
                    command.LiveSessionId, proposal.Expected.TurnIndex ?? state.CurrentTurnIndex, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (AssistantProposalApplyException ex)
            when (string.Equals(ex.FailureCode, AssistantProposalApplyException.GameChanged, StringComparison.Ordinal))
        {
            // La partita è cambiata fra il controllo qui sopra e la scrittura: lo dice il comando della
            // sessione, che ricontrolla sull'aggregato che sta per salvare. Nulla è stato scritto.
            proposal.MarkStale(Now());
            await SaveOutcomeAsync(proposal).ConfigureAwait(false);
            throw new ConflictException(StaleCode, StaleMessage);
        }
        catch (AssistantProposalApplyException ex)
        {
            proposal.MarkFailed(ex.FailureCode, Now());
            await SaveOutcomeAsync(proposal).ConfigureAwait(false);
            throw new ConflictException(ex.FailureCode, ex.Message);
        }

        // 5. Esito.
        if (command.CorrectedPoints is { } corrected && corrected != proposal.Payload.Points)
        {
            proposal.MarkEdited(corrected, Now());
        }
        else
        {
            proposal.MarkAccepted(Now());
        }

        await SaveOutcomeAsync(proposal).ConfigureAwait(false);
        return AssistantProposalDto.From(proposal);
    }

    /// <summary>
    /// Salva l'esito, e solo dopo lo conta (#4453): un esito che non è nel database non entra nel tasso di
    /// accettazione. Non segue l'annullamento della richiesta, come tutto ciò che viene dopo la prenotazione.
    /// </summary>
    private async Task SaveOutcomeAsync(AssistantProposal proposal)
    {
        await _unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        SessionAssistantMetrics.ProposalResolved(proposal);
    }

    /// <summary>
    /// Un secondo tap su una proposta che si sta applicando, o già applicata, riceve lo stato attuale:
    /// l'host non deve vedere un errore per aver toccato due volte. Chiusa in altro modo, è un 409.
    /// </summary>
    private static AssistantProposalDto CurrentStateOf(AssistantProposal proposal) =>
        proposal.Status is AssistantProposalStatus.Applying or AssistantProposalStatus.Accepted or AssistantProposalStatus.Edited
            ? AssistantProposalDto.From(proposal)
            : throw new ConflictException(
                AssistantProposal.NotPendingCode, $"The proposal is {proposal.Status}: it can no longer be confirmed.");

    private static bool MatchesWhatTheProposalExpected(AssistantProposal proposal, AssistantSessionState state)
    {
        if (proposal.Kind == AssistantProposalKind.AdvanceTurn)
        {
            return state.CurrentTurnIndex == proposal.Expected.TurnIndex;
        }

        // Una cella che non esiste vale zero, come quando la proposta è nata.
        var cell = state.Scores
            .Where(s => s.PlayerId == proposal.Payload.PlayerId
                && s.Round == proposal.Payload.Round
                && string.Equals(s.Dimension, proposal.Payload.Dimension, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .FirstOrDefault();
        return cell == (proposal.Expected.CellValue ?? 0);
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
