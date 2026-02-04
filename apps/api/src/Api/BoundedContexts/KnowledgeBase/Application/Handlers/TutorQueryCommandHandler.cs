using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// ISSUE-3499: Handler for TutorQueryCommand.
/// Calls orchestration-service for LangGraph-based tutor agent processing.
/// </summary>
internal class TutorQueryCommandHandler : IRequestHandler<TutorQueryCommand, TutorQueryResponse>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TutorQueryCommandHandler> _logger;

    public TutorQueryCommandHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<TutorQueryCommandHandler> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OrchestrationService");
        _logger = logger;
    }

    public async Task<TutorQueryResponse> Handle(TutorQueryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Executing tutor query for game {GameId}, session {SessionId}",
                request.GameId, request.SessionId);

            // Call orchestration service
            var apiRequest = new
            {
                game_id = request.GameId,
                session_id = request.SessionId,
                query = request.Query,
            };

            var httpResponse = await _httpClient.PostAsJsonAsync(
                "/execute",
                apiRequest,
                cancellationToken).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();

            var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var orchestrationResponse = JsonSerializer.Deserialize<OrchestrationResponse>(responseContent);

            if (orchestrationResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize orchestration response");
            }

            _logger.LogInformation(
                "Tutor query completed: agent={Agent}, confidence={Confidence}, time={Time}ms",
                orchestrationResponse.AgentType, orchestrationResponse.Confidence, orchestrationResponse.ExecutionTimeMs);

            return new TutorQueryResponse(
                Response: orchestrationResponse.Response,
                AgentType: orchestrationResponse.AgentType,
                Confidence: orchestrationResponse.Confidence,
                Citations: orchestrationResponse.Citations,
                ExecutionTimeMs: orchestrationResponse.ExecutionTimeMs
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Orchestration service unavailable");
            throw new InvalidOperationException("Tutor agent service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tutor query failed");
            throw;
        }
    }

    private record OrchestrationResponse(
        [property: JsonPropertyName("agent_type")] string AgentType,
        [property: JsonPropertyName("response")] string Response,
        [property: JsonPropertyName("confidence")] double Confidence,
        [property: JsonPropertyName("citations")] List<string> Citations,
        [property: JsonPropertyName("execution_time_ms")] double ExecutionTimeMs,
        [property: JsonPropertyName("error")] string? Error
    );
}
