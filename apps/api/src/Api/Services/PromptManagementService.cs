using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IPromptManagementService
{
    /// <summary>
    /// Creates a new prompt template with an initial version.
    /// </summary>
    Task<CreatePromptTemplateResponse> CreatePromptTemplateAsync(
        CreatePromptTemplateRequest request,
        string createdByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new version of an existing prompt template.
    /// </summary>
    Task<PromptVersionDto> CreatePromptVersionAsync(
        string templateId,
        CreatePromptVersionRequest request,
        string createdByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the currently active version of a prompt template by template name.
    /// Returns null if no active version exists.
    /// </summary>
    Task<PromptVersionDto?> GetActiveVersionAsync(
        string templateName,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a specific version of a prompt template by template ID and version number.
    /// </summary>
    Task<PromptVersionDto?> GetVersionAsync(
        string templateId,
        int versionNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Activates a specific version of a prompt template.
    /// Deactivates all other versions of the same template.
    /// </summary>
    Task<PromptVersionDto> ActivateVersionAsync(
        string templateId,
        string versionId,
        string activatedByUserId,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the version history for a prompt template.
    /// </summary>
    Task<PromptVersionHistoryResponse> GetVersionHistoryAsync(
        string templateId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the audit log for a prompt template.
    /// </summary>
    Task<PromptAuditLogResponse> GetAuditLogAsync(
        string templateId,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all prompt templates with optional category filter.
    /// </summary>
    Task<PromptTemplateListResponse> ListTemplatesAsync(
        string? category = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a specific prompt template by ID.
    /// </summary>
    Task<PromptTemplateDto?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default);
}

public class PromptManagementService : IPromptManagementService
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PromptManagementService> _logger;

    public PromptManagementService(
        MeepleAiDbContext db,
        ILogger<PromptManagementService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CreatePromptTemplateResponse> CreatePromptTemplateAsync(
        CreatePromptTemplateRequest request,
        string createdByUserId,
        CancellationToken ct = default)
    {
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

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("Created by user ID is required", nameof(createdByUserId));
        }

        // Check if template with same name already exists
        var existingTemplate = await _db.PromptTemplates
            .FirstOrDefaultAsync(t => t.Name == request.Name, ct);

        if (existingTemplate != null)
        {
            throw new InvalidOperationException($"A template with name '{request.Name}' already exists");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var templateId = Guid.NewGuid().ToString();
        var versionId = Guid.NewGuid().ToString();

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
            Id = Guid.NewGuid().ToString(),
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
            Id = Guid.NewGuid().ToString(),
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

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created prompt template {TemplateId} ({TemplateName}) with initial version {VersionId} by user {UserId}",
            templateId, request.Name, versionId, createdByUserId);

        // Load created entities with navigation properties
        var createdTemplate = await _db.PromptTemplates
            .Include(t => t.CreatedBy)
            .Include(t => t.Versions)
            .FirstAsync(t => t.Id == templateId, ct);

        var createdVersion = await _db.PromptVersions
            .Include(v => v.CreatedBy)
            .FirstAsync(v => v.Id == versionId, ct);

        return new CreatePromptTemplateResponse
        {
            Template = MapToTemplateDto(createdTemplate),
            InitialVersion = MapToVersionDto(createdVersion)
        };
    }

    public async Task<PromptVersionDto> CreatePromptVersionAsync(
        string templateId,
        CreatePromptVersionRequest request,
        string createdByUserId,
        CancellationToken ct = default)
    {
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

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("Created by user ID is required", nameof(createdByUserId));
        }

        // Check if template exists
        var template = await _db.PromptTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

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

        var versionId = Guid.NewGuid().ToString();

        // Start transaction for version creation and optional activation
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // Create new version
            var version = new PromptVersionEntity
            {
                Id = versionId,
                TemplateId = templateId,
                VersionNumber = nextVersionNumber,
                Content = request.Content,
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
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
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
                    .Where(v => v.TemplateId == templateId && v.Id != versionId && v.IsActive)
                    .ToListAsync(ct);

                foreach (var otherVersion in otherVersions)
                {
                    otherVersion.IsActive = false;

                    // Create deactivation audit log
                    var deactivationAuditLog = new PromptAuditLogEntity
                    {
                        Id = Guid.NewGuid().ToString(),
                        TemplateId = templateId,
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
                    Id = Guid.NewGuid().ToString(),
                    TemplateId = templateId,
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
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Created version {VersionNumber} for template {TemplateId} by user {UserId} (activated: {IsActive})",
                nextVersionNumber, templateId, createdByUserId, request.ActivateImmediately);

            // Load created version with navigation properties
            var createdVersion = await _db.PromptVersions
                .Include(v => v.CreatedBy)
                .FirstAsync(v => v.Id == versionId, ct);

            return MapToVersionDto(createdVersion);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PromptVersionDto?> GetActiveVersionAsync(
        string templateName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new ArgumentException("Template name is required", nameof(templateName));
        }

        var activeVersion = await _db.PromptVersions
            .Include(v => v.Template)
            .Include(v => v.CreatedBy)
            .Where(v => v.Template.Name == templateName && v.IsActive)
            .FirstOrDefaultAsync(ct);

        return activeVersion != null ? MapToVersionDto(activeVersion) : null;
    }

    public async Task<PromptVersionDto?> GetVersionAsync(
        string templateId,
        int versionNumber,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Template ID is required", nameof(templateId));
        }

        if (versionNumber <= 0)
        {
            throw new ArgumentException("Version number must be positive", nameof(versionNumber));
        }

        var version = await _db.PromptVersions
            .Include(v => v.CreatedBy)
            .Where(v => v.TemplateId == templateId && v.VersionNumber == versionNumber)
            .FirstOrDefaultAsync(ct);

        return version != null ? MapToVersionDto(version) : null;
    }

    public async Task<PromptVersionDto> ActivateVersionAsync(
        string templateId,
        string versionId,
        string activatedByUserId,
        string? reason = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Template ID is required", nameof(templateId));
        }

        if (string.IsNullOrWhiteSpace(versionId))
        {
            throw new ArgumentException("Version ID is required", nameof(versionId));
        }

        if (string.IsNullOrWhiteSpace(activatedByUserId))
        {
            throw new ArgumentException("Activated by user ID is required", nameof(activatedByUserId));
        }

        // Check if version exists and belongs to the specified template
        var versionToActivate = await _db.PromptVersions
            .Include(v => v.CreatedBy)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == templateId, ct);

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
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // Deactivate all other versions of the same template
            var otherVersions = await _db.PromptVersions
                .Where(v => v.TemplateId == templateId && v.Id != versionId && v.IsActive)
                .ToListAsync(ct);

            foreach (var otherVersion in otherVersions)
            {
                otherVersion.IsActive = false;

                // Create deactivation audit log
                var deactivationAuditLog = new PromptAuditLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
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

            // Create activation audit log
            var activationAuditLog = new PromptAuditLogEntity
            {
                Id = Guid.NewGuid().ToString(),
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

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Activated version {VersionNumber} ({VersionId}) for template {TemplateId} by user {UserId}",
                versionToActivate.VersionNumber, versionId, templateId, activatedByUserId);

            return MapToVersionDto(versionToActivate);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PromptVersionHistoryResponse> GetVersionHistoryAsync(
        string templateId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Template ID is required", nameof(templateId));
        }

        var template = await _db.PromptTemplates
            .Include(t => t.CreatedBy)
            .Include(t => t.Versions.OrderByDescending(v => v.VersionNumber))
            .ThenInclude(v => v.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template == null)
        {
            throw new InvalidOperationException($"Template with ID '{templateId}' not found");
        }

        var versions = template.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(MapToVersionDto)
            .ToList();

        return new PromptVersionHistoryResponse
        {
            Template = MapToTemplateDto(template),
            Versions = versions,
            TotalCount = versions.Count
        };
    }

    public async Task<PromptAuditLogResponse> GetAuditLogAsync(
        string templateId,
        int limit = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Template ID is required", nameof(templateId));
        }

        if (limit <= 0 || limit > 1000)
        {
            throw new ArgumentException("Limit must be between 1 and 1000", nameof(limit));
        }

        var template = await _db.PromptTemplates
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template == null)
        {
            throw new InvalidOperationException($"Template with ID '{templateId}' not found");
        }

        var auditLogs = await _db.PromptAuditLogs
            .Include(a => a.ChangedBy)
            .Include(a => a.Template)
            .Include(a => a.Version)
            .Where(a => a.TemplateId == templateId)
            .OrderByDescending(a => a.ChangedAt)
            .Take(limit)
            .ToListAsync(ct);

        var totalCount = await _db.PromptAuditLogs
            .CountAsync(a => a.TemplateId == templateId, ct);

        return new PromptAuditLogResponse
        {
            Template = MapToTemplateDto(template),
            Logs = auditLogs.Select(MapToAuditLogDto).ToList(),
            TotalCount = totalCount
        };
    }

    public async Task<PromptTemplateListResponse> ListTemplatesAsync(
        string? category = null,
        CancellationToken ct = default)
    {
        var query = _db.PromptTemplates
            .Include(t => t.CreatedBy)
            .Include(t => t.Versions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category == category);
        }

        var templates = await query
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return new PromptTemplateListResponse
        {
            Templates = templates.Select(MapToTemplateDto).ToList(),
            TotalCount = templates.Count
        };
    }

    public async Task<PromptTemplateDto?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Template ID is required", nameof(templateId));
        }

        var template = await _db.PromptTemplates
            .Include(t => t.CreatedBy)
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        return template != null ? MapToTemplateDto(template) : null;
    }

    // Helper methods for mapping entities to DTOs
    private static PromptTemplateDto MapToTemplateDto(PromptTemplateEntity entity)
    {
        var activeVersion = entity.Versions.FirstOrDefault(v => v.IsActive);

        return new PromptTemplateDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByEmail = entity.CreatedBy?.Email,
            CreatedAt = entity.CreatedAt,
            VersionCount = entity.Versions.Count,
            ActiveVersionNumber = activeVersion?.VersionNumber
        };
    }

    private static PromptVersionDto MapToVersionDto(PromptVersionEntity entity)
    {
        return new PromptVersionDto
        {
            Id = entity.Id,
            TemplateId = entity.TemplateId,
            VersionNumber = entity.VersionNumber,
            Content = entity.Content,
            IsActive = entity.IsActive,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByEmail = entity.CreatedBy?.Email,
            CreatedAt = entity.CreatedAt,
            Metadata = entity.Metadata
        };
    }

    private static PromptAuditLogDto MapToAuditLogDto(PromptAuditLogEntity entity)
    {
        return new PromptAuditLogDto
        {
            Id = entity.Id,
            TemplateId = entity.TemplateId,
            TemplateName = entity.Template?.Name,
            VersionId = entity.VersionId,
            VersionNumber = entity.Version?.VersionNumber,
            Action = entity.Action,
            ChangedByUserId = entity.ChangedByUserId,
            ChangedByEmail = entity.ChangedBy?.Email,
            ChangedAt = entity.ChangedAt,
            Details = entity.Details
        };
    }
}
