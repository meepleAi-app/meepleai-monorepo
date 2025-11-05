using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class RuleSpecService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IAiResponseCacheService _cache;
    private readonly TimeProvider _timeProvider;

    public RuleSpecService(MeepleAiDbContext dbContext, IAiResponseCacheService cache, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _cache = cache;
        _timeProvider = timeProvider ?? TimeProvider.System;
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

        var timestamp = _timeProvider.GetUtcNow().UtcDateTime;
        var version = $"ingest-{timestamp:yyyyMMddHHmmss}";

        return new RuleSpec(pdf.GameId, version, timestamp, atoms);
    }

    // FUTURE ENHANCEMENT: Integrate PDF parser (Tabula/Camelot via sidecar) for automated RuleSpec extraction
    // This would enable automatic conversion of PDF rulebooks to machine-readable RuleSpec format
    public async Task<RuleSpec> GetOrCreateDemoAsync(string gameId, CancellationToken cancellationToken = default)
    {
        var specEntity = await _dbContext.RuleSpecs
            .AsNoTracking() // PERF-05: Read-only query
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
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                };
                _dbContext.Games.Add(game);
            }

            specEntity = new RuleSpecEntity
            {
                GameId = gameId,
                Version = "v0-demo",
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
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
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
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
            .Select(v => v!.Value) // Safe because of Where clause - null-forgiving operator used
            .ToList();

        int? nextNumeric = null;

        if (numericVersions.Count > 0)
        {
            nextNumeric = numericVersions.Max() + 1;
        }

        string candidate = nextNumeric.HasValue
            ? $"v{nextNumeric.Value}"
            : $"v{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMddHHmmss}";

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

    /// <summary>
    /// EDIT-06: Get version timeline with filtering and branching support
    /// </summary>
    public async Task<VersionTimelineResponse> GetVersionTimelineAsync(
        string gameId,
        VersionTimelineFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new VersionTimelineFilters();

        var query = _dbContext.RuleSpecs
            .AsNoTracking() // PERF-06
            .Include(r => r.CreatedBy)
            .Where(r => r.GameId == gameId);

        // Apply filters
        if (filters.StartDate.HasValue)
            query = query.Where(r => r.CreatedAt >= filters.StartDate.Value);

        if (filters.EndDate.HasValue)
            query = query.Where(r => r.CreatedAt <= filters.EndDate.Value);

        if (!string.IsNullOrWhiteSpace(filters.Author))
        {
            var authorFilter = filters.Author.Trim();
            // CWE-476: Add null coalescing to Email property access
            query = query.Where(r => r.CreatedBy != null &&
                ((r.CreatedBy.DisplayName != null && r.CreatedBy.DisplayName.Contains(authorFilter)) ||
                 (r.CreatedBy.Email != null && r.CreatedBy.Email.Contains(authorFilter))));
        }

        if (!string.IsNullOrWhiteSpace(filters.SearchQuery))
        {
            var searchTerm = filters.SearchQuery.Trim();
            query = query.Where(r => r.Version.Contains(searchTerm));
        }

        var versions = await query
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Version,
                r.CreatedAt,
                r.ParentVersionId,
                r.MergedFromVersionIds,
                AtomCount = r.Atoms.Count,
                // CWE-476: Ensure Email fallback handles null properly
                AuthorName = r.CreatedBy != null
                    ? r.CreatedBy.DisplayName ?? r.CreatedBy.Email ?? "Unknown"
                    : "Unknown"
            })
            .ToListAsync(cancellationToken);

        // Build version nodes with parent/merge relationships
        var versionNodes = new List<VersionNodeDto>();
        var versionMap = versions.ToDictionary(v => v.Id, v => v.Version);

        foreach (var version in versions)
        {
            var node = new VersionNodeDto
            {
                Id = version.Id,
                Version = version.Version,
                Title = $"Version {version.Version}",
                Description = $"{version.AtomCount} rule atoms",
                Author = version.AuthorName,
                CreatedAt = version.CreatedAt,
                ParentVersionId = version.ParentVersionId,
                ParentVersion = version.ParentVersionId.HasValue &&
                    versionMap.TryGetValue(version.ParentVersionId.Value, out var parentVer)
                    ? parentVer
                    : null,
                ChangeCount = version.AtomCount,
                IsCurrentVersion = false // Will be updated below
            };

            // Parse merged from version IDs
            if (!string.IsNullOrWhiteSpace(version.MergedFromVersionIds))
            {
                var mergedIds = version.MergedFromVersionIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => Guid.TryParse(id.Trim(), out var guid) ? guid : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value) // Safe because of Where clause - null-forgiving operator used
                    .ToList();

                node = node with
                {
                    MergedFromVersionIds = mergedIds,
                    MergedFromVersions = mergedIds
                        .Where(id => versionMap.ContainsKey(id))
                        .Select(id => versionMap[id])
                        .ToList()
                };
            }

            versionNodes.Add(node);
        }

        // Mark the most recent version as current
        if (versionNodes.Any())
        {
            var latestVersion = versionNodes.OrderByDescending(v => v.CreatedAt).First();
            versionNodes = versionNodes.Select(v =>
                v.Id == latestVersion.Id ? v with { IsCurrentVersion = true } : v
            ).ToList();
        }

        // Extract unique authors for filter dropdown
        var authors = versionNodes
            .Select(v => v.Author)
            .Distinct()
            .OrderBy(a => a)
            .ToList();

        return new VersionTimelineResponse
        {
            GameId = gameId,
            Versions = versionNodes,
            TotalVersions = versionNodes.Count,
            Authors = authors
        };
    }

    /// <summary>
    /// Creates a ZIP archive containing multiple rule specs as JSON files
    /// </summary>
    /// <param name="gameIds">List of game IDs to export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Byte array containing the ZIP archive</returns>
    public async Task<byte[]> CreateZipArchiveAsync(List<string> gameIds, CancellationToken cancellationToken = default)
    {
        if (gameIds == null || gameIds.Count == 0)
        {
            throw new ArgumentException("At least one game ID must be provided", nameof(gameIds));
        }

        // Validate: Maximum 100 rule specs
        if (gameIds.Count > 100)
        {
            throw new ArgumentException("Cannot export more than 100 rule specs at once", nameof(gameIds));
        }

        // Fetch all rule specs with their latest versions
        var ruleSpecs = new List<(string gameId, RuleSpec spec)>();

        foreach (var gameId in gameIds.Distinct())
        {
            var spec = await GetRuleSpecAsync(gameId, cancellationToken);
            if (spec != null)
            {
                ruleSpecs.Add((gameId, spec));
            }
        }

        if (ruleSpecs.Count == 0)
        {
            throw new InvalidOperationException("No rule specs found for the provided game IDs");
        }

        // Create ZIP archive in memory
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (gameId, spec) in ruleSpecs)
            {
                // Sanitize filename: Remove invalid characters
                var safeGameId = SanitizeFileName(gameId);
                var fileName = $"{safeGameId}_{spec.version}.json";

                // Create entry in ZIP
                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);

                // Write JSON to entry
                using (var entryStream = entry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    var json = JsonSerializer.Serialize(spec, jsonOptions);
                    await writer.WriteAsync(json);
                }
            }
        }

        // Return ZIP as byte array
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Sanitizes a filename by removing invalid characters
    /// </summary>
    private static string SanitizeFileName(string filename)
    {
        return StringHelper.SanitizeFilename(filename, maxLength: 50, fallbackName: "rulespec");
    }
}
