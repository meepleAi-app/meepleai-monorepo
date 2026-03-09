using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get all active devices (sessions) for a user.
/// Issue #3340: Login device tracking and management.
/// </summary>
internal record GetUserDevicesQuery(
    Guid UserId,
    Guid? CurrentSessionId = null
) : IQuery<List<UserDeviceDto>>;

/// <summary>
/// DTO representing a user's device (parsed from session).
/// </summary>
internal record UserDeviceDto(
    Guid Id,
    string DeviceType,
    string Browser,
    string BrowserVersion,
    string OperatingSystem,
    string OsVersion,
    bool IsMobile,
    string DisplayName,
    string? IpAddress,
    DateTime CreatedAt,
    DateTime? LastSeenAt,
    bool IsCurrentDevice
);

/// <summary>
/// Handler for GetUserDevicesQuery.
/// </summary>
internal class GetUserDevicesQueryHandler : IQueryHandler<GetUserDevicesQuery, List<UserDeviceDto>>
{
    private readonly ISessionRepository _sessionRepository;

    public GetUserDevicesQueryHandler(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<List<UserDeviceDto>> Handle(GetUserDevicesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var devices = sessions.Select(session =>
        {
            var deviceInfo = DeviceInfo.Parse(session.UserAgent);

            return new UserDeviceDto(
                Id: session.Id,
                DeviceType: deviceInfo.DeviceType,
                Browser: deviceInfo.Browser,
                BrowserVersion: deviceInfo.BrowserVersion,
                OperatingSystem: deviceInfo.OperatingSystem,
                OsVersion: deviceInfo.OsVersion,
                IsMobile: deviceInfo.IsMobile,
                DisplayName: deviceInfo.GetDisplayName(),
                IpAddress: session.IpAddress,
                CreatedAt: session.CreatedAt,
                LastSeenAt: session.LastSeenAt,
                IsCurrentDevice: query.CurrentSessionId.HasValue && session.Id == query.CurrentSessionId.Value
            );
        })
        .OrderByDescending(d => d.IsCurrentDevice)
        .ThenByDescending(d => d.LastSeenAt ?? d.CreatedAt)
        .ToList();

        return devices;
    }
}
