using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal class GetActivePromptVersionQueryHandler : IQueryHandler<GetActivePromptVersionQuery, PromptVersionDto?>
{
    private readonly MeepleAiDbContext _db;

    public GetActivePromptVersionQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PromptVersionDto?> Handle(GetActivePromptVersionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var version = await _db.PromptVersions
            .AsNoTracking()
            .Include(v => v.Template)
            .Include(v => v.CreatedBy)
            .FirstOrDefaultAsync(v => v.Template.Name == request.TemplateName && v.IsActive, cancellationToken).ConfigureAwait(false);

        if (version == null)
        {
            return null;
        }

        return new PromptVersionDto
        {
            Id = version.Id.ToString(),
            TemplateId = version.TemplateId.ToString(),
            TemplateName = version.Template.Name,
            VersionNumber = version.VersionNumber,
            Content = version.Content,
            ChangeNotes = version.ChangeNotes,
            IsActive = version.IsActive,
            CreatedAt = version.CreatedAt,
            CreatedByUserId = version.CreatedByUserId.ToString(),
            CreatedByEmail = version.CreatedBy.Email,
            ActivatedAt = version.ActivatedAt,
            ActivatedByUserId = version.ActivatedByUserId?.ToString(),
            ActivationReason = version.ActivationReason
        };
    }
}
