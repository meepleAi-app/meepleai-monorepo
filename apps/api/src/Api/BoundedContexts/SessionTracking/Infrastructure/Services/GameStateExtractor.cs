using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Services;
using Api.Services.LlmClients;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Extracts structured game state from vision snapshot images using multimodal LLM.
/// Session Vision AI feature.
/// </summary>
internal sealed class GameStateExtractor : IGameStateExtractor
{
    private const double MinConfidence = 0.4;

    private static readonly string SystemPrompt = """
        You are a board game vision analyzer. Analyze the photo(s) of a board game in progress
        and extract the current game state as structured JSON.

        Return ONLY valid JSON with this structure:
        {
          "confidence": 0.0-1.0,
          "game_phase": "setup|early|mid|late|end",
          "visible_elements": {
            "board_state": "description of the board",
            "player_positions": [],
            "resources": {},
            "score_indicators": {},
            "cards_visible": [],
            "tokens_pieces": []
          },
          "observations": "brief natural language summary of what you see",
          "turn_estimate": null
        }

        Be honest about confidence. If the image is blurry, partially obscured, or you cannot
        identify the game, set confidence low. Only include elements you can actually see.
        """;

    private readonly IVisionSnapshotRepository _snapshotRepository;
    private readonly ILlmService _llmService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<GameStateExtractor> _logger;

    public GameStateExtractor(
        IVisionSnapshotRepository snapshotRepository,
        ILlmService llmService,
        IBlobStorageService blobStorageService,
        ILogger<GameStateExtractor> logger)
    {
        _snapshotRepository = snapshotRepository;
        _llmService = llmService;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<string?> ExtractIfNeededAsync(Guid sessionId, string? gameName, CancellationToken ct = default)
    {
        try
        {
            // 1. Get latest snapshot for session
            var snapshot = await _snapshotRepository
                .GetLatestBySessionIdAsync(sessionId, ct)
                .ConfigureAwait(false);

            if (snapshot is null)
            {
                _logger.LogDebug("No vision snapshots found for session {SessionId}", sessionId);
                return null;
            }

            // 2. Idempotent: if already extracted, return cached result
            if (snapshot.ExtractedGameState is not null)
            {
                return snapshot.ExtractedGameState;
            }

            if (snapshot.Images.Count == 0)
            {
                _logger.LogDebug("Vision snapshot {SnapshotId} has no images", snapshot.Id);
                return null;
            }

            // 3. Load images from blob storage and build multimodal message
            var gameIdForStorage = sessionId.ToString("N");
            var contentParts = new List<ContentPart>();

            var userPromptText = gameName is not null
                ? $"Analyze this photo of a {gameName} game in progress. Extract the visible game state."
                : "Analyze this photo of a board game in progress. Extract the visible game state.";

            contentParts.Add(new TextContentPart(userPromptText));

            foreach (var image in snapshot.Images)
            {
                using var stream = await _blobStorageService
                    .RetrieveAsync(image.StorageKey, gameIdForStorage, ct)
                    .ConfigureAwait(false);

                if (stream is null)
                {
                    _logger.LogWarning(
                        "Could not retrieve image {StorageKey} for snapshot {SnapshotId}",
                        image.StorageKey, snapshot.Id);
                    continue;
                }

                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
                var base64 = Convert.ToBase64String(ms.ToArray());

                contentParts.Add(new ImageContentPart(base64, image.MediaType));
            }

            if (contentParts.Count <= 1) // Only text, no images loaded
            {
                _logger.LogWarning(
                    "Could not load any images for snapshot {SnapshotId}", snapshot.Id);
                return null;
            }

            // 4. Call multimodal LLM
            var messages = new List<LlmMessage>
            {
                LlmMessage.FromText("system", SystemPrompt),
                new("user", contentParts)
            };

            var result = await _llmService
                .GenerateMultimodalCompletionAsync(messages, RequestSource.AgentTask, ct)
                .ConfigureAwait(false);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
            {
                _logger.LogWarning(
                    "LLM extraction failed for snapshot {SnapshotId}: {Error}",
                    snapshot.Id, result.ErrorMessage);
                return null;
            }

            // 5. Parse and validate confidence
            try
            {
                using var doc = JsonDocument.Parse(result.Response);
                var confidence = doc.RootElement.TryGetProperty("confidence", out var confProp)
                    ? confProp.GetDouble()
                    : 0.0;

                if (confidence < MinConfidence)
                {
                    _logger.LogInformation(
                        "Game state extraction confidence too low ({Confidence:F2}) for snapshot {SnapshotId}",
                        confidence, snapshot.Id);
                    return null;
                }

                // 6. Save to snapshot and return
                snapshot.UpdateGameState(result.Response);
                await _snapshotRepository.UpdateAsync(snapshot, ct).ConfigureAwait(false);
                await _snapshotRepository.SaveChangesAsync(ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Extracted game state for snapshot {SnapshotId} with confidence {Confidence:F2}",
                    snapshot.Id, confidence);

                return result.Response;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to parse LLM response as JSON for snapshot {SnapshotId}",
                    snapshot.Id);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during game state extraction for session {SessionId}",
                sessionId);
            return null; // Never block — extraction failures are non-critical
        }
    }
}
