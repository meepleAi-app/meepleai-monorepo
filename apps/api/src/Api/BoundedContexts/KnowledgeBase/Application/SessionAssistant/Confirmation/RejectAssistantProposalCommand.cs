using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

#pragma warning disable MA0048 // File name must match type name - command, validator and handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>Spec 2026-10-11 §5.3: l'host rifiuta una proposta dell'assistente. Non scrive la partita.</summary>
internal sealed record RejectAssistantProposalCommand(Guid LiveSessionId, Guid ProposalId, Guid UserId)
    : ICommand<AssistantProposalDto>;

internal sealed class RejectAssistantProposalCommandValidator : AbstractValidator<RejectAssistantProposalCommand>
{
    public RejectAssistantProposalCommandValidator()
    {
        RuleFor(c => c.LiveSessionId).NotEmpty();
        RuleFor(c => c.ProposalId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
    }
}

internal sealed class RejectAssistantProposalCommandHandler(
    IAssistantProposalRepository proposals,
    IAssistantSessionDirectory directory,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<RejectAssistantProposalCommand, AssistantProposalDto>
{
    public async Task<AssistantProposalDto> Handle(RejectAssistantProposalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await directory.GetAccessAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        if (access.OwnerUserId != command.UserId)
        {
            throw new ForbiddenException("Only the session host can reject an assistant proposal.");
        }

        var proposal = await proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null || proposal.LiveSessionId != command.LiveSessionId)
        {
            throw new NotFoundException("AssistantProposal", command.ProposalId.ToString());
        }

        if (proposal.Status != AssistantProposalStatus.Pending)
        {
            return CurrentStateOf(proposal);
        }

        proposal.Reject(command.UserId, timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Una conferma o un altro rifiuto è arrivato prima: vale ciò che ha lasciato.
            var current = await proposals.GetReadOnlyAsync(command.ProposalId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException("AssistantProposal", command.ProposalId.ToString());
            return CurrentStateOf(current);
        }

        // #4453: contato dopo il salvataggio. Chi perde la corsa non conta: l'esito l'ha contato chi l'ha salvato.
        SessionAssistantMetrics.ProposalResolved(proposal);
        return AssistantProposalDto.From(proposal);
    }

    /// <summary>Un secondo tap su una proposta già rifiutata la restituisce; ogni altro stato chiuso, o in corso, è un 409.</summary>
    private static AssistantProposalDto CurrentStateOf(AssistantProposal proposal) =>
        proposal.Status == AssistantProposalStatus.Rejected
            ? AssistantProposalDto.From(proposal)
            : throw new ConflictException(
                AssistantProposal.NotPendingCode, $"The proposal is {proposal.Status}: it can no longer be rejected.");
}
