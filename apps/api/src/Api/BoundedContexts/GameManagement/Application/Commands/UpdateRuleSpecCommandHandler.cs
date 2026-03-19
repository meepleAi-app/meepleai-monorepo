using System.Globalization;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles updating rule specifications for a game.
/// Creates a new version of the RuleSpec.
/// </summary>
internal class UpdateRuleSpecCommandHandler : ICommandHandler<UpdateRuleSpecCommand, RuleSpecDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecVersioningDomainService _versioningService;
    private readonly IAiResponseCacheService _cache;
    private readonly AuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public UpdateRuleSpecCommandHandler(
        MeepleAiDbContext dbContext,
        RuleSpecVersioningDomainService versioningService,
        IAiResponseCacheService cache,
        AuditService auditService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _versioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RuleSpecDto> Handle(UpdateRuleSpecCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate game and user exist
        await ValidateGameAndUserAsync(command.GameId, command.UserId, cancellationToken).ConfigureAwait(false);

        // Optimistic concurrency check and get latest spec
        var latestSpec = await ValidateOptimisticConcurrencyAsync(command.GameId, command.ExpectedETag, cancellationToken).ConfigureAwait(false);

        // Determine version
        var version = await DetermineVersionAsync(command, cancellationToken).ConfigureAwait(false);

        // Create new RuleSpec version
        var specEntity = CreateRuleSpecEntity(command, version, latestSpec?.Id);

        _dbContext.RuleSpecs.Add(specEntity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Post-processing: cache invalidation and audit
        await PostProcessRuleSpecUpdateAsync(command, version, cancellationToken).ConfigureAwait(false);

        // Return DTO with ETag
        return CreateRuleSpecDto(specEntity);
    }

    private async Task ValidateGameAndUserAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var game = await _dbContext.Games
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken).ConfigureAwait(false);

        if (game is null)
        {
            throw new InvalidOperationException($"Game {gameId} not found");
        }

        var userExists = await _dbContext.Users
            .AnyAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);

        if (!userExists)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }
    }

    private async Task<RuleSpecEntity?> ValidateOptimisticConcurrencyAsync(Guid gameId, string? expectedETag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(expectedETag))
        {
            return null;
        }

        var latestSpec = await _dbContext.RuleSpecs
            .AsNoTracking()
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (latestSpec != null && latestSpec.RowVersion != null)
        {
            var currentETag = Convert.ToBase64String(latestSpec.RowVersion);
            if (!string.Equals(currentETag, expectedETag, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Conflict detected: RuleSpec has been modified by another user. " +
                    $"Expected version ETag {expectedETag} but found {currentETag}. " +
                    $"Please refresh and try again.");
            }
        }

        return latestSpec;
    }

    private async Task<string> DetermineVersionAsync(UpdateRuleSpecCommand command, CancellationToken cancellationToken)
    {
        var versionProvided = !string.IsNullOrWhiteSpace(command.Version);
        var version = command.Version?.Trim() ?? string.Empty;

        if (!versionProvided)
        {
            return await _versioningService.GenerateNextVersionAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        }

        var duplicate = await _versioningService.VersionExistsAsync(command.GameId, version, cancellationToken).ConfigureAwait(false);
        if (duplicate)
        {
            throw new InvalidOperationException($"Version {version} already exists for game {command.GameId}");
        }

        return version;
    }

    private RuleSpecEntity CreateRuleSpecEntity(UpdateRuleSpecCommand command, string version, Guid? parentVersionId)
    {
        var specEntity = new RuleSpecEntity
        {
            GameId = command.GameId,
            Version = version,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            CreatedByUserId = command.UserId,
            ParentVersionId = command.ParentVersionId ?? parentVersionId,
        };

        int sortOrder = 1;
        foreach (var atom in command.Atoms)
        {
            specEntity.Atoms.Add(new RuleAtomEntity
            {
                RuleSpec = specEntity,
                Key = atom.Id,
                Text = atom.Text,
                Section = atom.Section,
                PageNumber = int.TryParse(atom.Page, CultureInfo.InvariantCulture, out var page) ? page : null,
                LineNumber = int.TryParse(atom.Line, CultureInfo.InvariantCulture, out var line) ? line : null,
                SortOrder = sortOrder++,
            });
        }

        return specEntity;
    }

    private async Task PostProcessRuleSpecUpdateAsync(UpdateRuleSpecCommand command, string version, CancellationToken cancellationToken)
    {
        await _cache.InvalidateGameAsync(command.GameId.ToString(), cancellationToken).ConfigureAwait(false);

        await _auditService.LogAsync(
            command.UserId.ToString(),
            "UPDATE_RULESPEC",
            "RuleSpec",
            command.GameId.ToString(),
            "Success",
            $"Updated RuleSpec to version {version}",
            command.IpAddress,
            command.UserAgent,
            cancellationToken).ConfigureAwait(false);
    }

    private static RuleSpecDto CreateRuleSpecDto(RuleSpecEntity specEntity)
    {
        var etag = specEntity.RowVersion != null
            ? Convert.ToBase64String(specEntity.RowVersion)
            : null;

        return new RuleSpecDto(
            Id: specEntity.Id,
            GameId: specEntity.GameId,
            Version: specEntity.Version,
            CreatedAt: specEntity.CreatedAt,
            CreatedByUserId: specEntity.CreatedByUserId,
            ParentVersionId: specEntity.ParentVersionId,
            Atoms: specEntity.Atoms
                .OrderBy(a => a.SortOrder)
                .Select(a => new RuleAtomDto(
                    Id: a.Key,
                    Text: a.Text,
                    Section: a.Section,
                    Page: a.PageNumber?.ToString(CultureInfo.InvariantCulture),
                    Line: a.LineNumber?.ToString(CultureInfo.InvariantCulture)
                ))
                .ToList(),
            ETag: etag
        );
    }
}
