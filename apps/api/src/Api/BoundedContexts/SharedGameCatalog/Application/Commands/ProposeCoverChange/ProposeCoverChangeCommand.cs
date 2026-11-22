using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ProposeCoverChange;

/// <summary>
/// Command for the authenticated-user flow that proposes a cover-from-PDF for a
/// SharedGame (Task 6, Game Cover-da-PDF plan). Renders the page with
/// <see cref="RenderPdfPageCoverCommand"/> (encodes WebP, uploads to R2) as a pending cover
/// artifact, then creates a <see cref="ShareRequest"/> of type <c>CoverChange</c> (Task 4) so an
/// admin can review and promote it to the SharedGame's public L4 cover (Task 5).
/// </summary>
/// <remarks>
/// #5100: until the approval nothing changes, neither the game nor the PDF. The proposal used
/// <c>MaterializePdfCoverCommand</c>, which makes the page the PDF's cover, and
/// <c>PdfCoverGeneratedEvent</c> then copied it onto the game during the proposal. The PDF must be
/// one of the game's documents: the same rule as <c>SetPdfCoverFromPageCommand</c>, the admin path.
/// </remarks>
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
/// Handles <see cref="ProposeCoverChangeCommand"/>: renders the pending cover then persists a Pending <see cref="ShareRequest"/> of type
/// <c>CoverChange</c> (Task 4) referencing it. Materialization failures
/// (<c>CoverMaterializationException</c>) propagate unhandled — no proposal is
/// created without a successfully materialized pending cover (no "half" state).
/// </summary>
internal sealed class ProposeCoverChangeCommandHandler : ICommandHandler<ProposeCoverChangeCommand, Guid>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly IShareRequestRepository _shareRequests;
    private readonly IUnitOfWork _unitOfWork;

    public ProposeCoverChangeCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        IShareRequestRepository shareRequests,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _shareRequests = shareRequests ?? throw new ArgumentNullException(nameof(shareRequests));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Guid> Handle(ProposeCoverChangeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == command.PdfDocumentId)
            .Select(p => new { p.SharedGameId, p.PageCount })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());

        if (pdf.SharedGameId != command.SharedGameId)
        {
            throw new ConflictException("The PDF is not one of this game's documents.");
        }

        if (pdf.PageCount is { } pageCount && command.PageNumber > pageCount)
        {
            throw new ConflictException($"The PDF has {pageCount} pages.");
        }

        // I2 fix: the dbKey must be unique PER PROPOSAL, not deterministic per-game.
        // A per-game deterministic key (e.g. "covers/{gameId}/pdf-cover-pending")
        // means two proposals for the same game (two users, or two pages) write the
        // SAME physical R2 object and persist the SAME PendingCoverR2Key. On
        // approval, the admin approving proposal A could end up promoting whatever
        // bytes a later still-pending proposal B last overwrote at that key — the
        // wrong image gets promoted. The GUID segment below eliminates the
        // collision while preserving the same slash-containing key SHAPE
        // (covers/{gameId}/...) required by the R2 resolver fix (commit
        // 05723c823's GetPresignedUrlForRawKeyAsync, which resolves slash/dot
        // physical keys via an existence check).
        var dbKey = $"covers/{command.SharedGameId:D}/pdf-cover-{Guid.NewGuid():N}";

        var pendingKey = await _mediator
            .Send(new RenderPdfPageCoverCommand(command.PdfDocumentId, command.PageNumber, dbKey), cancellationToken)
            .ConfigureAwait(false);

        // C1 fix: command.PageNumber is 1-based; ShareRequest.CoverPageIndex is documented
        // and stored 0-based (mirrors PdfDocument.CoverPageIndex).
        var request = ShareRequest.CreateCoverChange(
            command.UserId,
            command.SharedGameId,
            command.PdfDocumentId,
            pendingKey,
            command.PageNumber - 1);

        await _shareRequests.AddAsync(request, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return request.Id;
    }
}
