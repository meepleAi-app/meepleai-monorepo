using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

public class ActivatePromptVersionCommandHandler : ICommandHandler<ActivatePromptVersionCommand, PromptVersionDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ActivatePromptVersionCommandHandler> _logger;

    public ActivatePromptVersionCommandHandler(
        MeepleAiDbContext db,
        ILogger<ActivatePromptVersionCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PromptVersionDto> Handle(ActivatePromptVersionCommand command, CancellationToken cancellationToken)
    {
        var templateId = command.TemplateId;
        var versionId = command.VersionId;
        var activatedByUserId = command.ActivatedByUserId;
        var reason = command.Reason;

        if (templateId == Guid.Empty)
        {
            throw new ArgumentException("Template ID is required", nameof(templateId));
        }

        if (versionId == Guid.Empty)
        {
            throw new ArgumentException("Version ID is required", nameof(versionId));
        }

        if (activatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Activated by user ID is required", nameof(activatedByUserId));
        }

        // Check if version exists and belongs to the specified template
        var versionToActivate = await _db.PromptVersions
            .Include(v => v.Template)
            .Include(v => v.CreatedBy)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == templateId, cancellationToken);

        if (versionToActivate == null)
        {
            throw new InvalidOperationException(
                $"Version with ID '{versionId}' not found for template '{templateId}'");
        }

        if (versionToActivate.IsActive)
        {
            _logger.LogInformation(
                "Version {VersionId} is already active for template {TemplateId}",
                versionId, templateId);
            return MapToVersionDto(versionToActivate);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Start transaction for activation
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Deactivate all other versions of the same template
            var otherVersions = await _db.PromptVersions
                .Where(v => v.TemplateId == templateId && v.Id != versionId && v.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var otherVersion in otherVersions)
            {
                otherVersion.IsActive = false;

                // Create deactivation audit log
                var deactivationAuditLog = new PromptAuditLogEntity
                {
                    Id = Guid.NewGuid(),
                    TemplateId = templateId,
                    VersionId = otherVersion.Id,
                    Action = "version_deactivated",
                    ChangedByUserId = activatedByUserId,
                    ChangedAt = now,
                    Details = JsonSerializer.Serialize(new
                    {
                        versionNumber = otherVersion.VersionNumber,
                        reason = $"Deactivated due to activation of version {versionToActivate.VersionNumber}"
                    }),
                    Template = null!, // Will be loaded by EF
                    ChangedBy = null! // Will be loaded by EF
                };

                _db.PromptAuditLogs.Add(deactivationAuditLog);
            }

            // Activate the target version
            versionToActivate.IsActive = true;
            versionToActivate.ActivatedAt = now;
            versionToActivate.ActivatedByUserId = activatedByUserId;
            versionToActivate.ActivationReason = reason ?? "Manual activation";

            // Create activation audit log
            var activationAuditLog = new PromptAuditLogEntity
            {
                Id = Guid.NewGuid(),
                TemplateId = templateId,
                VersionId = versionId,
                Action = "version_activated",
                ChangedByUserId = activatedByUserId,
                ChangedAt = now,
                Details = JsonSerializer.Serialize(new
                {
                    versionNumber = versionToActivate.VersionNumber,
                    reason = reason ?? "Manual activation"
                }),
                Template = null!, // Will be loaded by EF
                ChangedBy = null! // Will be loaded by EF
            };

            _db.PromptAuditLogs.Add(activationAuditLog);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Activated version {VersionNumber} ({VersionId}) for template {TemplateId} by user {UserId}",
                versionToActivate.VersionNumber, versionId, templateId, activatedByUserId);

            return MapToVersionDto(versionToActivate);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "Prompt version activation failed. Template: {TemplateId}, Version: {VersionId}, User: {UserId}, Reason: {Reason}",
                templateId,
                versionId,
                activatedByUserId,
                reason ?? "Manual activation");

            try
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception rollbackEx)
#pragma warning restore CA1031
            {
                _logger.LogError(
                    rollbackEx,
                    "Failed to rollback transaction after prompt version activation error. Template: {TemplateId}, Version: {VersionId}, User: {UserId}",
                    templateId,
                    versionId,
                    activatedByUserId);
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
