using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands;

public class CreatePromptVersionCommandHandler : ICommandHandler<CreatePromptVersionCommand, PromptVersionDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreatePromptVersionCommandHandler> _logger;

    public CreatePromptVersionCommandHandler(
        MeepleAiDbContext db,
        ILogger<CreatePromptVersionCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PromptVersionDto> Handle(CreatePromptVersionCommand command, CancellationToken cancellationToken)
    {
        var templateId = command.TemplateId;
        var request = command.Request;
        var createdByUserId = command.CreatedByUserId;

        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Template ID is required", nameof(templateId));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Content is required", nameof(request));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Created by user ID is required", nameof(createdByUserId));
        }

        // Check if template exists
        var template = await _db.PromptTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id.ToString() == templateId, cancellationToken);

        if (template == null)
        {
            throw new InvalidOperationException($"Template with ID '{templateId}' not found");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Calculate next version number
        var maxVersionNumber = template.Versions.Any()
            ? template.Versions.Max(v => v.VersionNumber)
            : 0;
        var nextVersionNumber = maxVersionNumber + 1;

        var versionId = Guid.NewGuid();

        // Start transaction for version creation and optional activation
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Create new version
            var version = new PromptVersionEntity
            {
                Id = versionId,
                TemplateId = Guid.Parse(templateId),
                VersionNumber = nextVersionNumber,
                Content = request.Content,
                ChangeNotes = request.ChangeNotes,
                IsActive = request.ActivateImmediately,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                Metadata = request.Metadata,
                Template = null!, // Will be loaded by EF
                CreatedBy = null! // Will be loaded by EF
            };

            _db.PromptVersions.Add(version);

            // Create version creation audit log
            var versionAuditLog = new PromptAuditLogEntity
            {
                Id = Guid.NewGuid(),
                TemplateId = Guid.Parse(templateId),
                VersionId = versionId,
                Action = "version_created",
                ChangedByUserId = createdByUserId,
                ChangedAt = now,
                Details = JsonSerializer.Serialize(new
                {
                    versionNumber = nextVersionNumber,
                    isActive = request.ActivateImmediately,
                    contentLength = request.Content.Length
                }),
                Template = null!, // Will be loaded by EF
                ChangedBy = null! // Will be loaded by EF
            };

            _db.PromptAuditLogs.Add(versionAuditLog);

            // If immediate activation requested, deactivate all other versions
            if (request.ActivateImmediately)
            {
                var otherVersions = await _db.PromptVersions
                    .Where(v => v.TemplateId == Guid.Parse(templateId) && v.Id != versionId && v.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var otherVersion in otherVersions)
                {
                    otherVersion.IsActive = false;

                    // Create deactivation audit log
                    var deactivationAuditLog = new PromptAuditLogEntity
                    {
                        Id = Guid.NewGuid(),
                        TemplateId = Guid.Parse(templateId),
                        VersionId = otherVersion.Id,
                        Action = "version_deactivated",
                        ChangedByUserId = createdByUserId,
                        ChangedAt = now,
                        Details = JsonSerializer.Serialize(new
                        {
                            versionNumber = otherVersion.VersionNumber,
                            reason = $"Deactivated due to activation of version {nextVersionNumber}"
                        }),
                        Template = null!, // Will be loaded by EF
                        ChangedBy = null! // Will be loaded by EF
                    };

                    _db.PromptAuditLogs.Add(deactivationAuditLog);
                }

                // Create activation audit log for new version
                var activationAuditLog = new PromptAuditLogEntity
                {
                    Id = Guid.NewGuid(),
                    TemplateId = Guid.Parse(templateId),
                    VersionId = versionId,
                    Action = "version_activated",
                    ChangedByUserId = createdByUserId,
                    ChangedAt = now,
                    Details = JsonSerializer.Serialize(new
                    {
                        versionNumber = nextVersionNumber,
                        reason = "Activated immediately upon creation"
                    }),
                    Template = null!, // Will be loaded by EF
                    ChangedBy = null! // Will be loaded by EF
                };

                _db.PromptAuditLogs.Add(activationAuditLog);

                version.ActivatedAt = now;
                version.ActivatedByUserId = createdByUserId;
                version.ActivationReason = "Activated immediately upon creation";
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Created version {VersionNumber} ({VersionId}) for template {TemplateId} by user {UserId}. Activated immediately: {ActivatedImmediately}",
                nextVersionNumber, versionId, templateId, createdByUserId, request.ActivateImmediately);

            // Load created version with navigation properties
            var createdVersion = await _db.PromptVersions
                .Include(v => v.Template)
                .Include(v => v.CreatedBy)
                .FirstAsync(v => v.Id == versionId, cancellationToken);

            return MapToVersionDto(createdVersion);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "Prompt version creation failed for template {TemplateId} by user {UserId}. Version number: {VersionNumber}, Activate immediately: {ActivateImmediately}",
                templateId,
                createdByUserId,
                nextVersionNumber,
                request.ActivateImmediately);

            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception rollbackEx)
#pragma warning restore CA1031
            {
                _logger.LogError(
                    rollbackEx,
                    "Failed to rollback transaction after prompt version creation error. Template: {TemplateId}, User: {UserId}",
                    templateId,
                    createdByUserId);
            }

            throw; // Re-throw original exception
        }
    }

    private PromptVersionDto MapToVersionDto(PromptVersionEntity entity)
    {
        return new PromptVersionDto
        {
            Id = entity.Id.ToString(),
            TemplateId = entity.TemplateId.ToString(),
            TemplateName = entity.Template.Name,
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
