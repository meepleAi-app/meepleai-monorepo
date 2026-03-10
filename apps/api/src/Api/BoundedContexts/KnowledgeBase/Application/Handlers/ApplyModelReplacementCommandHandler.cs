using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Interfaces;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Applies a model replacement across all affected strategy mappings.
/// Issue #5499: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed class ApplyModelReplacementCommandHandler
    : ICommandHandler<ApplyModelReplacementCommand, ApplyModelReplacementResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IModelCompatibilityRepository _compatibilityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApplyModelReplacementCommandHandler> _logger;

    public ApplyModelReplacementCommandHandler(
        MeepleAiDbContext dbContext,
        IModelCompatibilityRepository compatibilityRepository,
        IUnitOfWork unitOfWork,
        ILogger<ApplyModelReplacementCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _compatibilityRepository = compatibilityRepository ?? throw new ArgumentNullException(nameof(compatibilityRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ApplyModelReplacementResult> Handle(
        ApplyModelReplacementCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var updatedStrategies = new List<string>();

        // Use a transaction to prevent concurrent replacement overwrites
        var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Find all strategy mappings that use the deprecated model as primary
            var affectedMappings = await _dbContext.Set<StrategyModelMappingEntity>()
                .Where(m => m.PrimaryModel == command.DeprecatedModelId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (affectedMappings.Count == 0)
            {
                throw new NotFoundException(
                    $"No strategy mappings found using model '{command.DeprecatedModelId}' as primary model");
            }

            foreach (var mapping in affectedMappings)
            {
                var previousModel = mapping.PrimaryModel;
                mapping.PrimaryModel = command.ReplacementModelId;
                mapping.UpdatedAt = DateTime.UtcNow;
                updatedStrategies.Add(mapping.Strategy);

                _logger.LogInformation(
                    "Replaced model for strategy {Strategy}: {OldModel} → {NewModel}",
                    mapping.Strategy, previousModel, command.ReplacementModelId);
            }

            // Log the change in model compatibility repository
            await _compatibilityRepository.LogChangeAsync(
                new ModelChangeLogEntry(
                    Guid.NewGuid(),
                    command.DeprecatedModelId,
                    "replaced",
                    command.DeprecatedModelId,
                    command.ReplacementModelId,
                    string.Join(", ", updatedStrategies),
                    $"Admin applied replacement: {command.DeprecatedModelId} → {command.ReplacementModelId}",
                    false,
                    null,
                    DateTime.UtcNow),
                cancellationToken).ConfigureAwait(false);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Applied model replacement: {OldModel} → {NewModel} across {Count} strategies [{Strategies}]",
            command.DeprecatedModelId, command.ReplacementModelId,
            updatedStrategies.Count, string.Join(", ", updatedStrategies));

        return new ApplyModelReplacementResult(
            updatedStrategies.ToArray(),
            updatedStrategies.Count);
    }
}
