using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Handles play record creation.
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal class CreatePlayRecordCommandHandler : ICommandHandler<CreatePlayRecordCommand, Guid>
{
    private readonly IPlayRecordRepository _recordRepository;
    private readonly IGameCoreDataProvider _gameCoreData;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreatePlayRecordCommandHandler(
        IPlayRecordRepository recordRepository,
        IGameCoreDataProvider gameCoreData,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Guid> Handle(CreatePlayRecordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate game exists if GameId provided
        if (command.GameId.HasValue)
        {
            var game = await _gameCoreData
                .GetCoreDataAsync(GameRef.Shared(command.GameId.Value), cancellationToken)
                .ConfigureAwait(false);

            if (game is null)
                throw new NotFoundException("Game", command.GameId.Value.ToString());
        }

        // Create scoring config
        SessionScoringConfig scoringConfig;
        if (command.ScoringDimensions != null && command.ScoringDimensions.Count > 0)
        {
            scoringConfig = new SessionScoringConfig(
                command.ScoringDimensions,
                command.DimensionUnits);
        }
        else
        {
            scoringConfig = SessionScoringConfig.CreateDefault();
        }

        // Create aggregate
        PlayRecord record;
        if (command.GameId.HasValue)
        {
            record = PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                command.GameId.Value,
                command.GameName,
                command.UserId,  // From authenticated user context
                command.SessionDate,
                command.Visibility,
                _timeProvider,
                command.GroupId,
                scoringConfig);
        }
        else
        {
            record = PlayRecord.CreateFreeForm(
                Guid.NewGuid(),
                command.GameName,
                command.UserId,  // From authenticated user context
                command.SessionDate,
                command.Visibility,
                scoringConfig,
                _timeProvider,
                command.GroupId);
        }

        // Persist
        await _recordRepository.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return record.Id;
    }
}
