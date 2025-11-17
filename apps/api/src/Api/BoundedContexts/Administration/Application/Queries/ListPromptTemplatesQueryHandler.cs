using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

public class ListPromptTemplatesQueryHandler : IQueryHandler<ListPromptTemplatesQuery, PromptTemplateListResponse>
{
    private readonly MeepleAiDbContext _db;

    public ListPromptTemplatesQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PromptTemplateListResponse> Handle(ListPromptTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.PromptTemplates
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(t => t.Category == request.Category);
        }

        var templates = await query
            .OrderBy(t => t.Name)
            .Select(t => new PromptTemplateDto
            {
                Id = t.Id.ToString(),
                Name = t.Name,
                Description = t.Description,
                Category = t.Category,
                CreatedAt = t.CreatedAt,
                CreatedByUserId = t.CreatedByUserId.ToString(),
                CreatedByEmail = t.CreatedBy.Email
            })
            .ToListAsync(cancellationToken);

        return new PromptTemplateListResponse { Templates = templates };
    }
}
