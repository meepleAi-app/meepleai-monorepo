using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Api.Services;

public class RuleSpecService
{
    private readonly MeepleAiDbContext _dbContext;

    public RuleSpecService(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // TODO: integra parser PDF (Tabula/Camelot via sidecar) e normalizzazione in RuleSpec
    public async Task<RuleSpec> GetOrCreateDemoAsync(string tenantId, string gameId, CancellationToken cancellationToken = default)
    {
        var specEntity = await _dbContext.RuleSpecs
            .Include(r => r.Atoms)
            .Where(r => r.TenantId == tenantId && r.GameId == gameId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (specEntity is null)
        {
            var tenant = await _dbContext.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
            if (tenant is null)
            {
                tenant = new TenantEntity
                {
                    Id = tenantId,
                    Name = tenantId,
                    CreatedAt = DateTime.UtcNow,
                };
                _dbContext.Tenants.Add(tenant);
            }

            var game = await _dbContext.Games
                .FirstOrDefaultAsync(g => g.Id == gameId && g.TenantId == tenantId, cancellationToken);
            if (game is null)
            {
                game = new GameEntity
                {
                    Id = gameId,
                    TenantId = tenantId,
                    Name = gameId,
                    CreatedAt = DateTime.UtcNow,
                };
                _dbContext.Games.Add(game);
            }

            specEntity = new RuleSpecEntity
            {
                TenantId = tenantId,
                GameId = gameId,
                Version = "v0-demo",
                CreatedAt = DateTime.UtcNow,
            };

            specEntity.Atoms.Add(new RuleAtomEntity
            {
                RuleSpec = specEntity,
                Key = "r1",
                Text = "Two players.",
                Section = "Basics",
                PageNumber = 1,
                LineNumber = 1,
                SortOrder = 1,
            });

            specEntity.Atoms.Add(new RuleAtomEntity
            {
                RuleSpec = specEntity,
                Key = "r2",
                Text = "White moves first.",
                Section = "Basics",
                PageNumber = 1,
                LineNumber = 2,
                SortOrder = 2,
            });

            _dbContext.RuleSpecs.Add(specEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToModel(specEntity);
    }

    public async Task<RuleSpec?> GetRuleSpecAsync(string tenantId, string gameId, CancellationToken cancellationToken = default)
    {
        var specEntity = await _dbContext.RuleSpecs
            .Include(r => r.Atoms)
            .Where(r => r.TenantId == tenantId && r.GameId == gameId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return specEntity is null ? null : ToModel(specEntity);
    }

    public async Task<RuleSpec> UpdateRuleSpecAsync(string tenantId, string gameId, RuleSpec ruleSpec, CancellationToken cancellationToken = default)
    {
        // Ensure game exists
        var game = await _dbContext.Games
            .FirstOrDefaultAsync(g => g.Id == gameId && g.TenantId == tenantId, cancellationToken);

        if (game is null)
        {
            throw new InvalidOperationException($"Game {gameId} not found for tenant {tenantId}");
        }

        // Create new RuleSpec version
        var specEntity = new RuleSpecEntity
        {
            TenantId = tenantId,
            GameId = gameId,
            Version = ruleSpec.version,
            CreatedAt = DateTime.UtcNow,
        };

        int sortOrder = 1;
        foreach (var atom in ruleSpec.rules)
        {
            specEntity.Atoms.Add(new RuleAtomEntity
            {
                RuleSpec = specEntity,
                Key = atom.id,
                Text = atom.text,
                Section = atom.section,
                PageNumber = int.TryParse(atom.page, out var page) ? page : null,
                LineNumber = int.TryParse(atom.line, out var line) ? line : null,
                SortOrder = sortOrder++,
            });
        }

        _dbContext.RuleSpecs.Add(specEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToModel(specEntity);
    }

    private static RuleSpec ToModel(RuleSpecEntity entity)
    {
        var atoms = entity.Atoms
            .OrderBy(a => a.SortOrder)
            .Select(a => new RuleAtom(
                a.Key,
                a.Text,
                a.Section,
                a.PageNumber?.ToString(CultureInfo.InvariantCulture),
                a.LineNumber?.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        return new RuleSpec(entity.GameId, entity.Version, entity.CreatedAt, atoms);
    }
}
