using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

public class GetPromptAuditLogQueryHandler : IQueryHandler<GetPromptAuditLogQuery, PromptAuditLogResponse>
{
    private readonly MeepleAiDbContext _db;

    public GetPromptAuditLogQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PromptAuditLogResponse> Handle(GetPromptAuditLogQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.TemplateId, out var templateGuid))
        {
            throw new ArgumentException($"Invalid template ID: {request.TemplateId}");
        }

        if (request.Limit <= 0 || request.Limit > 1000)
        {
            throw new ArgumentException("Limit must be between 1 and 1000");
        }

        var template = await _db.PromptTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateGuid, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template not found: {request.TemplateId}");
        }

        var auditEntries = await _db.PromptAuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.TemplateId == templateGuid)
            .OrderByDescending(a => a.Timestamp)
            .Take(request.Limit)
            .Select(a => new PromptAuditEntryDto
            {
                Id = a.Id.ToString(),
                TemplateId = a.TemplateId.ToString(),
                VersionId = a.VersionId?.ToString(),
                Action = a.Action,
                Details = a.Details,
                Timestamp = a.Timestamp,
                UserId = a.UserId.ToString(),
                UserEmail = a.User.Email
            })
            .ToListAsync(cancellationToken);

        return new PromptAuditLogResponse
        {
            TemplateId = template.Id.ToString(),
            TemplateName = template.Name,
            AuditEntries = auditEntries
        };
    }
}
