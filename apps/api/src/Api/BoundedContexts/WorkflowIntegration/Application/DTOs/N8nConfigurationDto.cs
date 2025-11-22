namespace Api.BoundedContexts.WorkflowIntegration.Application.DTOs;

public record N8nConfigurationDto(
    Guid Id,
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
    string? WebhookUrl = null
);

public record WorkflowErrorLogDto(
    Guid Id,
    string WorkflowId,
    string ExecutionId,
    string ErrorMessage,
    string? NodeName,
    int RetryCount,
    DateTime CreatedAt
);
