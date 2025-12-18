using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

internal class ActivatePromptVersionCommandHandler : ICommandHandler<ActivatePromptVersionCommand, PromptVersionDto>
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
        ArgumentNullException.ThrowIfNull(command);

        // Validate inputs
        ValidateCommand(command);

        // Load and validate version
        var versionToActivate = await LoadAndValidateVersionAsync(command.TemplateId, command.VersionId, cancellationToken).ConfigureAwait(false);

        // If already active, return immediately
        if (versionToActivate.IsActive)
        {
            _logger.LogInformation(
                "Version {VersionId} is already active for template {TemplateId}",
                command.VersionId, command.TemplateId);
            return MapToVersionDto(versionToActivate);
        }

        // Execute activation in transaction
        await ExecuteActivationTransactionAsync(command, versionToActivate, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Activated version {VersionNumber} ({VersionId}) for template {TemplateId} by user {UserId}",
            versionToActivate.VersionNumber, command.VersionId, command.TemplateId, command.ActivatedByUserId);

        return MapToVersionDto(versionToActivate);
    }

    private static void ValidateCommand(ActivatePromptVersionCommand command)
    {
        if (command.TemplateId == Guid.Empty)
        {
            throw new ArgumentException("Template ID is required", nameof(command));
        }

        if (command.VersionId == Guid.Empty)
        {
            throw new ArgumentException("Version ID is required", nameof(command));
        }

        if (command.ActivatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Activated by user ID is required", nameof(command));
        }
    }

    private async Task<PromptVersionEntity> LoadAndValidateVersionAsync(Guid templateId, Guid versionId, CancellationToken cancellationToken)
    {
        var version = await _db.PromptVersions
            .Include(v => v.Template)
            .Include(v => v.CreatedBy)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == templateId, cancellationToken).ConfigureAwait(false);

        if (version == null)
        {
            throw new InvalidOperationException(
                $"Version with ID '{versionId}' not found for template '{templateId}'");
        }

        return version;
    }

    private async Task ExecuteActivationTransactionAsync(
        ActivatePromptVersionCommand command,
        PromptVersionEntity versionToActivate,
        CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Deactivate other versions
            await DeactivateOtherVersionsAsync(command.TemplateId, command.VersionId, command.ActivatedByUserId, versionToActivate.VersionNumber, now, cancellationToken).ConfigureAwait(false);

            // Activate target version
            ActivateVersion(versionToActivate, command.ActivatedByUserId, command.Reason, now);

            // Create activation audit log
            CreateActivationAuditLog(command.TemplateId, command.VersionId, command.ActivatedByUserId, versionToActivate.VersionNumber, command.Reason, now);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // TRANSACTION BOUNDARY PATTERN: Log activation failure before rolling back and rethrowing.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "Prompt version activation failed. Template: {TemplateId}, Version: {VersionId}, User: {UserId}, Reason: {Reason}",
                command.TemplateId,
                command.VersionId,
                command.ActivatedByUserId,
                command.Reason ?? "Manual activation");

            await RollbackTransactionSafelyAsync(transaction, command.TemplateId, command.VersionId, command.ActivatedByUserId, cancellationToken).ConfigureAwait(false);

            throw; // Re-throw original exception
        }
#pragma warning restore S2139
    }

    private async Task DeactivateOtherVersionsAsync(
        Guid templateId,
        Guid versionId,
        Guid activatedByUserId,
        int activatingVersionNumber,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var otherVersions = await _db.PromptVersions
            .Where(v => v.TemplateId == templateId && v.Id != versionId && v.IsActive)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var otherVersion in otherVersions)
        {
            otherVersion.IsActive = false;

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
                    reason = $"Deactivated due to activation of version {activatingVersionNumber}"
                }),
                Template = null!, // Will be loaded by EF
                ChangedBy = null! // Will be loaded by EF
            };

            _db.PromptAuditLogs.Add(deactivationAuditLog);
        }
    }

    private static void ActivateVersion(PromptVersionEntity version, Guid activatedByUserId, string? reason, DateTime now)
    {
        version.IsActive = true;
        version.ActivatedAt = now;
        version.ActivatedByUserId = activatedByUserId;
        version.ActivationReason = reason ?? "Manual activation";
    }

    private void CreateActivationAuditLog(Guid templateId, Guid versionId, Guid activatedByUserId, int versionNumber, string? reason, DateTime now)
    {
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
                versionNumber,
                reason = reason ?? "Manual activation"
            }),
            Template = null!, // Will be loaded by EF
            ChangedBy = null! // Will be loaded by EF
        };

        _db.PromptAuditLogs.Add(activationAuditLog);
    }

    private async Task RollbackTransactionSafelyAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Guid templateId,
        Guid versionId,
        Guid activatedByUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Cleanup operation - Transaction rollback must not throw;
        // log rollback failure but preserve original exception for caller
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
