using System.Diagnostics;
using Api.Services.LlmClients;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for multi-model consensus validation (GPT-4 + Claude)
/// ISSUE-974: BGAI-032 - Multi-model validation with consensus threshold ≥0.90
/// ISSUE-975: BGAI-033 - Consensus similarity calculation using cosine ≥0.90
/// </summary>
/// <remarks>
/// Implements consensus validation by querying multiple LLM models and comparing responses.
/// Uses TF-IDF cosine similarity for semantic text comparison.
///
/// Models used:
/// - GPT-4: openai/gpt-4o (high quality, widely used)
/// - Claude: anthropic/claude-3.5-sonnet (alternative perspective)
///
/// Similarity algorithm:
/// - Tokenizes and builds TF-IDF vectors for both responses
/// - Calculates cosine similarity: (A · B) / (||A|| * ||B||)
/// - Threshold: ≥0.90 for consensus (>95% accuracy target)
/// </remarks>
public class MultiModelValidationService : IMultiModelValidationService
{
    private readonly ILlmClient _openRouterClient;
    private readonly ILogger<MultiModelValidationService> _logger;
    private readonly CosineSimilarityCalculator _similarityCalculator;

    /// <summary>
    /// Minimum consensus threshold for validation (≥0.90)
    /// Correlates to >95% accuracy target for board game rules
    /// </summary>
    public const double MinimumConsensusThreshold = 0.90;

    /// <summary>
    /// GPT-4 model identifier
    /// </summary>
    private const string Gpt4Model = "openai/gpt-4o";

    /// <summary>
    /// Claude model identifier
    /// </summary>
    private const string ClaudeModel = "anthropic/claude-3.5-sonnet";

    public MultiModelValidationService(
        IEnumerable<ILlmClient> llmClients,
        ILogger<MultiModelValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get OpenRouter client (supports both GPT-4 and Claude)
        _openRouterClient = llmClients.FirstOrDefault(c => c.ProviderName == "OpenRouter")
            ?? throw new InvalidOperationException("OpenRouter client not found in DI container");

        // Initialize cosine similarity calculator
        _similarityCalculator = new CosineSimilarityCalculator();

        _logger.LogInformation(
            "MultiModelValidationService initialized with cosine similarity and consensus threshold {Threshold:F2}",
            MinimumConsensusThreshold);
    }

    /// <inheritdoc/>
    public double ConsensusThreshold => MinimumConsensusThreshold;

