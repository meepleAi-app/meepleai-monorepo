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
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == templateGuid, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template not found: {request.TemplateId}");
        }

        var auditEntries = await _db.PromptAuditLogs
            .AsNoTracking()
            .Include(a => a.ChangedBy)
            .Include(a => a.Template)
            .Where(a => a.TemplateId == templateGuid)
            .OrderByDescending(a => a.ChangedAt)
            .Take(request.Limit)
            .Select(a => new PromptAuditLogDto
            {
                Id = a.Id.ToString(),
                TemplateId = a.TemplateId.ToString(),
                TemplateName = a.Template.Name,
                VersionId = a.VersionId.HasValue ? a.VersionId.Value.ToString() : null,
                VersionNumber = null, // Not available in this query
                Action = a.Action,
                ChangedByUserId = a.ChangedByUserId.ToString(),
                ChangedByEmail = a.ChangedBy.Email,
                ChangedAt = a.ChangedAt,
                Details = a.Details
            })
            .ToListAsync(cancellationToken);

        return new PromptAuditLogResponse
        {
            Template = new PromptTemplateDto
            {
                Id = template.Id.ToString(),
                Name = template.Name,
                Description = template.Description,
                Category = template.Category,
                CreatedByUserId = template.CreatedByUserId.ToString(),
                CreatedByEmail = template.CreatedBy.Email,
                CreatedAt = template.CreatedAt,
                VersionCount = 0, // Not calculated in this query
                ActiveVersionNumber = null
            },
            Logs = auditEntries,
            TotalCount = auditEntries.Count
        };
    }
}
