using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Handles saving a resource forecast scenario to the database.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed class SaveResourceForecastCommandHandler
    : ICommandHandler<SaveResourceForecastCommand, Guid>
{
    private readonly IResourceForecastRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SaveResourceForecastCommandHandler> _logger;

    public SaveResourceForecastCommandHandler(
        IResourceForecastRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SaveResourceForecastCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        SaveResourceForecastCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var forecast = ResourceForecast.Create(
            name: command.Name,
            growthPattern: command.GrowthPattern,
            monthlyGrowthRate: command.MonthlyGrowthRate,
            currentUsers: command.CurrentUsers,
            currentDbSizeGb: command.CurrentDbSizeGb,
            currentDailyTokens: command.CurrentDailyTokens,
            currentCacheMb: command.CurrentCacheMb,
            currentVectorEntries: command.CurrentVectorEntries,
            dbPerUserMb: command.DbPerUserMb,
            tokensPerUserPerDay: command.TokensPerUserPerDay,
            cachePerUserMb: command.CachePerUserMb,
            vectorsPerUser: command.VectorsPerUser,
            projectionsJson: command.ProjectionsJson,
            recommendationsJson: command.RecommendationsJson,
            projectedMonthlyCost: command.ProjectedMonthlyCost,
            createdByUserId: command.CreatedByUserId);

        await _repository.AddAsync(forecast, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Saved resource forecast {ForecastId}: '{Name}' ({Pattern} {Rate}%/mo) ${Cost}/mo by user {UserId}",
            forecast.Id, command.Name, command.GrowthPattern, command.MonthlyGrowthRate,
            command.ProjectedMonthlyCost, command.CreatedByUserId);

        return forecast.Id;
    }
}
