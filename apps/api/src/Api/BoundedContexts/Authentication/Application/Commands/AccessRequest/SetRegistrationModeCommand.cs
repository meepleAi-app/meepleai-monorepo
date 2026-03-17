using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record SetRegistrationModeCommand(bool Enabled, Guid AdminId) : ICommand<MediatRUnit>;

internal class SetRegistrationModeCommandHandler : ICommandHandler<SetRegistrationModeCommand, MediatRUnit>
{
    private readonly IMediator _mediator;
    private readonly IConfigurationService _configService;

    public SetRegistrationModeCommandHandler(IMediator mediator, IConfigurationService configService)
    {
        _mediator = mediator;
        _configService = configService;
    }

    public async Task<MediatRUnit> Handle(SetRegistrationModeCommand request, CancellationToken cancellationToken)
    {
        var value = request.Enabled.ToString().ToLowerInvariant();
        var existing = await _configService.GetConfigurationByKeyAsync(
            "Registration:PublicEnabled", null, cancellationToken).ConfigureAwait(false);

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
                    "Registration:PublicEnabled",
                    value,
                    "Boolean",
                    "Controls whether public registration is enabled",
                    "Authentication",
                    "Production",
                    false,
                    request.AdminId),
                cancellationToken).ConfigureAwait(false);
        }

        return MediatRUnit.Value;
    }
}
