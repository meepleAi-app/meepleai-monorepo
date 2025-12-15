

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.WorkflowIntegration.Application.DTOs;

internal record N8NConfigurationDto(
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

internal record CreateN8NConfigRequest(
    string Name,
    string BaseUrl,
    string ApiKey,
    string? WebhookUrl = null
);

internal record WorkflowErrorLogDto(
    Guid Id,
    string WorkflowId,
    string ExecutionId,
    string ErrorMessage,
    string? NodeName,
    int RetryCount,
    DateTime CreatedAt
);
