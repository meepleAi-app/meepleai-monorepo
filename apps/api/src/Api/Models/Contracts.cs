using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Api.Models;

public record QaRequest(string gameId, string query, Guid? chatId = null);
public record QaResponse(
    string answer,
    IReadOnlyList<Snippet> snippets,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null,
    IReadOnlyDictionary<string, string>? metadata = null);
public record Snippet(string text, string source, int page, int line);

public record IngestPdfResponse(string jobId);
public record SeedRequest(string gameId);
public record AgentFeedbackRequest(string messageId, string endpoint, string? outcome, string userId, string? gameId);

// AI-02: RAG Explain models
public record ExplainRequest(string gameId, string topic, Guid? chatId = null);
public record ExplainResponse(
    ExplainOutline outline,
    string script,
    IReadOnlyList<Snippet> citations,
    int estimatedReadingTimeMinutes,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null);
public record ExplainOutline(
    string mainTopic,
    IReadOnlyList<string> sections
);

// API-02: RAG Explain Streaming models (SSE)
public enum StreamingEventType
{
    StateUpdate,    // Progress updates (e.g., "Generating embeddings")
    Citations,      // Retrieved citations from vector search
    Outline,        // Generated outline structure
    ScriptChunk,    // Incremental script content chunks
    Complete,       // Final event with metadata (tokens, confidence)
    Error,          // Error event
    Heartbeat       // Keep-alive signal
}

public record RagStreamingEvent(
    StreamingEventType Type,
    object? Data,
    DateTime Timestamp
);

// Specific data models for streaming events
public record StreamingStateUpdate(string message);
public record StreamingCitations(IReadOnlyList<Snippet> citations);
public record StreamingOutline(ExplainOutline outline);
public record StreamingScriptChunk(string chunk, int chunkIndex, int totalChunks);
public record StreamingComplete(
    int estimatedReadingTimeMinutes,
    int promptTokens,
    int completionTokens,
    int totalTokens,
    double? confidence
);
public record StreamingError(string errorMessage, string? errorCode = null);
public record StreamingHeartbeat(string message = "keep-alive");

// AI-03: RAG Setup Guide models
public record SetupGuideRequest(string gameId, Guid? chatId = null);
public record SetupGuideResponse(
    string gameTitle,
    IReadOnlyList<SetupGuideStep> steps,
    int estimatedSetupTimeMinutes,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null);
public record SetupGuideStep(
    int stepNumber,
    string title,
    string instruction,
    IReadOnlyList<Snippet> references,
    bool isOptional = false
);

// ADM-02: n8n Configuration models
public record N8nConfigDto(
    string Id,
    string Name,
    string BaseUrl,
    string? WebhookUrl,
    bool IsActive,
    DateTime? LastTestedAt,
    string? LastTestResult,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateN8nConfigRequest(
    string Name,
    string BaseUrl,
    string ApiKey,
    string? WebhookUrl
);

public record UpdateN8nConfigRequest(
    string? Name,
    string? BaseUrl,
    string? ApiKey,
    string? WebhookUrl,
    bool? IsActive
);

public record N8nTestResult(
    bool Success,
    string Message,
    int? LatencyMs
);

// UI-01: Chat management models
public record ChatDto(
    Guid Id,
    string GameId,
    string GameName,
    string AgentId,
    string AgentName,
    DateTime StartedAt,
    DateTime? LastMessageAt
);

public record ChatWithHistoryDto(
    Guid Id,
    string GameId,
    string GameName,
    string AgentId,
    string AgentName,
    DateTime StartedAt,
    DateTime? LastMessageAt,
    IReadOnlyList<ChatMessageDto> Messages
);

public record ChatMessageDto(
    Guid Id,
    string Level,
    string Message,
    string? MetadataJson,
    DateTime CreatedAt
);

public record CreateChatRequest(
    string GameId,
    string AgentId
);

public record AgentDto(
    string Id,
    string GameId,
    string Name,
    string Kind,
    DateTime CreatedAt
);

// CHESS-04: Chess Agent models
public record ChessAgentRequest(
    string question,
    string? fenPosition = null,
    Guid? chatId = null
);

public record ChessAgentResponse(
    string answer,
    ChessAnalysis? analysis,
    IReadOnlyList<string> suggestedMoves,
    IReadOnlyList<Snippet> sources,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null,
    IReadOnlyDictionary<string, string>? metadata = null
);

public record ChessAnalysis(
    string? fenPosition,
    string? evaluationSummary,
    IReadOnlyList<string> keyConsiderations
);
