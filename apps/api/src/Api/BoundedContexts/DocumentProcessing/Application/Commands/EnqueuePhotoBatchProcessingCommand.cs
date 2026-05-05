using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command that enqueues a photo batch for background OCR processing.
/// Sent internally by <see cref="UploadPhotoBatchCommandHandler"/> immediately
/// after the batch entity is persisted.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.5.
/// </summary>
/// <param name="BatchId">ID of the <see cref="Domain.Entities.PhotoBatchUpload"/> to process.</param>
internal sealed record EnqueuePhotoBatchProcessingCommand(Guid BatchId) : ICommand<Unit>;
