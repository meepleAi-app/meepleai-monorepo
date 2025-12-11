using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Api.Services; // AI-14: For SearchMode enum

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

/// <summary>
/// AI-14: Updated to support hybrid search modes
/// </summary>
public record QaRequest(
    string gameId,
    string query,
    Guid? chatId = null,
    SearchMode searchMode = SearchMode.Hybrid); // AI-14: Default to hybrid search
public record QaResponse(
    string answer,
    IReadOnlyList<Snippet> snippets,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    IReadOnlyList<string>? followUpQuestions = null);  // CHAT-02: AI-generated follow-up questions
public record Snippet(string text, string source, int page, int line, float score);

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
    FollowUpQuestions,  // CHAT-02: AI-generated follow-up questions
    SetupStep           // AI-03: Individual setup step for streaming setup guide
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
public record StreamingSetupStep(SetupGuideStep step); // AI-03: Individual setup step

// CHAT-02: Follow-Up Questions models
public record StreamingFollowUpQuestions(
    [property: JsonPropertyName("questions")] IReadOnlyList<string> questions
);

/// <summary>
/// Internal DTO for LLM JSON parsing of follow-up questions.
/// </summary>
internal record FollowUpQuestionsDto(
    [property: JsonPropertyName("questions")] IList<string> Questions
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
public record N8NConfigDto(
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

public record CreateN8NConfigRequest(
    string Name,
    string BaseUrl,
    string ApiKey,
    string? WebhookUrl
);

public record UpdateN8NConfigRequest(
    string? Name,
    string? BaseUrl,
    string? ApiKey,
    string? WebhookUrl,
    bool? IsActive
);

public record N8NTestResult(
    bool Success,
    string Message,
    int? LatencyMs
);

// N8N-04: Workflow template models
public record WorkflowTemplateDto(
    string Id,
    string Name,
    string Version,
    string Description,
    string Category,
    string Author,
    IList<string> Tags,
    string Icon,
    string? Screenshot,
    string? Documentation,
    IList<TemplateParameterDto> Parameters
);

public record TemplateParameterDto(
    string Name,
    string Type,
    string Label,
    string Description,
    bool Required,
    string? Default,
    IList<string>? Options,
    bool Sensitive
);

public record WorkflowTemplateDetailDto(
    string Id,
    string Name,
    string Version,
    string Description,
    string Category,
    string Author,
    IList<string> Tags,
    string Icon,
    string? Screenshot,
    string? Documentation,
    IList<TemplateParameterDto> Parameters,
    object Workflow
);

public record ImportTemplateRequest(
    IDictionary<string, string> Parameters
);

public record ImportTemplateResponse(
    string WorkflowId,
    string Message
);

public record ValidateTemplateRequest(
    string TemplateJson
);

public record ValidateTemplateResponse(
    [property: JsonPropertyName("valid")] bool IsValid,
    [property: JsonPropertyName("errors")] IList<string>? Errors
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

// AI-11: Quality Scoring models
/// <summary>
/// Result model for low-quality responses endpoint.
/// Contains paginated list of low-quality AI responses with their quality metrics.
/// </summary>
public record LowQualityResponsesResult(
    int TotalCount,
    IReadOnlyList<LowQualityResponseDto> Responses
);

/// <summary>
/// DTO for a low-quality AI response.
/// Includes all quality dimensions and metadata.
/// </summary>
public record LowQualityResponseDto(
    Guid Id,
    DateTime CreatedAt,
    string Query,
    double RagConfidence,
    double LlmConfidence,
    double CitationQuality,
    double OverallConfidence,
    bool IsLowQuality
);

// ADMIN-01: User Management models
/// <summary>
/// DTO for user information.
/// Used in admin user management interfaces.
/// </summary>
public record UserDto(
    string Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    DateTime? LastSeenAt
);

/// <summary>
/// Request model for creating a new user.
/// Includes email, password, display name, and role.
/// </summary>
public record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string DisplayName,
    string Role = "User"
);

/// <summary>
/// Request model for updating an existing user.
/// All fields are optional - only provided fields will be updated.
/// </summary>
public record UpdateUserRequest(
    [EmailAddress] string? Email = null,
    string? DisplayName = null,
    string? Role = null
);

/// <summary>
/// Paginated result container for any list of items.
/// Generic type T represents the item type (e.g., UserDto).
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize
);

// CONFIG-01: Dynamic Configuration System models
public record SystemConfigurationDto(
    string Id,
    string Key,
    string Value,
    string ValueType,
    string? Description,
    string Category,
    bool IsActive,
    bool RequiresRestart,
    string Environment,
    int Version,
    string? PreviousValue,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedByUserId,
    string? UpdatedByUserId,
    DateTime? LastToggledAt);

public record CreateConfigurationRequest(
    [Required][StringLength(500, MinimumLength = 1)] string Key,
    [Required] string Value,
    [Required] string ValueType = "string",
    string? Description = null,
    string Category = "General",
    bool IsActive = true,
    bool RequiresRestart = false,
    string Environment = "All");

public record UpdateConfigurationRequest(
    string? Value = null,
    string? ValueType = null,
    string? Description = null,
    string? Category = null,
    bool? IsActive = null,
    bool? RequiresRestart = null,
    string? Environment = null);

public record ConfigurationHistoryDto(
    string Id,
    string ConfigurationId,
    string Key,
    string OldValue,
    string NewValue,
    int Version,
    DateTime ChangedAt,
    string ChangedByUserId,
    string ChangeReason);

public record BulkConfigurationUpdateRequest(
    [Required] IReadOnlyList<ConfigurationUpdate> Updates);

public record ConfigurationUpdate(
    [Required] string Id,
    [Required] string Value);

public record ConfigurationValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public record ConfigurationExportDto(
    IReadOnlyList<SystemConfigurationDto> Configurations,
    DateTime ExportedAt,
    string Environment);

public record ConfigurationImportRequest(
    [Required] IReadOnlyList<CreateConfigurationRequest> Configurations,
    bool OverwriteExisting = false);

// CONFIG-05: Feature Flags models
public record FeatureFlagDto(
    string FeatureName,
    bool IsEnabled,
    string? RoleRestriction,
    string? Description);

public record FeatureFlagUpdateRequest(
    [Required] bool Enabled,
    string? Role = null);

// EDIT-05: Rule Specification Comments models
public record RuleCommentDto(
    Guid Id,
    string GameId,
    string Version,
    int? LineNumber,
    string? LineContext,
    Guid? ParentCommentId,
    string CommentText,
    string UserId,
    string UserDisplayName,
    bool IsResolved,
    string? ResolvedByUserId,
    string? ResolvedByDisplayName,
    DateTime? ResolvedAt,
    IList<string> MentionedUserIds,
    IList<RuleCommentDto> Replies,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateCommentRequest(
    [Required] string GameId,
    [Required] string Version,
    int? LineNumber,
    [Required] string CommentText
);

public record CreateReplyRequest(
    [Required] string CommentText
);

public record UserSearchResultDto(
    string Id,
    string DisplayName,
    string Email
);

// ADMIN-02: Analytics Dashboard models
public record DashboardStatsDto(
    DashboardMetrics Metrics,
    IReadOnlyList<TimeSeriesDataPoint> UserTrend,
    IReadOnlyList<TimeSeriesDataPoint> SessionTrend,
    IReadOnlyList<TimeSeriesDataPoint> ApiRequestTrend,
    IReadOnlyList<TimeSeriesDataPoint> PdfUploadTrend,
    IReadOnlyList<TimeSeriesDataPoint> ChatMessageTrend,
    DateTime GeneratedAt
);

public record DashboardMetrics(
    int TotalUsers,
    int ActiveSessions,
    int ApiRequestsToday,
    int TotalPdfDocuments,
    int TotalChatMessages,
    double AverageConfidenceScore,
    int TotalRagRequests,
    long TotalTokensUsed,
    // Issue #874: Additional metrics for centralized dashboard (12+ metrics total)
    int TotalGames,
    int ApiRequests7d,
    int ApiRequests30d,
    double AverageLatency24h,
    double AverageLatency7d,
    double ErrorRate24h,
    int ActiveAlerts,
    int ResolvedAlerts
);

public record TimeSeriesDataPoint(
    DateTime Date,
    long Count,
    double? AverageValue = null
);

public record AnalyticsQueryParams(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Days = 30,
    string? GameId = null,
    string? RoleFilter = null
);

// Issue #874: Activity Feed models for admin dashboard
public record RecentActivityDto(
    IReadOnlyList<ActivityEvent> Events,
    int TotalCount,
    DateTime GeneratedAt
);

public record ActivityEvent(
    string Id,
    ActivityEventType EventType,
    string Description,
    string? UserId,
    string? UserEmail,
    string? EntityId,
    string? EntityType,
    DateTime Timestamp,
    ActivitySeverity Severity = ActivitySeverity.Info
);

public enum ActivityEventType
{
    UserRegistered,
    UserLogin,
    PdfUploaded,
    PdfProcessed,
    AlertCreated,
    AlertResolved,
    GameAdded,
    ConfigurationChanged,
    ErrorOccurred,
    SystemEvent
}

public enum ActivitySeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public record ExportDataRequest(
    [Required] string Format, // "csv" or "json"
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? GameId = null
);

// AI-13: BoardGameGeek API integration models
public record BggSearchResultDto(
    int BggId,
    string Name,
    int? YearPublished,
    string? ThumbnailUrl,
    string Type // "boardgame", "boardgameexpansion", etc.
);

public record BggGameDetailsDto(
    int BggId,
    string Name,
    string? Description,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTime,
    int? MinPlayTime,
    int? MaxPlayTime,
    int? MinAge,
    double? AverageRating,
    double? BayesAverageRating,
    int? UsersRated,
    double? AverageWeight, // Complexity: 1-5
    string? ThumbnailUrl,
    string? ImageUrl,
    IList<string> Categories,
    IList<string> Mechanics,
    IList<string> Designers,
    IList<string> Publishers
);

public record BggSearchRequest(
    [Required][MinLength(1)] string Query,
    bool Exact = false
);

// N8N-05: Workflow Error Logging models
public record LogWorkflowErrorRequest(
    [Required][MaxLength(255)] string WorkflowId,
    [Required][MaxLength(255)] string ExecutionId,
    [Required][MaxLength(5000)] string ErrorMessage,
    [MaxLength(255)] string? NodeName = null,
    int RetryCount = 0,
    [MaxLength(10000)] string? StackTrace = null
);

public record WorkflowErrorDto(
    Guid Id,
    string WorkflowId,
    string ExecutionId,
    string ErrorMessage,
    string? NodeName,
    int RetryCount,
    string? StackTrace,
    DateTime CreatedAt
);

public record WorkflowErrorsQueryParams(
    string? WorkflowId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int Limit = 20
);

// OPS-07: Alerting system models
public record AlertDto(
    Guid Id,
    string AlertType,
    string Severity,
    string Message,
    IDictionary<string, object>? Metadata,
    DateTime TriggeredAt,
    DateTime? ResolvedAt,
    bool IsActive,
    IDictionary<string, bool>? ChannelSent
);

public record PrometheusAlertWebhook(
    string Version,
    string GroupKey,
    string TruncatedAlerts,
    string Status,
    PrometheusAlert[] Alerts
);

public record PrometheusAlert(
    string Status, // "firing" or "resolved"
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyDictionary<string, string> Annotations,
    DateTime StartsAt,
    DateTime? EndsAt
);

// ADMIN-01: Prompt Management DTOs - See PromptManagementDto.cs for full definitions