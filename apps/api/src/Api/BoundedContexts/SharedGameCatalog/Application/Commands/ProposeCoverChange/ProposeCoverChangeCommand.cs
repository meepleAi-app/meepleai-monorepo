using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ProposeCoverChange;

/// <summary>
/// Command for the authenticated-user flow that proposes a cover-from-PDF for a
/// SharedGame (Task 6, Game Cover-da-PDF plan). Orchestrates
/// <see cref="MaterializePdfCoverCommand"/> (Task 3 — renders the PDF page, encodes
/// WebP, uploads to R2) to materialize a pending cover artifact, then creates a
/// <see cref="ShareRequest"/> of type <c>CoverChange</c> (Task 4) so an admin can
/// review and promote it to the SharedGame's public L4 cover (Task 5).
/// </summary>
/// <param name="UserId">The authenticated user proposing the cover.</param>
/// <param name="SharedGameId">The target SharedGame the cover is proposed for.</param>
/// <param name="PdfDocumentId">The source PDF the cover page is extracted from.</param>
/// <param name="PageNumber">1-based page number to render as the cover.</param>
internal sealed record ProposeCoverChangeCommand(
    Guid UserId,
    Guid SharedGameId,
    Guid PdfDocumentId,
    int PageNumber
) : ICommand<Guid>;

/// <summary>
/// Validator for <see cref="ProposeCoverChangeCommand"/>.
/// </summary>
internal sealed class ProposeCoverChangeCommandValidator : AbstractValidator<ProposeCoverChangeCommand>
{
    public ProposeCoverChangeCommandValidator()
    {
        RuleFor(x => x.SharedGameId).NotEmpty();
        RuleFor(x => x.PdfDocumentId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0);
    }
}

/// <summary>
/// Handles <see cref="ProposeCoverChangeCommand"/>: materializes the pending cover
/// (Task 3) then persists a Pending <see cref="ShareRequest"/> of type
/// <c>CoverChange</c> (Task 4) referencing it. Materialization failures
/// (<c>CoverMaterializationException</c>) propagate unhandled — no proposal is
/// created without a successfully materialized pending cover (no "half" state).
/// </summary>
internal sealed class ProposeCoverChangeCommandHandler : ICommandHandler<ProposeCoverChangeCommand, Guid>
{
    private readonly IMediator _mediator;
    private readonly IShareRequestRepository _shareRequests;
    private readonly IUnitOfWork _unitOfWork;

    public ProposeCoverChangeCommandHandler(
        IMediator mediator,
        IShareRequestRepository shareRequests,
        IUnitOfWork unitOfWork)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _shareRequests = shareRequests ?? throw new ArgumentNullException(nameof(shareRequests));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Guid> Handle(ProposeCoverChangeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Deterministic dbKey for the pending cover of this proposal.
        var dbKey = $"covers/{command.SharedGameId:D}/pdf-cover-pending";

        var pendingKey = await _mediator
            .Send(new MaterializePdfCoverCommand(command.PdfDocumentId, command.PageNumber, dbKey), cancellationToken)
            .ConfigureAwait(false);

        var request = ShareRequest.CreateCoverChange(
            command.UserId,
            command.SharedGameId,
            command.PdfDocumentId,
            pendingKey,
            command.PageNumber);

        await _shareRequests.AddAsync(request, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return request.Id;
    }
}
