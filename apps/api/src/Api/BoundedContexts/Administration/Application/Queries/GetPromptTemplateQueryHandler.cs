using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

public class GetPromptTemplateQueryHandler : IQueryHandler<GetPromptTemplateQuery, PromptTemplateDto?>
{
    private readonly MeepleAiDbContext _db;

    public GetPromptTemplateQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PromptTemplateDto?> Handle(GetPromptTemplateQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.TemplateId, out var templateGuid))
        {
            return null;
        }

        var template = await _db.PromptTemplates
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == templateGuid, cancellationToken).ConfigureAwait(false);

        if (template == null)
        {
            return null;
        }

        return new PromptTemplateDto
        {
            Id = template.Id.ToString(),
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            CreatedAt = template.CreatedAt,
            CreatedByUserId = template.CreatedByUserId.ToString(),
            CreatedByEmail = template.CreatedBy.Email
        };
    }
}
