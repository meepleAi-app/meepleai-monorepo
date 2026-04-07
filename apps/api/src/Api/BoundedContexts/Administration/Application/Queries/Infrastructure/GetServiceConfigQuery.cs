using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries.Infrastructure;

internal record GetServiceConfigQuery(string ServiceName) : IQuery<ServiceConfigResponse>;

internal class GetServiceConfigQueryHandler
    : IQueryHandler<GetServiceConfigQuery, ServiceConfigResponse>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GetServiceConfigQueryHandler> _logger;

    public GetServiceConfigQueryHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<GetServiceConfigQueryHandler> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ServiceConfigResponse> Handle(
        GetServiceConfigQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ServiceRegistry.ConfigParams.TryGetValue(request.ServiceName, out var paramDefs))
            return new ServiceConfigResponse(request.ServiceName, Array.Empty<ServiceConfigParamDto>());

        var currentValues = await FetchCurrentConfigAsync(request.ServiceName, cancellationToken)
            .ConfigureAwait(false);

        var parameters = paramDefs.Select(paramDef =>
            new ServiceConfigParamDto(
                Key: paramDef.Key,
                DisplayName: paramDef.DisplayName,
                Value: currentValues.GetValueOrDefault(paramDef.Key, "unknown"),
                Type: paramDef.Type,
                Options: paramDef.Options,
                MinValue: paramDef.MinValue,
                MaxValue: paramDef.MaxValue)).ToList();

        return new ServiceConfigResponse(request.ServiceName, parameters);
    }

    private async Task<Dictionary<string, string>> FetchCurrentConfigAsync(
        string serviceName, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient($"ai-{serviceName}");
            var response = await client.GetAsync("/config", ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, string>(StringComparer.Ordinal);

            var json = await response.Content
                .ReadFromJsonAsync<Dictionary<string, object>>(ct)
                .ConfigureAwait(false);

            return json?.ToDictionary(
                    kv => kv.Key, kv => kv.Value?.ToString() ?? "", StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch config from {Service}", serviceName);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
