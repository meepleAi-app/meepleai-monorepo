using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Handler for deleting a saved cost scenario.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed class DeleteCostScenarioCommandHandler
    : ICommandHandler<DeleteCostScenarioCommand>
{
    private readonly ICostScenarioRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteCostScenarioCommandHandler> _logger;

    public DeleteCostScenarioCommandHandler(
        ICostScenarioRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteCostScenarioCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        DeleteCostScenarioCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var scenario = await _repository.GetByIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);

        if (scenario is null)
            throw new NotFoundException("CostScenario", command.Id.ToString());

        await _repository.DeleteAsync(scenario, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deleted cost scenario {ScenarioId}: '{Name}'",
            scenario.Id, scenario.Name);
    }
}
