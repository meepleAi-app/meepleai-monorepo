namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Data transfer object for AI model information.
/// Issue #3377: Models Tier Endpoint
/// </summary>
/// <param name="Id">Model identifier (e.g., "anthropic/claude-3.5-haiku").</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="Provider">LLM provider (e.g., "anthropic", "openai").</param>
/// <param name="Tier">Required subscription tier.</param>
/// <param name="CostPer1kInputTokens">Cost per 1,000 input tokens in USD.</param>
/// <param name="CostPer1kOutputTokens">Cost per 1,000 output tokens in USD.</param>
/// <param name="MaxTokens">Maximum tokens supported.</param>
/// <param name="SupportsStreaming">Whether streaming is supported.</param>
/// <param name="Description">Optional description of capabilities.</param>
public record ModelDto(
    string Id,
    string Name,
    string Provider,
    string Tier,
    decimal CostPer1kInputTokens,
    decimal CostPer1kOutputTokens,
    int MaxTokens,
    bool SupportsStreaming,
    string? Description = null);

/// <summary>
/// Response wrapper for models list endpoint.
/// Issue #3377: Models Tier Endpoint
/// </summary>
/// <param name="Models">List of available models.</param>
public record GetModelsResponse(IReadOnlyList<ModelDto> Models);
