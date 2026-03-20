using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetVapidPublicKeyQuery.
/// Returns the VAPID public key from configuration for Web Push subscriptions.
/// Issue #4416.
/// </summary>
internal class GetVapidPublicKeyQueryHandler : IQueryHandler<GetVapidPublicKeyQuery, string>
{
    private readonly IConfiguration _configuration;

    public GetVapidPublicKeyQueryHandler(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    public Task<string> Handle(GetVapidPublicKeyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var publicKey = _configuration["Push:VapidPublicKey"] ?? "";
        return Task.FromResult(publicKey);
    }
}
