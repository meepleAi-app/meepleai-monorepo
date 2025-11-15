using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles updating rule specifications for a game.
/// Creates a new version of the RuleSpec.
/// </summary>
public class UpdateRuleSpecCommandHandler : ICommandHandler<UpdateRuleSpecCommand, RuleSpecDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecVersioningDomainService _versioningService;
    private readonly IAiResponseCacheService _cache;
    private readonly TimeProvider _timeProvider;

    public UpdateRuleSpecCommandHandler(
        MeepleAiDbContext dbContext,
        RuleSpecVersioningDomainService versioningService,
        IAiResponseCacheService cache,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _versioningService = versioningService;
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<RuleSpecDto> Handle(UpdateRuleSpecCommand command, CancellationToken cancellationToken)
    {
        // Ensure game exists
        var game = await _dbContext.Games
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game is null)
        {
            throw new InvalidOperationException($"Game {command.GameId} not found");
        }

        // Ensure user exists
        var userExists = await _dbContext.Users
            .AnyAsync(u => u.Id == command.UserId, cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException($"User {command.UserId} not found");
        }

        // Determine version
        var versionProvided = !string.IsNullOrWhiteSpace(command.Version);
        var version = command.Version?.Trim() ?? string.Empty;

        if (!versionProvided)
        {
            version = await _versioningService.GenerateNextVersionAsync(command.GameId, cancellationToken);
        }
        else
        {
            var duplicate = await _versioningService.VersionExistsAsync(command.GameId, version, cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException($"Version {version} already exists for game {command.GameId}");
            }
        }

        // Create new RuleSpec version
        var specEntity = new RuleSpecEntity
        {
            GameId = command.GameId,
            Version = version,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            CreatedByUserId = command.UserId,
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
                PageNumber = int.TryParse(atom.Page, out var page) ? page : null,
                LineNumber = int.TryParse(atom.Line, out var line) ? line : null,
                SortOrder = sortOrder++,
            });
        }

        _dbContext.RuleSpecs.Add(specEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cache.InvalidateGameAsync(command.GameId.ToString(), cancellationToken);

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
                    Page: a.PageNumber?.ToString(),
                    Line: a.LineNumber?.ToString()
                ))
                .ToList()
        );
    }
}
