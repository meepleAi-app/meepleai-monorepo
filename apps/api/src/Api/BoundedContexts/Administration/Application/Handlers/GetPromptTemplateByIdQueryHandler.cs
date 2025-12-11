using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handles retrieval of a single prompt template by ID.
/// </summary>
public class GetPromptTemplateByIdQueryHandler : IQueryHandler<GetPromptTemplateByIdQuery, PromptTemplateDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetPromptTemplateByIdQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PromptTemplateDto?> Handle(
        GetPromptTemplateByIdQuery query,
        CancellationToken cancellationToken)
    {
        var template = await _dbContext.Set<PromptTemplateEntity>()
            .AsNoTracking()
            .Include(t => t.Versions)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == query.TemplateId, cancellationToken).ConfigureAwait(false);

        if (template == null)
        {
            return null;
        }

        if (template.CreatedBy == null)
        {
            throw new InvalidOperationException($"Template {query.TemplateId} has no creator information");
        }

        return new PromptTemplateDto
        {
            Id = template.Id.ToString(),
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            CreatedByUserId = template.CreatedByUserId.ToString(),
            CreatedByEmail = template.CreatedBy.Email,
            CreatedAt = template.CreatedAt,
            VersionCount = template.Versions.Count,
            ActiveVersionNumber = template.Versions
                .FirstOrDefault(v => v.IsActive)?.VersionNumber
        };
    }
}
