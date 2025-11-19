using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handles retrieval of paginated prompt templates with optional filtering.
/// </summary>
public class GetPromptTemplatesQueryHandler
    : IQueryHandler<GetPromptTemplatesQuery, (IReadOnlyList<PromptTemplateDto> Templates, int TotalCount)>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetPromptTemplatesQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<PromptTemplateDto> Templates, int TotalCount)> Handle(
        GetPromptTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.Set<PromptTemplateEntity>()
            .AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.Versions)
            .AsQueryable();

        // Apply category filter if provided
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            dbQuery = dbQuery.Where(t => t.Category == query.Category);
        }

        // Get total count
        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Get paginated results
        var templates = await dbQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(t => new PromptTemplateDto
            {
                Id = t.Id.ToString(),
                Name = t.Name,
                Description = t.Description,
                Category = t.Category,
                CreatedByUserId = t.CreatedByUserId.ToString(),
                CreatedByEmail = t.CreatedBy.Email,
                CreatedAt = t.CreatedAt,
                VersionCount = t.Versions.Count,
                ActiveVersionNumber = t.Versions
                    .Where(v => v.IsActive)
                    .Select(v => (int?)v.VersionNumber)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return (templates, totalCount);
    }
}
