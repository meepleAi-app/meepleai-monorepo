using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.Middleware.Exceptions;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Handles deleting a saved resource forecast scenario.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed class DeleteResourceForecastCommandHandler
    : ICommandHandler<DeleteResourceForecastCommand>
{
    private readonly IResourceForecastRepository _repository;
    private readonly ILogger<DeleteResourceForecastCommandHandler> _logger;

    public DeleteResourceForecastCommandHandler(
        IResourceForecastRepository repository,
        ILogger<DeleteResourceForecastCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        DeleteResourceForecastCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var forecast = await _repository
            .GetByIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);

        if (forecast is null)
            throw new NotFoundException("ResourceForecast", command.Id.ToString());

        await _repository.DeleteAsync(forecast, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deleted resource forecast {ForecastId}: '{Name}'",
            forecast.Id, forecast.Name);
    }
}
