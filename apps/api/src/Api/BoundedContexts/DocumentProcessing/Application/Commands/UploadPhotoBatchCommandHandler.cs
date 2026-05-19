using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Creates a <see cref="PhotoBatchUpload"/> aggregate, stores each photo in blob storage,
/// persists the batch entity, and enqueues it for background OCR processing.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.5.
/// </summary>
internal sealed class UploadPhotoBatchCommandHandler
    : ICommandHandler<UploadPhotoBatchCommand, UploadPhotoBatchResult>
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IBlobStorageService _blob;
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<UploadPhotoBatchCommandHandler> _logger;

    public UploadPhotoBatchCommandHandler(
        IPhotoBatchUploadRepository repo,
        IBlobStorageService blob,
        IUnitOfWork uow,
        IMediator mediator,
        ILogger<UploadPhotoBatchCommandHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _blob = blob ?? throw new ArgumentNullException(nameof(blob));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UploadPhotoBatchResult> Handle(UploadPhotoBatchCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // 1. Create and persist the batch aggregate first so we have a stable BatchId
        var batch = PhotoBatchUpload.Create(cmd.UserId, cmd.GameId, cmd.SourceLanguage, cmd.Photos.Length);
        await _repo.AddAsync(batch, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Photo batch {BatchId} created for game {GameId} ({PhotoCount} photos)",
            batch.Id, cmd.GameId, cmd.Photos.Length);

        // 2. Store each photo in blob storage.
        //    IBlobStorageService.StoreAsync accepts gameId as string.
        for (var i = 0; i < cmd.Photos.Length; i++)
        {
            var photo = cmd.Photos[i];
            var photoBytes = Convert.FromBase64String(photo.Base64Content);
            var fileName = $"photo-batch-{batch.Id}-page-{i:D3}.jpg";

            using var stream = new MemoryStream(photoBytes);
            await _blob.StoreAsync(stream, fileName, BlobCategory.Pdf, cmd.GameId.ToString(), cancellationToken).ConfigureAwait(false);
        }

        // 3. Enqueue background processing (fire-and-forget via IBackgroundTaskService)
        await _mediator.Send(new EnqueuePhotoBatchProcessingCommand(batch.Id), cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Photo batch {BatchId} stored and enqueued for OCR processing",
            batch.Id);

        return new UploadPhotoBatchResult(batch.Id, cmd.Photos.Length);
    }
}
