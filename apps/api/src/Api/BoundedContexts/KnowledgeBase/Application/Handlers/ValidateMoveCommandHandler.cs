using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// ISSUE-3759: Handler for ValidateMoveCommand.
/// Calls orchestration-service for LangGraph-based Arbitro agent processing.
/// </summary>
internal class ValidateMoveCommandHandler : IRequestHandler<ValidateMoveCommand, ValidateMoveResponse>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ValidateMoveCommandHandler> _logger;

    public ValidateMoveCommandHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<ValidateMoveCommandHandler> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OrchestrationService");
        _logger = logger;
    }

    public async Task<ValidateMoveResponse> Handle(ValidateMoveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Validating move for game {GameId}, session {SessionId}: {Move}",
                request.GameId, request.SessionId, request.Move);

            // Call orchestration service for Arbitro agent
            var apiRequest = new
            {
                game_id = request.GameId,
                session_id = request.SessionId,
                move = request.Move,
                game_state = request.GameState,
                agent_type = "arbitro" // Route to Arbitro agent
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
                "Move validation completed: valid={IsValid}, confidence={Confidence}, time={Time}ms",
                orchestrationResponse.IsValid, orchestrationResponse.Confidence, orchestrationResponse.ExecutionTimeMs);

            return new ValidateMoveResponse(
                IsValid: orchestrationResponse.IsValid,
                Reason: orchestrationResponse.Reason,
                AppliedRuleIds: orchestrationResponse.AppliedRuleIds,
                Confidence: orchestrationResponse.Confidence,
                Citations: orchestrationResponse.Citations,
                ExecutionTimeMs: orchestrationResponse.ExecutionTimeMs,
                ErrorMessage: orchestrationResponse.Error
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Orchestration service unavailable");
            throw new ExternalServiceException("Arbitro agent service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Move validation failed");
            throw;
        }
    }

    private sealed record OrchestrationResponse(
        [property: JsonPropertyName("is_valid")] bool IsValid,
        [property: JsonPropertyName("reason")] string Reason,
        [property: JsonPropertyName("applied_rule_ids")] List<Guid> AppliedRuleIds,
        [property: JsonPropertyName("confidence")] double Confidence,
        [property: JsonPropertyName("citations")] List<string> Citations,
        [property: JsonPropertyName("execution_time_ms")] double ExecutionTimeMs,
        [property: JsonPropertyName("error")] string? Error
    );
}
