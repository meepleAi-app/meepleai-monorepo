using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Helpers;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;
using System.Globalization;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles exporting multiple rule specifications as a ZIP archive.
/// </summary>
public class ExportRuleSpecsCommandHandler : ICommandHandler<ExportRuleSpecsCommand, byte[]>
{
    private readonly MeepleAiDbContext _dbContext;

    public ExportRuleSpecsCommandHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<byte[]> Handle(ExportRuleSpecsCommand command, CancellationToken cancellationToken)
    {
        if (command.GameIds == null || command.GameIds.Count == 0)
        {
            throw new ArgumentException("At least one game ID must be provided", nameof(command));
        }

        // Validate: Maximum 100 rule specs
        if (command.GameIds.Count > 100)
        {
            throw new ArgumentException("Cannot export more than 100 rule specs at once", nameof(command));
        }

        // Fetch all rule specs with their latest versions
        // Note: Include must come AFTER GroupBy projection to materialize navigation properties
        var latestSpecIds = await _dbContext.RuleSpecs
            .AsNoTracking()
            .Where(rs => command.GameIds.Contains(rs.GameId))
            .GroupBy(rs => rs.GameId)
            .Select(g => g.MaxBy(rs => rs.CreatedAt)!.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var ruleSpecs = await _dbContext.RuleSpecs
            .AsNoTracking()
            .Include(rs => rs.Atoms)
            .Where(rs => latestSpecIds.Contains(rs.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (ruleSpecs.Count == 0)
        {
            throw new InvalidOperationException("No rule specs found for the provided game IDs");
        }

        // Create ZIP archive in memory
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var spec in ruleSpecs)
            {
                // Sanitize filename: Remove invalid characters
                var safeGameId = SanitizeFileName(spec.GameId.ToString());
                var fileName = $"{safeGameId}_{spec.Version}.json";

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

                    var exportObject = new
                    {
                        gameId = spec.GameId.ToString(),
                        version = spec.Version,
                        createdAt = spec.CreatedAt,
                        rules = spec.Atoms
                            .OrderBy(a => a.SortOrder)
                            .Select(a => new
                            {
                                id = a.Key,
                                text = a.Text,
                                section = a.Section,
                                page = a.PageNumber?.ToString(CultureInfo.InvariantCulture),
                                line = a.LineNumber?.ToString(CultureInfo.InvariantCulture)
                            })
                            .ToList()
                    };

                    var json = JsonSerializer.Serialize(exportObject, jsonOptions);
                    await writer.WriteAsync(json).ConfigureAwait(false);
                }
            }
        }

        // Return ZIP as byte array
        return memoryStream.ToArray();
    }

    private static string SanitizeFileName(string filename)
    {
        return StringHelper.SanitizeFilename(filename, maxLength: 50, fallbackName: "rulespec");
    }
}