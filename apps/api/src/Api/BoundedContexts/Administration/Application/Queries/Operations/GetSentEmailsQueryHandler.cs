using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Operations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Operations;

/// <summary>
/// Handler for GetSentEmailsQuery.
/// Issue #3696: Operations - Service Control Panel.
/// Retrieves sent email records from audit logs for monitoring purposes.
/// </summary>
internal sealed class GetSentEmailsQueryHandler
    : IRequestHandler<GetSentEmailsQuery, SentEmailsResponseDto>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<GetSentEmailsQueryHandler> _logger;

    public GetSentEmailsQueryHandler(
        IAuditLogRepository auditLogRepository,
        ILogger<GetSentEmailsQueryHandler> logger)
    {
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SentEmailsResponseDto> Handle(
        GetSentEmailsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Query audit logs for email_sent actions
        var auditLogs = await _auditLogRepository.GetEmailSentLogsAsync(
            limit: query.Limit,
            offset: query.Offset,
            startDate: query.StartDate,
            endDate: query.EndDate,
            status: query.Status,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        var total = await _auditLogRepository.CountEmailSentLogsAsync(
            startDate: query.StartDate,
            endDate: query.EndDate,
            status: query.Status,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        var emails = auditLogs.Select(log => new SentEmailDto(
            Id: log.Id,
            To: ExtractEmailRecipient(log.Details),
            Subject: ExtractEmailSubject(log.Details),
            Preview: ExtractEmailPreview(log.Details),
            SentAt: log.CreatedAt,
            Status: log.Result,
            ErrorMessage: string.Equals(log.Result, "failed", StringComparison.OrdinalIgnoreCase)
                ? ExtractErrorMessage(log.Details)
                : null
        )).ToList();

        _logger.LogInformation(
            "Retrieved {EmailCount} sent emails (total: {Total}, offset: {Offset})",
            emails.Count, total, query.Offset);

        return new SentEmailsResponseDto(
            Emails: emails,
            Total: total,
            Limit: query.Limit,
            Offset: query.Offset
        );
    }

    private static string ExtractEmailRecipient(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return "Unknown";

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("to", out var toElement))
            {
                return toElement.GetString() ?? "Unknown";
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return "Unknown";
    }

    private static string ExtractEmailSubject(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return "No subject";

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("subject", out var subjectElement))
            {
                return subjectElement.GetString() ?? "No subject";
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return "No subject";
    }

    private static string? ExtractEmailPreview(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("preview", out var previewElement))
            {
                var preview = previewElement.GetString();
                return preview?.Length > 100 ? preview[..100] + "..." : preview;
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return null;
    }

    private static string? ExtractErrorMessage(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                return errorElement.GetString();
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return null;
    }
}
