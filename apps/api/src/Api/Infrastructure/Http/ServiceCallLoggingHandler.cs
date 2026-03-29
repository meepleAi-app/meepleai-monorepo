using System.Diagnostics;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Http;

/// <summary>
/// DelegatingHandler that records every outbound HTTP call as a ServiceCallLogEntry.
/// Uses fire-and-forget persistence via a scoped DI scope to avoid blocking the
/// calling code and to stay safe under DI lifetime constraints.
/// </summary>
public sealed class ServiceCallLoggingHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _serviceName;
    private readonly ILogger<ServiceCallLoggingHandler> _logger;

    public ServiceCallLoggingHandler(
        IServiceProvider serviceProvider,
        string serviceName,
        ILogger<ServiceCallLoggingHandler> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _serviceName = serviceName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? capturedEx = null;
        string? errorMessage = null;

        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            capturedEx = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
            errorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }

        sw.Stop();

        if (response != null && !response.IsSuccessStatusCode)
            errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

        PersistLogEntryFireAndForget(request, response, errorMessage, sw.ElapsedMilliseconds);

        capturedEx?.Throw();
        return response!;
    }

    private void PersistLogEntryFireAndForget(
        HttpRequestMessage request, HttpResponseMessage? response,
        string? errorMessage, long latencyMs)
    {
        var correlationId = Activity.Current?.TraceId.ToString();
        if (correlationId == null && request.Headers.TryGetValues("X-Correlation-Id", out var vals))
            correlationId = vals.FirstOrDefault();

        var entry = ServiceCallLogEntry.Create(
            serviceName: _serviceName,
            httpMethod: request.Method.Method,
            requestUrl: request.RequestUri?.ToString() ?? "unknown",
            statusCode: response != null ? (int)response.StatusCode : null,
            latencyMs: latencyMs,
            isSuccess: response?.IsSuccessStatusCode ?? false,
            errorMessage: errorMessage,
            correlationId: correlationId);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IServiceCallLogRepository>();
                await repo.AddAsync(entry, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist service call log for {Service}", _serviceName);
            }
        });
    }
}
