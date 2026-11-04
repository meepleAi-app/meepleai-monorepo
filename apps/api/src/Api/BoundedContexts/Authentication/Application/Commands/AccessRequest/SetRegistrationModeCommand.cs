using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using MediatRUnit = MediatR.Unit;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record SetRegistrationModeCommand(bool Enabled, Guid AdminId) : ICommand<MediatRUnit>;

internal class SetRegistrationModeCommandHandler : ICommandHandler<SetRegistrationModeCommand, MediatRUnit>
{
    private const string RegistrationKey = "Registration:PublicEnabled";

    private readonly IMediator _mediator;
    private readonly IConfigurationService _configService;
    private readonly IWebHostEnvironment _environment;

    public SetRegistrationModeCommandHandler(
        IMediator mediator,
        IConfigurationService configService,
        IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _configService = configService;
        _environment = environment;
    }

    public async Task<MediatRUnit> Handle(SetRegistrationModeCommand request, CancellationToken cancellationToken)
    {
        var value = request.Enabled.ToString().ToLowerInvariant();
        var environmentName = _environment.EnvironmentName;
        var existing = await _configService.GetConfigurationByKeyAsync(
            RegistrationKey, environmentName, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            await _mediator.Send(
                new UpdateConfigValueCommand(
                    Guid.Parse(existing.Id),
                    value,
                    request.AdminId),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _mediator.Send(
                new CreateConfigurationCommand(
                    RegistrationKey,
                    value,
                    "Boolean",
                    "Controls whether public registration is enabled",
                    "Authentication",
                    environmentName,
                    false,
                    request.AdminId),
                cancellationToken).ConfigureAwait(false);
        }

        // The configuration events invalidate the cached lookup too, but staging and production run them from the
        // outbox (a few seconds of polling). In that window a second click read the cached "not found" and tried to
        // create the row again (23505). The write is committed by now: drop the stale key here, without waiting.
        await _configService.InvalidateAsync(RegistrationKey, environmentName, cancellationToken).ConfigureAwait(false);

        return MediatRUnit.Value;
    }
}
