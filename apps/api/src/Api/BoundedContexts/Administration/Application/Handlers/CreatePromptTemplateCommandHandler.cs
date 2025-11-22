using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handles creation of a new prompt template.
/// </summary>
public class CreatePromptTemplateCommandHandler : ICommandHandler<CreatePromptTemplateCommand, PromptTemplateDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CreatePromptTemplateCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<PromptTemplateDto> Handle(
        CreatePromptTemplateCommand command,
        CancellationToken cancellationToken)
    {
        // Verify template name is unique
        var exists = await _dbContext.Set<PromptTemplateEntity>()
            .AnyAsync(t => t.Name == command.Name, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Template with name '{command.Name}' already exists");
        }

        // Load user for navigation property
        var user = await _dbContext.Set<UserEntity>().FindAsync([command.CreatedByUserId], cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User {command.CreatedByUserId} not found");
        }

        // Create template
        var templateId = Guid.NewGuid();
        var template = new PromptTemplateEntity
        {
            Id = templateId,
            Name = command.Name,
            Description = command.Description,
            Category = command.Category,
            CreatedByUserId = command.CreatedByUserId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            CreatedBy = user
        };

        // Create initial version (version 1)
        var initialVersion = new PromptVersionEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            VersionNumber = 1,
            Content = command.InitialContent,
            IsActive = true, // First version is automatically active
            CreatedByUserId = command.CreatedByUserId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Metadata = command.Metadata,
            Template = template,
            CreatedBy = user
        };

        _dbContext.Set<PromptTemplateEntity>().Add(template);
        _dbContext.Set<PromptVersionEntity>().Add(initialVersion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PromptTemplateDto
        {
            Id = template.Id.ToString(),
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            CreatedByUserId = template.CreatedByUserId.ToString(),
            CreatedByEmail = user.Email,
            CreatedAt = template.CreatedAt,
            VersionCount = 1,
            ActiveVersionNumber = 1
        };
    }
}
