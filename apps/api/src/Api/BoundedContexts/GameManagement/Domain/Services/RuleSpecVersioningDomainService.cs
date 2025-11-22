using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.GameManagement.Domain.Services;

/// <summary>
/// Domain service for RuleSpec versioning logic.
/// Handles version number generation and validation.
/// </summary>
public class RuleSpecVersioningDomainService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public RuleSpecVersioningDomainService(MeepleAiDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Generates the next version number for a game's RuleSpec.
    /// Uses numeric versioning (v1, v2, v3...) or timestamp-based if no numeric versions exist.
    /// </summary>
    public async Task<string> GenerateNextVersionAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var versions = await _dbContext.RuleSpecs
            .Where(r => r.GameId == gameId)
            .Select(r => r.Version)
            .ToListAsync(cancellationToken);

        var numericVersions = versions
            .Select(TryParseNumericVersion)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        int? nextNumeric = null;

        if (numericVersions.Count > 0)
        {
            nextNumeric = numericVersions.Max() + 1;
        }

        string candidate = nextNumeric.HasValue
            ? $"v{nextNumeric.Value}"
            : $"v{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMddHHmmss}";

        // Ensure uniqueness
        while (await _dbContext.RuleSpecs
            .AnyAsync(r => r.GameId == gameId && r.Version == candidate, cancellationToken))
        {
            if (nextNumeric.HasValue)
            {
                nextNumeric++;
                candidate = $"v{nextNumeric.Value}";
            }
            else
            {
                candidate = $"v{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMddHHmmssfff}";
            }
        }

        return candidate;
    }

    /// <summary>
    /// Validates if a version already exists for a game.
    /// </summary>
    public async Task<bool> VersionExistsAsync(Guid gameId, string version, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RuleSpecs
            .AnyAsync(r => r.GameId == gameId && r.Version == version, cancellationToken);
    }

    private static int? TryParseNumericVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var trimmed = version.Trim();

        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        return int.TryParse(trimmed, out var number) ? number : null;
    }
}
