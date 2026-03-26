using System.Text.Json;
using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Handler for saving a cost estimation scenario.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed class SaveCostScenarioCommandHandler
    : ICommandHandler<SaveCostScenarioCommand, Guid>
{
    private readonly ICostScenarioRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SaveCostScenarioCommandHandler> _logger;

    public SaveCostScenarioCommandHandler(
        ICostScenarioRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SaveCostScenarioCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        SaveCostScenarioCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var warningsJson = command.Warnings is { Count: > 0 }
            ? JsonSerializer.Serialize(command.Warnings)
            : null;

        var scenario = CostScenario.Create(
            name: command.Name,
            strategy: command.Strategy,
            modelId: command.ModelId,
            messagesPerDay: command.MessagesPerDay,
            activeUsers: command.ActiveUsers,
            avgTokensPerRequest: command.AvgTokensPerRequest,
            costPerRequest: command.CostPerRequest,
            dailyProjection: command.DailyProjection,
            monthlyProjection: command.MonthlyProjection,
            warnings: warningsJson,
            createdByUserId: command.CreatedByUserId);

        await _repository.AddAsync(scenario, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Saved cost scenario {ScenarioId}: '{Name}' ({Strategy}/{Model}) ${Monthly:F2}/mo by user {UserId}",
            scenario.Id, command.Name, command.Strategy, command.ModelId,
            command.MonthlyProjection, command.CreatedByUserId);

        return scenario.Id;
    }
}
