using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands;

public class CreatePromptTemplateCommandHandler : ICommandHandler<CreatePromptTemplateCommand, CreatePromptTemplateResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreatePromptTemplateCommandHandler> _logger;

    public CreatePromptTemplateCommandHandler(
        MeepleAiDbContext db,
        ILogger<CreatePromptTemplateCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CreatePromptTemplateResponse> Handle(CreatePromptTemplateCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var createdByUserId = command.CreatedByUserId;

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Template name is required", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.InitialContent))
        {
            throw new ArgumentException("Initial content is required", nameof(request));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Created by user ID is required", nameof(createdByUserId));
        }

        // Check if template with same name already exists
        var existingTemplate = await _db.PromptTemplates
            .FirstOrDefaultAsync(t => t.Name == request.Name, cancellationToken);

        if (existingTemplate != null)
        {
            throw new InvalidOperationException($"A template with name '{request.Name}' already exists");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        // Create template
        var template = new PromptTemplateEntity
        {
            Id = templateId,
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            CreatedBy = null! // Will be loaded by EF
        };

        // Create initial version (version 1)
        var version = new PromptVersionEntity
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Content = request.InitialContent,
            IsActive = true, // First version is automatically active
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            Metadata = request.Metadata,
            Template = null!, // Will be loaded by EF
            CreatedBy = null! // Will be loaded by EF
        };

        // Create audit log entries
        var templateAuditLog = new PromptAuditLogEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            VersionId = null,
            Action = "template_created",
            ChangedByUserId = createdByUserId,
            ChangedAt = now,
            Details = JsonSerializer.Serialize(new
            {
                name = request.Name,
                description = request.Description,
                category = request.Category
            }),
            Template = null!, // Will be loaded by EF
            ChangedBy = null! // Will be loaded by EF
        };

        var versionAuditLog = new PromptAuditLogEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            VersionId = versionId,
            Action = "version_created",
            ChangedByUserId = createdByUserId,
            ChangedAt = now,
            Details = JsonSerializer.Serialize(new
            {
                versionNumber = 1,
                isActive = true,
                contentLength = request.InitialContent.Length
            }),
            Template = null!, // Will be loaded by EF
            ChangedBy = null! // Will be loaded by EF
        };

        _db.PromptTemplates.Add(template);
        _db.PromptVersions.Add(version);
        _db.PromptAuditLogs.Add(templateAuditLog);
        _db.PromptAuditLogs.Add(versionAuditLog);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created prompt template {TemplateId} ({TemplateName}) with initial version {VersionId} by user {UserId}",
            templateId, request.Name, versionId, createdByUserId);

        // Load created entities with navigation properties
        var createdTemplate = await _db.PromptTemplates
            .Include(t => t.CreatedBy)
            .Include(t => t.Versions)
            .FirstAsync(t => t.Id == templateId, cancellationToken);

        var createdVersion = await _db.PromptVersions
            .Include(v => v.CreatedBy)
            .FirstAsync(v => v.Id == versionId, cancellationToken);

        return new CreatePromptTemplateResponse
        {
            Template = MapToTemplateDto(createdTemplate),
            InitialVersion = MapToVersionDto(createdVersion)
        };
    }

    private PromptTemplateDto MapToTemplateDto(PromptTemplateEntity entity)
    {
        return new PromptTemplateDto
        {
            Id = entity.Id.ToString(),
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            CreatedAt = entity.CreatedAt,
            CreatedByUserId = entity.CreatedByUserId.ToString(),
            CreatedByEmail = entity.CreatedBy.Email
        };
    }

    private PromptVersionDto MapToVersionDto(PromptVersionEntity entity)
    {
        return new PromptVersionDto
        {
            Id = entity.Id.ToString(),
            TemplateId = entity.TemplateId.ToString(),
            TemplateName = entity.Template?.Name ?? string.Empty,
            VersionNumber = entity.VersionNumber,
            Content = entity.Content,
            ChangeNotes = entity.ChangeNotes,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedByUserId = entity.CreatedByUserId.ToString(),
            CreatedByEmail = entity.CreatedBy.Email,
            ActivatedAt = entity.ActivatedAt,
            ActivatedByUserId = entity.ActivatedByUserId?.ToString(),
            ActivationReason = entity.ActivationReason
        };
    }
}
