using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Api.Services; // AI-14: For SearchMode enum

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

/// <summary>
/// AI-14: Updated to support hybrid search modes
/// Issue #2051: Supports document source filtering
/// </summary>
internal record QaRequest(
    string gameId,
    string query,
    Guid? chatId = null,
    SearchMode searchMode = SearchMode.Hybrid, // AI-14: Default to hybrid search
    IReadOnlyList<Guid>? documentIds = null); // Issue #2051: Filter by document IDs (null = all)
internal record QaResponse(
    string answer,
    IReadOnlyList<Snippet> snippets,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    IReadOnlyList<string>? followUpQuestions = null);  // CHAT-02: AI-generated follow-up questions
internal record Snippet(string text, string source, int page, int line, float score);

internal record IngestPdfResponse(string jobId);
internal record SeedRequest(string gameId);
internal record AgentFeedbackRequest(string messageId, string endpoint, string? outcome, string userId, string? gameId);

// AI-02: RAG Explain models
internal record ExplainRequest(string gameId, string topic, Guid? chatId = null);
internal record ExplainResponse(
    ExplainOutline outline,
    string script,
    IReadOnlyList<Snippet> citations,
    int estimatedReadingTimeMinutes,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null);
internal record ExplainOutline(
    string mainTopic,
    IReadOnlyList<string> sections
);

// API-02: RAG Explain Streaming models (SSE)
internal enum StreamingEventType
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

internal record RagStreamingEvent(
    StreamingEventType Type,
    object? Data,
    DateTime Timestamp
);

// Specific data models for streaming events
internal record StreamingStateUpdate(string message);
internal record StreamingCitations(IReadOnlyList<Snippet> citations);
internal record StreamingOutline(ExplainOutline outline);
internal record StreamingScriptChunk(string chunk, int chunkIndex, int totalChunks);
internal record StreamingComplete(
    int estimatedReadingTimeMinutes,
    int promptTokens,
    int completionTokens,
    int totalTokens,
    double? confidence
);
internal record StreamingError(string errorMessage, string? errorCode = null);
internal record StreamingHeartbeat(string message = "keep-alive");
internal record StreamingToken(string token); // CHAT-01: Individual LLM token
internal record StreamingSetupStep(SetupGuideStep step); // AI-03: Individual setup step