    /// <inheritdoc/>
    public async Task<MultiModelConsensusResult> ValidateWithConsensusAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.3,
        int maxTokens = 1000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return CreateErrorResult("User prompt cannot be empty");
        }

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting multi-model consensus validation (GPT-4 + Claude) for prompt length {Length}",
            userPrompt.Length);

        // Query both models in parallel for efficiency
        var gpt4Task = QueryModelAsync(Gpt4Model, systemPrompt, userPrompt, temperature, maxTokens, cancellationToken);
        var claudeTask = QueryModelAsync(ClaudeModel, systemPrompt, userPrompt, temperature, maxTokens, cancellationToken);

        await Task.WhenAll(gpt4Task, claudeTask);

        var gpt4Response = await gpt4Task;
        var claudeResponse = await claudeTask;

        stopwatch.Stop();

        // Check if both models responded successfully
        if (!gpt4Response.IsSuccess || !claudeResponse.IsSuccess)
        {
            var errorMsg = BuildErrorMessage(gpt4Response, claudeResponse);
            _logger.LogError("Multi-model validation failed: {Error}", errorMsg);

            return new MultiModelConsensusResult
            {
                HasConsensus = false,
                SimilarityScore = 0.0,
                RequiredThreshold = MinimumConsensusThreshold,
                Gpt4Response = gpt4Response,
                ClaudeResponse = claudeResponse,
                ConsensusResponse = null,
                Message = errorMsg,
                TotalDurationMs = stopwatch.ElapsedMilliseconds,
                Severity = ConsensusSeverity.Error
            };
        }

        // BGAI-038: Validate response content quality (check for empty/whitespace-only responses)
        if (string.IsNullOrWhiteSpace(gpt4Response.ResponseText) || string.IsNullOrWhiteSpace(claudeResponse.ResponseText))
        {
            var emptyModel = string.IsNullOrWhiteSpace(gpt4Response.ResponseText) ? "GPT-4" : "Claude";
            var errorMsg = $"{emptyModel} returned empty or whitespace-only response";
            _logger.LogError("Multi-model validation failed: {Error}", errorMsg);

            return new MultiModelConsensusResult
            {
                HasConsensus = false,
                SimilarityScore = 0.0,
                RequiredThreshold = MinimumConsensusThreshold,
                Gpt4Response = gpt4Response,
                ClaudeResponse = claudeResponse,
                ConsensusResponse = null,
                Message = errorMsg,
                TotalDurationMs = stopwatch.ElapsedMilliseconds,
                Severity = ConsensusSeverity.Error
            };
        }

        // Calculate similarity between responses
        var similarity = CalculateSimilarity(gpt4Response.ResponseText, claudeResponse.ResponseText);
        var hasConsensus = similarity >= MinimumConsensusThreshold;
        var severity = CalculateSeverity(similarity);

        var message = hasConsensus
            ? $"Consensus achieved (similarity={similarity:F3} ≥ {MinimumConsensusThreshold:F2})"
            : $"No consensus (similarity={similarity:F3} < {MinimumConsensusThreshold:F2})";

        _logger.LogInformation(
            "Multi-model validation complete: similarity={Similarity:F3}, consensus={HasConsensus}, duration={Duration}ms",
            similarity, hasConsensus, stopwatch.ElapsedMilliseconds);

        // Use GPT-4 response as primary when consensus achieved (slightly preferred)
        var consensusResponse = hasConsensus ? gpt4Response.ResponseText : null;

        return new MultiModelConsensusResult
        {
            HasConsensus = hasConsensus,
            SimilarityScore = similarity,
            RequiredThreshold = MinimumConsensusThreshold,
            Gpt4Response = gpt4Response,
            ClaudeResponse = claudeResponse,
            ConsensusResponse = consensusResponse,
            Message = message,
            TotalDurationMs = stopwatch.ElapsedMilliseconds,
            Severity = severity
        };
    }

    /// <inheritdoc/>
    public double CalculateSimilarity(string text1, string text2)
    {
        // Delegate to cosine similarity calculator (TF-IDF based)
        return _similarityCalculator.CalculateCosineSimilarity(text1, text2);
    }

    /// <summary>
    /// Query a single LLM model and return the response
    /// </summary>
    private async Task<ModelResponse> QueryModelAsync(
        string modelId,
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Querying model {Model} for consensus validation", modelId);

            var result = await _openRouterClient.GenerateCompletionAsync(
                modelId,
                systemPrompt,
                userPrompt,
                temperature,
                maxTokens,
                cancellationToken);

            stopwatch.Stop();

            if (!result.Success)
            {
                _logger.LogWarning("Model {Model} failed: {Error}", modelId, result.ErrorMessage);

                return new ModelResponse
                {
                    ModelId = modelId,
                    ResponseText = string.Empty,
                    IsSuccess = false,
                    ErrorMessage = result.ErrorMessage,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Usage = null
                };
            }

            return new ModelResponse
            {
                ModelId = modelId,
                ResponseText = result.Response,
                IsSuccess = true,
                ErrorMessage = null,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Usage = result.Usage
            };
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation to allow proper request abort
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Exception querying model {Model}", modelId);

            return new ModelResponse
            {
                ModelId = modelId,
                ResponseText = string.Empty,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Usage = null
            };
        }
    }

    /// <summary>
    /// Calculate consensus severity based on similarity score
    /// </summary>
    private ConsensusSeverity CalculateSeverity(double similarity)
    {
        return similarity switch
        {
            >= 0.90 => ConsensusSeverity.High,
            >= 0.70 => ConsensusSeverity.Moderate,
            >= 0.50 => ConsensusSeverity.Low,
            _ => ConsensusSeverity.None
        };
    }

    /// <summary>
    /// Build error message when one or both models fail
    /// </summary>
    private string BuildErrorMessage(ModelResponse gpt4, ModelResponse claude)
    {
        if (!gpt4.IsSuccess && !claude.IsSuccess)
        {
            return $"Both models failed: GPT-4='{gpt4.ErrorMessage}', Claude='{claude.ErrorMessage}'";
        }

        if (!gpt4.IsSuccess)
        {
            return $"GPT-4 failed: {gpt4.ErrorMessage}";
        }

        return $"Claude failed: {claude.ErrorMessage}";
    }

    /// <summary>
    /// Create error result for validation failures
    /// </summary>
    private MultiModelConsensusResult CreateErrorResult(string errorMessage)
    {
        var emptyResponse = new ModelResponse
        {
            ModelId = "N/A",
            ResponseText = string.Empty,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            DurationMs = 0,
            Usage = null
        };

        return new MultiModelConsensusResult
        {
            HasConsensus = false,
            SimilarityScore = 0.0,
            RequiredThreshold = MinimumConsensusThreshold,
            Gpt4Response = emptyResponse,
            ClaudeResponse = emptyResponse,
            ConsensusResponse = null,
            Message = errorMessage,
            TotalDurationMs = 0,
            Severity = ConsensusSeverity.Error
        };
    }
}
