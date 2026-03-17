using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for GetSlackConnectionStatusQuery.
/// Returns the Slack connection status DTO or null if no connection exists.
/// </summary>
internal class GetSlackConnectionStatusQueryHandler
    : IQueryHandler<GetSlackConnectionStatusQuery, SlackConnectionStatusDto?>
{
    private readonly ISlackConnectionRepository _connectionRepository;

    public GetSlackConnectionStatusQueryHandler(ISlackConnectionRepository connectionRepository)
    {
        ArgumentNullException.ThrowIfNull(connectionRepository);
        _connectionRepository = connectionRepository;
    }

    public async Task<SlackConnectionStatusDto?> Handle(
        GetSlackConnectionStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var connection = await _connectionRepository.GetByUserIdAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (connection == null)
            return null;

        return new SlackConnectionStatusDto(
            connection.IsActive,
            connection.SlackTeamName,
            connection.SlackUserId,
            connection.ConnectedAt);
    }
}