// CHAT-02: Follow-Up Questions models
internal record StreamingFollowUpQuestions(
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
internal record FollowUpQuestionClickEvent(
    [property: JsonPropertyName("chatId")] Guid chatId,
    [property: JsonPropertyName("originalQuestion")] string originalQuestion,
    [property: JsonPropertyName("followUpQuestion")] string followUpQuestion,
    [property: JsonPropertyName("questionIndex")] int questionIndex
);

// AI-03: RAG Setup Guide models
internal record SetupGuideRequest(string gameId, Guid? chatId = null);
internal record SetupGuideResponse(
    string gameTitle,
    IReadOnlyList<SetupGuideStep> steps,
    int estimatedSetupTimeMinutes,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    double? confidence = null);
internal record SetupGuideStep(
    int stepNumber,
    string title,
    string instruction,
    IReadOnlyList<Snippet> references,
    bool isOptional = false
);

// ADM-02: n8n Configuration models
internal record N8NConfigDto(
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

internal record CreateN8NConfigRequest(
    string Name,
    string BaseUrl,
    string ApiKey,
    string? WebhookUrl
);

internal record UpdateN8NConfigRequest(
    string? Name,
    string? BaseUrl,
    string? ApiKey,
    string? WebhookUrl,
    bool? IsActive
);

internal record N8NTestResult(
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

internal record ImportTemplateRequest(
    IDictionary<string, string> Parameters
);

public record ImportTemplateResponse(
    string WorkflowId,
    string Message
);

internal record ValidateTemplateRequest(
    string TemplateJson
);

public record ValidateTemplateResponse(
    [property: JsonPropertyName("valid")] bool IsValid,
    [property: JsonPropertyName("errors")] IList<string>? Errors
);

// UI-01: Chat management models
internal record ChatDto(
    Guid Id,
    string GameId,
    string GameName,
    string AgentId,
    string AgentName,
    DateTime StartedAt,
    DateTime? LastMessageAt
);

internal record ChatWithHistoryDto(
    Guid Id,
    string GameId,
    string GameName,
    string AgentId,
    string AgentName,
    DateTime StartedAt,
    DateTime? LastMessageAt,
    IReadOnlyList<ChatMessageDto> Messages
);

internal record ChatMessageDto(
    Guid Id,
    string Level,
    string Message,
    string? MetadataJson,
    DateTime CreatedAt
);

internal record CreateChatRequest(
    string GameId,
    string AgentId
);

// CHAT-06: Message editing and deletion models
internal record UpdateMessageRequest(
    [Required]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 10000 characters")]
    string Content
);

internal record ChatMessageResponse(
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

internal record AgentDto(
    string Id,
    string GameId,
    string Name,
    string Kind,
    DateTime CreatedAt
);

// CHESS-04: Chess Agent models
internal record ChessAgentRequest(
    string question,
    string? fenPosition = null,
    Guid? chatId = null
);

internal record ChessAgentResponse(
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

internal record ChessAnalysis(
    string? fenPosition,
    string? evaluationSummary,
    IReadOnlyList<string> keyConsiderations
);

// Issue #2421: Player Mode AI Suggestion models
internal record PlayerModeSuggestionRequest(
    [Required] string gameId,
    [Required] IReadOnlyDictionary<string, object> gameState,
    string? query = null,
    Guid? chatThreadId = null
);

internal record PlayerModeSuggestionResponse(
    SuggestedMove primarySuggestion,
    IReadOnlyList<SuggestedMove>? alternativeMoves,
    double overallConfidence,
    string? strategicContext,
    IReadOnlyList<Snippet>? sources,
    int promptTokens = 0,
    int completionTokens = 0,
    int totalTokens = 0,
    int processingTimeMs = 0,
    IReadOnlyDictionary<string, object>? metadata = null
);

internal record SuggestedMove(
    string action,
    string rationale,
    string? expectedOutcome,
    double confidence
);

// CHAT-05: Chat Export models
internal record ExportChatRequest(
    string Format,
    DateTime? DateFrom = null,
    DateTime? DateTo = null
);

internal class ExportResult
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
internal record LowQualityResponsesResult(
    int TotalCount,
    IReadOnlyList<LowQualityResponseDto> Responses
);

/// <summary>
/// DTO for a low-quality AI response.
/// Includes all quality dimensions and metadata.
/// </summary>
internal record LowQualityResponseDto(
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
internal record UserDto(
    string Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    DateTime? LastSeenAt,
    bool IsSuspended = false,
    string? SuspendReason = null
);

/// <summary>
/// Request model for creating a new user.
/// Includes email, password, display name, and role.
/// </summary>
internal record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string DisplayName,
    string Role = "User"
);

/// <summary>
/// Request model for updating an existing user.
/// All fields are optional - only provided fields will be updated.
/// </summary>
internal record UpdateUserRequest(
    [EmailAddress] string? Email = null,
    string? DisplayName = null,
    string? Role = null
);

/// <summary>
/// Paginated result container for any list of items.
/// Generic type T represents the item type (e.g., UserDto).
/// </summary>
internal record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize
);

// CONFIG-01: Dynamic Configuration System models
internal record SystemConfigurationDto(
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

internal record CreateConfigurationRequest(
    [Required][StringLength(500, MinimumLength = 1)] string Key,
    [Required] string Value,
    [Required] string ValueType = "string",
    string? Description = null,
    string Category = "General",
    bool IsActive = true,
    bool RequiresRestart = false,
    string Environment = "All");

internal record UpdateConfigurationRequest(
    string? Value = null,
    string? ValueType = null,
    string? Description = null,
    string? Category = null,
    bool? IsActive = null,
    bool? RequiresRestart = null,
    string? Environment = null);

internal record ConfigurationHistoryDto(
    string Id,
    string ConfigurationId,
    string Key,
    string OldValue,
    string NewValue,
    int Version,
    DateTime ChangedAt,
    string ChangedByUserId,
    string ChangeReason);

internal record BulkConfigurationUpdateRequest(
    [Required] IReadOnlyList<ConfigurationUpdate> Updates);

internal record ConfigurationUpdate(
    [Required] string Id,
    [Required] string Value);

internal record ConfigurationValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

internal record ConfigurationExportDto(
    IReadOnlyList<SystemConfigurationDto> Configurations,
    DateTime ExportedAt,
    string Environment);

internal record ConfigurationImportRequest(
    [Required] IReadOnlyList<CreateConfigurationRequest> Configurations,
    bool OverwriteExisting = false);

// CONFIG-05: Feature Flags models
// Issue #3073: Extended to support tier-based feature flags (Free/Normal/Premium)
internal record FeatureFlagDto(
    string FeatureName,
    bool IsEnabled,
    string? RoleRestriction,
    string? TierRestriction,
    string? Description);

internal record FeatureFlagUpdateRequest(
    [Required] bool Enabled,
    string? Role = null,
    string? Tier = null);

// EDIT-05: Rule Specification Comments models
internal record RuleCommentDto(
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

internal record CreateCommentRequest(
    [Required] string GameId,
    [Required] string Version,
    int? LineNumber,
    [Required] string CommentText
);

internal record CreateReplyRequest(
    [Required] string CommentText
);

internal record UserSearchResultDto(
    string Id,
    string DisplayName,
    string Email
);

// ADMIN-02: Analytics Dashboard models
internal record DashboardStatsDto(
    DashboardMetrics Metrics,
    List<TimeSeriesDataPoint> UserTrend,
    List<TimeSeriesDataPoint> SessionTrend,
    List<TimeSeriesDataPoint> ApiRequestTrend,
    List<TimeSeriesDataPoint> PdfUploadTrend,
    List<TimeSeriesDataPoint> ChatMessageTrend,
    DateTime GeneratedAt
);

internal record DashboardMetrics(
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

internal record TimeSeriesDataPoint(
    DateTime Date,
    long Count,
    double? AverageValue = null
);

internal record AnalyticsQueryParams(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Days = 30,
    string? GameId = null,
    string? RoleFilter = null
);

// Issue #874: Activity Feed models for admin dashboard
internal record RecentActivityDto(
    IReadOnlyList<ActivityEvent> Events,
    int TotalCount,
    DateTime GeneratedAt
);

internal record ActivityEvent(
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

internal enum ActivityEventType
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

internal enum ActivitySeverity
{
    Info,
    Warning,
    Error,
    Critical
}

internal record ExportDataRequest(
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
internal record LogWorkflowErrorRequest(
    [Required][MaxLength(255)] string WorkflowId,
    [Required][MaxLength(255)] string ExecutionId,
    [Required][MaxLength(5000)] string ErrorMessage,
    [MaxLength(255)] string? NodeName = null,
    int RetryCount = 0,
    [MaxLength(10000)] string? StackTrace = null
);

internal record WorkflowErrorDto(
    Guid Id,
    string WorkflowId,
    string ExecutionId,
    string ErrorMessage,
    string? NodeName,
    int RetryCount,
    string? StackTrace,
    DateTime CreatedAt
);

internal record WorkflowErrorsQueryParams(
    string? WorkflowId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int Limit = 20
);

// OPS-07: Alerting system models
internal record AlertDto(
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

internal record PrometheusAlertWebhook(
    string Version,
    string GroupKey,
    string TruncatedAlerts,
    string Status,
    PrometheusAlert[] Alerts
);

internal record PrometheusAlert(
    string Status, // "firing" or "resolved"
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyDictionary<string, string> Annotations,
    DateTime StartsAt,
    DateTime? EndsAt
);

// ADMIN-01: Prompt Management DTOs - See PromptManagementDto.cs for full definitions
