using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;

public record RegistrationModeDto(bool PublicRegistrationEnabled, bool OauthEnabled);

internal record GetRegistrationModeQuery : IQuery<RegistrationModeDto>;

internal class GetRegistrationModeQueryHandler : IQueryHandler<GetRegistrationModeQuery, RegistrationModeDto>
{
    private readonly IConfigurationService _configService;

    public GetRegistrationModeQueryHandler(IConfigurationService configService)
    {
        _configService = configService;
    }

    public async Task<RegistrationModeDto> Handle(GetRegistrationModeQuery request, CancellationToken cancellationToken)
    {
        // Fail closed: default false if config not found or service unreachable
        bool enabled;
        try
        {
            enabled = await _configService.GetValueAsync<bool?>(
                "Registration:PublicEnabled", false).ConfigureAwait(false) ?? false;
        }
        catch
        {
            enabled = false; // Fail closed
        }

        // OAuth login gate (fail closed for alpha)
        bool oauthEnabled;
        try
        {
            oauthEnabled = await _configService.GetValueAsync<bool?>(
                "oauth_login", false).ConfigureAwait(false) ?? false;
        }
        catch
        {
            oauthEnabled = false; // Fail closed
        }

        return new RegistrationModeDto(enabled, oauthEnabled);
    }
}
