using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Api.Services;

public class RuleSpecService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IAiResponseCacheService _cache;

    public RuleSpecService(MeepleAiDbContext dbContext, IAiResponseCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<RuleSpec> GenerateRuleSpecFromPdfAsync(string pdfId, CancellationToken cancellationToken = default)
    {
        var pdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfId, cancellationToken);

        if (pdf is null)
        {
            throw new InvalidOperationException($"PDF document {pdfId} not found");
        }

        var rules = ParseAtomicRules(pdf.AtomicRules);

        if (rules.Count == 0)
        {
            rules = ParseExtractedText(pdf.ExtractedText);
        }

        if (rules.Count == 0)
        {
            throw new InvalidOperationException($"PDF document {pdfId} does not contain any parsed rules");
        }

        var atoms = new List<RuleAtom>();

        for (int index = 0; index < rules.Count; index++)
        {
            atoms.Add(CreateRuleAtom(rules[index], index + 1));
        }

        var timestamp = DateTime.UtcNow;
        var version = $"ingest-{timestamp:yyyyMMddHHmmss}";

        return new RuleSpec(pdf.GameId, version, timestamp, atoms);
    }

    // TODO: integra parser PDF (Tabula/Camelot via sidecar) e normalizzazione in RuleSpec
    public async Task<RuleSpec> GetOrCreateDemoAsync(string gameId, CancellationToken cancellationToken = default)
    {
        var specEntity = await _dbContext.RuleSpecs
            .Include(r => r.Atoms)
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (specEntity is null)
        {
            var game = await _dbContext.Games
                .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
            if (game is null)
            {
                game = new GameEntity
                {
                    Id = gameId,
                    Name = gameId,
                    CreatedAt = DateTime.UtcNow,
                };
                _dbContext.Games.Add(game);
            }

            specEntity = new RuleSpecEntity
            {
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

    public async Task<RuleSpec?> GetRuleSpecAsync(string gameId, CancellationToken cancellationToken = default)
    {
        var specEntity = await _dbContext.RuleSpecs
            .Include(r => r.Atoms)
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return specEntity is null ? null : ToModel(specEntity);
    }

    public async Task<RuleSpec> UpdateRuleSpecAsync(
        string gameId,
        RuleSpec ruleSpec,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Ensure game exists
        var game = await _dbContext.Games
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game is null)
        {
            throw new InvalidOperationException($"Game {gameId} not found");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required", nameof(userId));
        }

        var userExists = await _dbContext.Users
            .AnyAsync(u => u.Id == userId, cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        var incomingVersion = ruleSpec.version?.Trim();
        var versionProvided = !string.IsNullOrWhiteSpace(incomingVersion);

        var version = incomingVersion ?? string.Empty;

        if (!versionProvided)
        {
            version = await GenerateNextVersionAsync(gameId, cancellationToken);
        }
        else
        {
            var duplicate = await _dbContext.RuleSpecs
                .AnyAsync(r => r.GameId == gameId && r.Version == version, cancellationToken);

            if (duplicate)
            {
                throw new InvalidOperationException($"Version {version} already exists for game {gameId}");
            }
        }

        // Create new RuleSpec version
        var specEntity = new RuleSpecEntity
        {
            GameId = gameId,
            Version = version,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
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

        await _cache.InvalidateGameAsync(gameId, cancellationToken);

        return ToModel(specEntity);
    }

    private async Task<string> GenerateNextVersionAsync(string gameId, CancellationToken cancellationToken)
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
            : $"v{DateTime.UtcNow:yyyyMMddHHmmss}";

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
                candidate = $"v{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            }
        }

        return candidate;
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

    public async Task<RuleSpecHistory> GetVersionHistoryAsync(string gameId, CancellationToken cancellationToken = default)
    {
        var versions = await _dbContext.RuleSpecs
            .Include(r => r.Atoms)
            .Include(r => r.CreatedBy)
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RuleSpecVersion(
                r.Version,
                r.CreatedAt,
                r.Atoms.Count,
                r.CreatedBy != null ? r.CreatedBy.DisplayName ?? r.CreatedBy.Email : null
            ))
            .ToListAsync(cancellationToken);

        return new RuleSpecHistory(gameId, versions, versions.Count);
    }

    public async Task<RuleSpec?> GetVersionAsync(string gameId, string version, CancellationToken cancellationToken = default)
    {
        var specEntity = await _dbContext.RuleSpecs
            .Include(r => r.Atoms)
            .Where(r => r.GameId == gameId && r.Version == version)
            .FirstOrDefaultAsync(cancellationToken);

        return specEntity is null ? null : ToModel(specEntity);
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

    private static List<string> ParseAtomicRules(string? atomicRulesJson)
    {
        if (string.IsNullOrWhiteSpace(atomicRulesJson))
        {
            return new List<string>();
        }

        try
        {
            var rules = JsonSerializer.Deserialize<List<string>>(atomicRulesJson);
            return rules?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList() ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static List<string> ParseExtractedText(string? extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return new List<string>();
        }

        return extractedText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static RuleAtom CreateRuleAtom(string rawText, int index)
    {
        string? page = null;
        string cleanedText = rawText.Trim();

        var tableMatch = Regex.Match(cleanedText, "^\\[Table on page (?<page>\\d+)\\]\\s*(?<rest>.+)$", RegexOptions.IgnoreCase);
        if (tableMatch.Success)
        {
            page = tableMatch.Groups["page"].Value;
            cleanedText = tableMatch.Groups["rest"].Value.Trim();
        }
        else
        {
            var pageMatch = Regex.Match(cleanedText, "page\\s+(?<page>\\d+)", RegexOptions.IgnoreCase);
            if (pageMatch.Success)
            {
                page = pageMatch.Groups["page"].Value;
            }
        }

        return new RuleAtom($"r{index}", cleanedText, null, page, null);
    }
}
