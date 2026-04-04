using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handles retrieval of all versions for a prompt template.
/// </summary>
internal class GetPromptVersionsQueryHandler : IQueryHandler<GetPromptVersionsQuery, IReadOnlyList<PromptVersionDto>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetPromptVersionsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<PromptVersionDto>> Handle(
        GetPromptVersionsQuery query,
        CancellationToken cancellationToken)
    {
        // Verify template exists
        var templateExists = await _dbContext.Set<PromptTemplateEntity>()
            .AsNoTracking()
            .AnyAsync(t => t.Id == query.TemplateId, cancellationToken).ConfigureAwait(false);

        if (!templateExists)
        {
            throw new InvalidOperationException($"Prompt template {query.TemplateId} not found");
        }

        // Get versions
        var versions = await _dbContext.Set<PromptVersionEntity>()
            .AsNoTracking()
            .Include(v => v.CreatedBy)
            .Where(v => v.TemplateId == query.TemplateId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new PromptVersionDto
            {
                Id = v.Id.ToString(),
                TemplateId = v.TemplateId.ToString(),
                VersionNumber = v.VersionNumber,
                Content = v.Content,
                IsActive = v.IsActive,
                CreatedByUserId = v.CreatedByUserId.ToString(),
                CreatedByEmail = v.CreatedBy.Email,
                CreatedAt = v.CreatedAt,
                Metadata = v.Metadata
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return versions;
    }
}
