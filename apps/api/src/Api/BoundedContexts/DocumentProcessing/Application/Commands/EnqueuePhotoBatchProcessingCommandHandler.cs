using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Enqueues a photo batch for background OCR processing via <see cref="IBackgroundTaskService"/>.
/// Resolves <see cref="IPhotoBatchProcessor"/> from a fresh DI scope so that scoped services
/// (e.g. DbContext) are not captured by the long-running background thread.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.5.
/// </summary>
internal sealed class EnqueuePhotoBatchProcessingCommandHandler
    : ICommandHandler<EnqueuePhotoBatchProcessingCommand, Unit>
{
    private readonly IBackgroundTaskService _backgroundTasks;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnqueuePhotoBatchProcessingCommandHandler> _logger;

    public EnqueuePhotoBatchProcessingCommandHandler(
        IBackgroundTaskService backgroundTasks,
        IServiceScopeFactory scopeFactory,
        ILogger<EnqueuePhotoBatchProcessingCommandHandler> logger)
    {
        _backgroundTasks = backgroundTasks ?? throw new ArgumentNullException(nameof(backgroundTasks));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Unit> Handle(EnqueuePhotoBatchProcessingCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var taskId = $"photo-batch-{cmd.BatchId}";

        _backgroundTasks.ExecuteWithCancellation(taskId, async (scopedCt) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPhotoBatchProcessor>();
            try
            {
                await processor.ProcessAsync(cmd.BatchId, scopedCt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Photo batch {BatchId} processing failed during background execution",
                    cmd.BatchId);
            }
        });

        _logger.LogInformation(
            "Photo batch {BatchId} enqueued for background processing (taskId: {TaskId})",
            cmd.BatchId, taskId);

        return Task.FromResult(Unit.Value);
    }
}
