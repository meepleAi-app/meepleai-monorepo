using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class GameService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ITenantContext _tenantContext;

    public GameService(MeepleAiDbContext dbContext, ITenantContext tenantContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<GameEntity> CreateGameAsync(string name, string? requestedGameId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.GetRequiredTenantId();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Game name is required", nameof(name));
        }

        var normalizedTenantId = tenantId.Trim();
        var normalizedName = name.Trim();
        var desiredId = !string.IsNullOrWhiteSpace(requestedGameId)
            ? NormalizeId(requestedGameId)
            : GenerateIdFromName(normalizedName);

        if (string.IsNullOrWhiteSpace(desiredId))
        {
            desiredId = Guid.NewGuid().ToString("N")[..12];
        }

        if (await _dbContext.Games.AnyAsync(g => g.TenantId == normalizedTenantId && g.Id == desiredId, ct))
        {
            throw new InvalidOperationException($"Game with id '{desiredId}' already exists");
        }

        if (await _dbContext.Games.AnyAsync(g => g.TenantId == normalizedTenantId && g.Name == normalizedName, ct))
        {
            throw new InvalidOperationException($"Game with name '{normalizedName}' already exists");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var entity = new GameEntity
        {
            Id = desiredId,
            TenantId = normalizedTenantId,
            Name = normalizedName,
            CreatedAt = now
        };

        _dbContext.Games.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        return entity;
    }

    public async Task<IReadOnlyList<GameEntity>> GetGamesAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Array.Empty<GameEntity>();
        }

        return await _dbContext.Games
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
    }

    private static string GenerateIdFromName(string name)
    {
        var normalized = NormalizeId(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized;
    }

    private static string NormalizeId(string value)
    {
        var span = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(capacity: Math.Min(span.Length, 64));
        char? lastAppended = null;

        foreach (var ch in span)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastAppended = ch;
            }
            else if (builder.Length < 64 && lastAppended != '-')
            {
                builder.Append('-');
                lastAppended = '-';
            }

            if (builder.Length >= 64)
            {
                break;
            }
        }

        var result = builder.ToString().Trim('-');
        if (result.Length > 64)
        {
            result = result[..64];
        }

        return result;
    }
}
