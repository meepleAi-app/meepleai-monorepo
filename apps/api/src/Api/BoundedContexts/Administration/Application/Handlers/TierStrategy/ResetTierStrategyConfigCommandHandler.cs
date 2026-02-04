using Api.BoundedContexts.Administration.Application.Commands.TierStrategy;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers.TierStrategy;

/// <summary>
/// Handler for ResetTierStrategyConfigCommand.
/// Resets tier-strategy configuration to defaults by deleting database entries.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal class ResetTierStrategyConfigCommandHandler
    : ICommandHandler<ResetTierStrategyConfigCommand, TierStrategyResetResultDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResetTierStrategyConfigCommandHandler> _logger;

    public ResetTierStrategyConfigCommandHandler(
        MeepleAiDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILogger<ResetTierStrategyConfigCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TierStrategyResetResultDto> Handle(
        ResetTierStrategyConfigCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var accessDeleted = 0;
        var mappingsDeleted = 0;

        if (command.ResetAccessMatrix)
        {
            accessDeleted = await _dbContext.Set<TierStrategyAccessEntity>()
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Deleted {Count} tier-strategy access entries", accessDeleted);
        }

        if (command.ResetModelMappings)
        {
            mappingsDeleted = await _dbContext.Set<StrategyModelMappingEntity>()
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Deleted {Count} strategy-model mapping entries", mappingsDeleted);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var message = $"Reset complete. Deleted {accessDeleted} access entries and {mappingsDeleted} model mappings. Defaults will be used.";

        _logger.LogInformation(
            "Reset tier-strategy configuration: {AccessDeleted} access, {MappingsDeleted} mappings",
            accessDeleted,
            mappingsDeleted);

        return new TierStrategyResetResultDto(
            AccessEntriesDeleted: accessDeleted,
            ModelMappingsDeleted: mappingsDeleted,
            Message: message);
    }
}
