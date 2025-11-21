using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles creating or retrieving a demo RuleSpec for a game.
/// Used for admin/testing purposes.
/// </summary>
public class CreateDemoRuleSpecCommandHandler : ICommandHandler<CreateDemoRuleSpecCommand, RuleSpecDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CreateDemoRuleSpecCommandHandler(MeepleAiDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RuleSpecDto> Handle(CreateDemoRuleSpecCommand command, CancellationToken cancellationToken)
    {
        // Check if RuleSpec already exists
        var specEntity = await _dbContext.RuleSpecs
            .AsNoTracking()
            .Include(r => r.Atoms)
            .Where(r => r.GameId == command.GameId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (specEntity != null)
        {
            // Return existing
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

        // Create game if it doesn't exist
        var game = await _dbContext.Games.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);
        if (game is null)
        {
            game = new GameEntity
            {
                Id = command.GameId,
                Name = command.GameId.ToString(),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            };
            _dbContext.Games.Add(game);
        }

        // Create demo RuleSpec
        var newSpecEntity = new RuleSpecEntity
        {
            GameId = command.GameId,
            Version = "v0-demo",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

        newSpecEntity.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = newSpecEntity,
            Key = "r1",
            Text = "Two players.",
            Section = "Basics",
            PageNumber = 1,
            LineNumber = 1,
            SortOrder = 1,
        });

        newSpecEntity.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = newSpecEntity,
            Key = "r2",
            Text = "White moves first.",
            Section = "Basics",
            PageNumber = 1,
            LineNumber = 2,
            SortOrder = 2,
        });

        _dbContext.RuleSpecs.Add(newSpecEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RuleSpecDto(
            Id: newSpecEntity.Id,
            GameId: newSpecEntity.GameId,
            Version: newSpecEntity.Version,
            CreatedAt: newSpecEntity.CreatedAt,
            CreatedByUserId: newSpecEntity.CreatedByUserId,
            ParentVersionId: newSpecEntity.ParentVersionId,
            Atoms: newSpecEntity.Atoms
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
