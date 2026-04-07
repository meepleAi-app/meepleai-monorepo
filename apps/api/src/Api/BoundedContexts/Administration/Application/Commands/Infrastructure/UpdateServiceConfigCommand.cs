using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Api.BoundedContexts.Administration.Application.Commands.Infrastructure;

internal record UpdateServiceConfigCommand(
    string ServiceName,
    Dictionary<string, string> Parameters) : ICommand<ConfigUpdateResponse>;

internal class UpdateServiceConfigCommandHandler
    : ICommandHandler<UpdateServiceConfigCommand, ConfigUpdateResponse>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateServiceConfigCommandHandler> _logger;

    public UpdateServiceConfigCommandHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateServiceConfigCommandHandler> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfigUpdateResponse> Handle(
        UpdateServiceConfigCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var client = _httpClientFactory.CreateClient($"ai-{command.ServiceName}");

        _logger.LogWarning("Updating config for {Service}: {Params}",
            command.ServiceName, string.Join(", ", command.Parameters.Keys));

        var response = await client
            .PutAsJsonAsync("/config", command.Parameters, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Config update failed for {command.ServiceName}: {response.StatusCode} - {body}");
        }

        return new ConfigUpdateResponse(command.ServiceName, command.Parameters.Keys.ToList());
    }
}
