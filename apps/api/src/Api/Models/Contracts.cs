namespace Api.Models;

public record QaRequest(string gameId, string query);
public record QaResponse(string answer, IReadOnlyList<Snippet> snippets);
public record Snippet(string text, string source, int page, int line);

public record IngestPdfResponse(string jobId);
public record SeedRequest(string gameId);
public record AgentFeedbackRequest(string messageId, string endpoint, string? outcome, string userId, string? gameId);

// AI-02: RAG Explain models
public record ExplainRequest(string gameId, string topic);
public record ExplainResponse(
    ExplainOutline outline,
    string script,
    IReadOnlyList<Snippet> citations,
    int estimatedReadingTimeMinutes
);
public record ExplainOutline(
    string mainTopic,
    IReadOnlyList<string> sections
);

// AI-03: RAG Setup Guide models
public record SetupGuideRequest(string gameId);
public record SetupGuideResponse(
    string gameTitle,
    IReadOnlyList<SetupGuideStep> steps,
    int estimatedSetupTimeMinutes
);
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
