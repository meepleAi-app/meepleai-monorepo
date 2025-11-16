using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handles creation of a new version for an existing prompt template.
/// </summary>
public class CreatePromptVersionCommandHandler : ICommandHandler<CreatePromptVersionCommand, PromptVersionDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CreatePromptVersionCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<PromptVersionDto> Handle(
        CreatePromptVersionCommand command,
        CancellationToken cancellationToken)
    {
        // Load template with versions
        var template = await _dbContext.Set<PromptTemplateEntity>()
            .Include(t => t.Versions)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == command.TemplateId, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template {command.TemplateId} not found");
        }

        // Load user for navigation property
        var user = await _dbContext.Set<UserEntity>().FindAsync([command.CreatedByUserId], cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User {command.CreatedByUserId} not found");
        }

        // Calculate next version number
        var nextVersionNumber = template.Versions.Any()
            ? template.Versions.Max(v => v.VersionNumber) + 1
            : 1;

        // Create version
        var version = new PromptVersionEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = command.TemplateId,
            VersionNumber = nextVersionNumber,
            Content = command.Content,
            IsActive = false, // New versions are inactive by default
            CreatedByUserId = command.CreatedByUserId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Metadata = command.Metadata,
            Template = template,
            CreatedBy = user
        };

        _dbContext.Set<PromptVersionEntity>().Add(version);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PromptVersionDto
        {
            Id = version.Id.ToString(),
            TemplateId = version.TemplateId.ToString(),
            VersionNumber = version.VersionNumber,
            Content = version.Content,
            IsActive = version.IsActive,
            CreatedByUserId = version.CreatedByUserId.ToString(),
            CreatedByEmail = user.Email,
            CreatedAt = version.CreatedAt,
            Metadata = version.Metadata
        };
    }
}
