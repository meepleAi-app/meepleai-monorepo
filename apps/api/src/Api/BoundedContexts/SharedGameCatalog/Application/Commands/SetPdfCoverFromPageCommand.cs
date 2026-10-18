using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// #4413: an admin or editor picks a page of the game's rulebook as its cover. The page is rendered
/// on the server and becomes the game's PDF cover, the first source the cover resolver reads.
/// </summary>
/// <remarks>
/// <para>
/// The PDF import wizard let the admin choose a page, then lost the choice: the preview was a
/// <c>blob:</c> URL of the browser. This is the server path for it. Unlike
/// <c>ProposeCoverChangeCommand</c>, the user path, it applies the cover at once: there is no one
/// to approve an admin's choice.
/// </para>
/// <para>
/// Only on a PDF that is <c>Ready</c>. Rendering the cover writes the PDF row, and while the upload
/// is still processing it, the processing holds that row and saves it again: the concurrency
/// conflict would end the processing <c>Failed</c>. <c>BackfillPdfCoversJob</c> waits for
/// <c>Ready</c> for the same reason. The wizard applies the cover at step 5, once the indexing is
/// done.
/// </para>
/// </remarks>
/// <returns>The stored key of the cover.</returns>
internal sealed record SetPdfCoverFromPageCommand(
    Guid GameId,
    Guid PdfDocumentId,
    int PageNumber,
    Guid RequestedBy) : ICommand<string>;

internal sealed class SetPdfCoverFromPageCommandValidator : AbstractValidator<SetPdfCoverFromPageCommand>
{
    public SetPdfCoverFromPageCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PdfDocumentId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("Page numbers start at 1");
        RuleFor(x => x.RequestedBy).NotEmpty();
    }
}

internal sealed class SetPdfCoverFromPageCommandHandler : ICommandHandler<SetPdfCoverFromPageCommand, string>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<SetPdfCoverFromPageCommandHandler> _logger;

    public SetPdfCoverFromPageCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        ILogger<SetPdfCoverFromPageCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> Handle(SetPdfCoverFromPageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == command.PdfDocumentId)
            .Select(p => new { p.SharedGameId, p.ProcessingState, p.PageCount })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PdfDocument", command.PdfDocumentId.ToString());

        if (pdf.SharedGameId != command.GameId)
        {
            // The cover event sets the cover of the PDF's own game: a PDF of another game would
            // change that game's cover, not this one.
            throw new ConflictException("The PDF is not one of this game's documents.");
        }

        if (!string.Equals(pdf.ProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal))
        {
            throw new ConflictException(
                "The PDF is still being processed (or its processing failed): its pages can become the cover once it is ready.");
        }

        if (pdf.PageCount is { } pageCount && command.PageNumber > pageCount)
        {
            throw new ConflictException($"The PDF has {pageCount} pages.");
        }

        // A key per choice, like ProposeCoverChangeCommand: a deterministic one per game would let a
        // later choice overwrite the image an earlier one still points to.
        var dbKey = $"covers/{command.GameId:D}/pdf-cover-{Guid.NewGuid():N}";
        var storedKey = await _mediator
            .Send(new MaterializePdfCoverCommand(command.PdfDocumentId, command.PageNumber, dbKey), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "PDF cover set from page {Page} of PDF {PdfId} for game {GameId} by {UserId}",
            command.PageNumber, command.PdfDocumentId, command.GameId, command.RequestedBy);
        return storedKey;
    }
}
