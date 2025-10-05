using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Api.Services;

public class RuleSpecService
{
    private readonly MeepleAiDbContext _dbContext;
    public RuleSpecService(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
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

    public async Task<RuleSpec> GenerateRuleSpecFromPdfAsync(string pdfId, CancellationToken cancellationToken = default)
    {
        var pdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfId, cancellationToken);

        if (pdf is null)
        {
            throw new KeyNotFoundException($"PDF document {pdfId} not found");
        }

        if (!string.Equals(pdf.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("PDF is not ready yet. Please wait for processing to complete.");
        }

        var atoms = ParseAtomicRules(pdf.AtomicRules);

        if (atoms.Count == 0)
        {
            atoms.AddRange(ParseExtractedText(pdf.ExtractedText));
        }

        if (atoms.Count == 0)
        {
            throw new InvalidOperationException("No extracted rules available for this PDF");
        }

        var processedAt = pdf.ProcessedAt ?? DateTime.UtcNow;
        var version = $"pdf-{processedAt:yyyyMMddHHmmss}";

        return new RuleSpec(pdf.GameId, version, processedAt, atoms);
    }

    public async Task<RuleSpec> UpdateRuleSpecAsync(string gameId, RuleSpec ruleSpec, CancellationToken cancellationToken = default)
    {
        // Ensure game exists
        var game = await _dbContext.Games
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game is null)
        {
            throw new InvalidOperationException($"Game {gameId} not found");
        }

        // Create new RuleSpec version
        var specEntity = new RuleSpecEntity
        {
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

    private static List<RuleAtom> ParseAtomicRules(string? atomicRulesJson)
    {
        var atoms = new List<RuleAtom>();

        if (string.IsNullOrWhiteSpace(atomicRulesJson))
        {
            return atoms;
        }

        List<string>? atomicRules;
        try
        {
            atomicRules = JsonSerializer.Deserialize<List<string>>(atomicRulesJson);
        }
        catch (JsonException)
        {
            return atoms;
        }

        if (atomicRules is null)
        {
            return atoms;
        }

        int index = 1;
        foreach (var ruleText in atomicRules)
        {
            if (string.IsNullOrWhiteSpace(ruleText))
            {
                continue;
            }

            var text = ruleText.Trim();
            string? page = null;
            string? section = null;

            var match = Regex.Match(text, @"\[Table on page\s+(?<page>\d+)\]", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                page = match.Groups["page"].Value;
                section = "Table";
                text = text.Substring(match.Index + match.Length).Trim();
            }

            if (text.StartsWith(':'))
            {
                text = text.TrimStart(':').Trim();
            }

            if (text.Length == 0)
            {
                continue;
            }

            text = NormalizeWhitespace(text);

            atoms.Add(new RuleAtom($"atom-{index}", text, section, page, null));
            index++;
        }

        return atoms;
    }

    private static List<RuleAtom> ParseExtractedText(string? extractedText)
    {
        var atoms = new List<RuleAtom>();

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return atoms;
        }

        var segments = SplitIntoSegments(extractedText);

        int index = 1;
        foreach (var segment in segments)
        {
            var text = NormalizeWhitespace(segment);
            if (text.Length == 0)
            {
                continue;
            }

            atoms.Add(new RuleAtom($"atom-{index}", text));
            index++;
        }

        return atoms;
    }

    private static IEnumerable<string> SplitIntoSegments(string text)
    {
        var paragraphSeparators = new[] { "\r\n\r\n", "\n\n" };
        var segments = text.Split(paragraphSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length <= 1)
        {
            segments = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return segments.Where(s => !string.IsNullOrWhiteSpace(s));
    }

    private static string NormalizeWhitespace(string text)
    {
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
