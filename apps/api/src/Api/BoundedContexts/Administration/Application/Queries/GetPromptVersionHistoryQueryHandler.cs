using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

public class GetPromptVersionHistoryQueryHandler : IQueryHandler<GetPromptVersionHistoryQuery, PromptVersionHistoryResponse>
{
    private readonly MeepleAiDbContext _db;

    public GetPromptVersionHistoryQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PromptVersionHistoryResponse> Handle(GetPromptVersionHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.TemplateId, out var templateGuid))
        {
            throw new ArgumentException($"Invalid template ID: {request.TemplateId}");
        }

        var template = await _db.PromptTemplates
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == templateGuid, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template not found: {request.TemplateId}");
        }

        var versions = await _db.PromptVersions
            .AsNoTracking()
            .Include(v => v.CreatedBy)
            .Include(v => v.Template)
            .Where(v => v.TemplateId == templateGuid)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new PromptVersionDto
            {
                Id = v.Id.ToString(),
                TemplateId = v.TemplateId.ToString(),
                TemplateName = v.Template.Name,
                VersionNumber = v.VersionNumber,
                Content = v.Content,
                ChangeNotes = v.ChangeNotes,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                CreatedByUserId = v.CreatedByUserId.ToString(),
                CreatedByEmail = v.CreatedBy.Email,
                ActivatedAt = v.ActivatedAt,
                ActivatedByUserId = v.ActivatedByUserId.HasValue ? v.ActivatedByUserId.Value.ToString() : null,
                ActivationReason = v.ActivationReason
            })
            .ToListAsync(cancellationToken);

        return new PromptVersionHistoryResponse
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
                VersionCount = versions.Count,
                ActiveVersionNumber = versions.FirstOrDefault(v => v.IsActive)?.VersionNumber
            },
            Versions = versions,
            TotalCount = versions.Count
        };
    }
}
