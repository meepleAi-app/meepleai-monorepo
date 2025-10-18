using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Api.Models;

public record QaRequest(string gameId, string query, Guid? chatId = null);
public record QaResponse(
    string answer,
    IReadOnlyList<Snippet> snippets,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    IReadOnlyList<string>? followUpQuestions = null);  // CHAT-02: AI-generated follow-up questions
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
    StateUpdate,        // Progress updates (e.g., "Generating embeddings")
    Citations,          // Retrieved citations from vector search
    Outline,            // Generated outline structure
    ScriptChunk,        // Incremental script content chunks
    Complete,           // Final event with metadata (tokens, confidence)
    Error,              // Error event
    Heartbeat,          // Keep-alive signal
    Token,              // CHAT-01: Individual LLM token for QA/Setup streaming
    FollowUpQuestions   // CHAT-02: AI-generated follow-up questions
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
public record StreamingToken(string token); // CHAT-01: Individual LLM token

// CHAT-02: Follow-Up Questions models
public record StreamingFollowUpQuestions(
    [property: JsonPropertyName("questions")] IReadOnlyList<string> questions
);

/// <summary>
/// Internal DTO for LLM JSON parsing of follow-up questions.
/// </summary>
internal record FollowUpQuestionsDto(
    [property: JsonPropertyName("questions")] List<string> Questions
);

/// <summary>
/// Analytics event for tracking follow-up question clicks.
/// </summary>
public record FollowUpQuestionClickEvent(
    [property: JsonPropertyName("chatId")] Guid chatId,
    [property: JsonPropertyName("originalQuestion")] string originalQuestion,
    [property: JsonPropertyName("followUpQuestion")] string followUpQuestion,
    [property: JsonPropertyName("questionIndex")] int questionIndex
);

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

// CHAT-06: Message editing and deletion models
public record UpdateMessageRequest(
    [Required]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 10000 characters")]
    string Content
);

public record ChatMessageResponse(
    Guid Id,
    Guid ChatId,
    string? UserId,
    string Level,
    string Content,
    int SequenceNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsDeleted,
    DateTime? DeletedAt,
    string? DeletedByUserId,
    bool IsInvalidated,
    string? MetadataJson
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

// CHAT-05: Chat Export models
public record ExportChatRequest(
    string Format,
    DateTime? DateFrom = null,
    DateTime? DateTo = null
);

public class ExportResult
{
    public bool Success { get; init; }
    public Stream? Stream { get; init; }
    public string? ContentType { get; init; }
    public string? Filename { get; init; }
    public string? Error { get; init; }
    public string? ErrorDetails { get; init; }

    // Factory methods for common scenarios
    public static ExportResult NotFound(string message = "Chat not found")
        => new()
        {
            Success = false,
            Error = "not_found",
            ErrorDetails = message
        };

    public static ExportResult UnsupportedFormat(string format)
        => new()
        {
            Success = false,
            Error = "unsupported_format",
            ErrorDetails = $"Export format '{format}' is not supported. Supported formats: txt, md, pdf"
        };

    public static ExportResult GenerationFailed(string message)
        => new()
        {
            Success = false,
            Error = "generation_failed",
            ErrorDetails = message
        };

    public static ExportResult SuccessResult(Stream stream, string contentType, string filename)
        => new()
        {
            Success = true,
            Stream = stream,
            ContentType = contentType,
            Filename = filename
        };
}
