using System.Globalization;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using ClosedXML.Excel;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────

public sealed record ExcelPreviewResult(
    int TotalRows,
    IReadOnlyList<ExcelGameRow> NewGames,
    IReadOnlyList<ExcelGameModification> ModifiedGames,
    IReadOnlyList<ExcelGameRemoval> RemovedGames,
    IReadOnlyList<ExcelGameRow> DuplicateBggIds,
    IReadOnlyList<ExcelRowError> Errors,
    int UnchangedCount);

public sealed record ExcelGameRow(
    int RowNumber,
    string Title,
    int? BggId,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge,
    string? Description);

public sealed record ExcelGameModification(
    Guid ExistingGameId,
    string ExistingTitle,
    string CurrentStatus,
    ExcelGameRow ExcelRow,
    IReadOnlyList<string> ChangedFields);

public sealed record ExcelGameRemoval(
    Guid GameId,
    string Title,
    string Status,
    string GameDataStatus,
    bool IsProtected);

// ── Internal projection DTO ──────────────────────────────────────────

internal sealed record ExistingGameProjection(
    Guid Id,
    string Title,
    int? BggId,
    GameStatus Status,
    GameDataStatus GameDataStatus,
    int YearPublished,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    string Description);

// ── Query ────────────────────────────────────────────────────────────

internal sealed record PreviewExcelImportQuery(IFormFile File) : IQuery<ExcelPreviewResult>;

// ── Validator ────────────────────────────────────────────────────────

internal sealed class PreviewExcelImportQueryValidator : AbstractValidator<PreviewExcelImportQuery>
{
    public PreviewExcelImportQueryValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("File is required");
        RuleFor(x => x.File.Length)
            .LessThanOrEqualTo(5 * 1024 * 1024)
            .When(x => x.File is not null)
            .WithMessage("File must be 5MB or smaller");
        RuleFor(x => x.File.FileName)
            .Must(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            .When(x => x.File is not null)
            .WithMessage("File must be an .xlsx Excel file");
    }
}

// ── Handler ──────────────────────────────────────────────────────────

internal sealed class PreviewExcelImportQueryHandler
    : IQueryHandler<PreviewExcelImportQuery, ExcelPreviewResult>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<PreviewExcelImportQueryHandler> _logger;

    private const int MaxRows = 5000;

    public PreviewExcelImportQueryHandler(
        MeepleAiDbContext context,
        ILogger<PreviewExcelImportQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExcelPreviewResult> Handle(
        PreviewExcelImportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var workbook = new XLWorkbook(query.File.OpenReadStream());
        var worksheet = workbook.Worksheets.First();

        // ── Parse header columns ──
        var headerRow = worksheet.Row(1);
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            columns[cell.GetString().Trim()] = cell.Address.ColumnNumber;
        }

        if (!columns.ContainsKey("Name"))
        {
            return new ExcelPreviewResult(0, [], [], [], [],
                [new ExcelRowError(1, "Name", "Required column 'Name' not found in header row")], 0);
        }

        // ── Load existing games from DB (capped for memory safety) ──
        var existingGames = await _context.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Title)
            .Take(MaxRows * 2)
            .Select(g => new ExistingGameProjection(
                g.Id,
                g.Title,
                g.BggId,
                (GameStatus)g.Status,
                (GameDataStatus)g.GameDataStatus,
                g.YearPublished,
                g.MinPlayers,
                g.MaxPlayers,
                g.PlayingTimeMinutes,
                g.MinAge,
                g.Description))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gameById = existingGames.ToDictionary(g => g.Id);
        var gameByBggId = existingGames
            .Where(g => g.BggId.HasValue)
            .ToDictionary(g => g.BggId!.Value);
        var gameByTitle = existingGames
            .GroupBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var matchedGameIds = new HashSet<Guid>();
        var newGames = new List<ExcelGameRow>();
        var modifiedGames = new List<ExcelGameModification>();
        var duplicateBggIds = new List<ExcelGameRow>();
        var errors = new List<ExcelRowError>();
        var unchangedCount = 0;
        var totalDataRows = 0;

        var lastRow = Math.Min(
            worksheet.LastRowUsed()?.RowNumber() ?? 1,
            MaxRows + 1);

        for (var row = 2; row <= lastRow; row++)
        {
            var title = GetCellString(worksheet, row, columns, "Name");

            if (string.IsNullOrWhiteSpace(title))
                continue;

            totalDataRows++;

            var excelRow = ParseRow(worksheet, row, columns, title, errors);
            if (excelRow is null) continue;

            // ── Try match by Id first ──
            var idStr = GetCellString(worksheet, row, columns, "Id");
            if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var existingId))
            {
                if (gameById.TryGetValue(existingId, out var existing))
                {
                    matchedGameIds.Add(existingId);
                    var changedFields = DetectChanges(existing, excelRow);

                    if (changedFields.Count > 0)
                    {
                        var isProtected = existing.Status != GameStatus.Draft;
                        modifiedGames.Add(new ExcelGameModification(
                            existingId,
                            existing.Title,
                            existing.Status.ToString(),
                            excelRow,
                            isProtected
                                ? [$"PROTECTED (status: {existing.Status})", .. changedFields]
                                : changedFields));
                    }
                    else
                    {
                        unchangedCount++;
                    }
                    continue;
                }
            }

            // ── Check BGG ID duplicate ──
            if (excelRow.BggId.HasValue && gameByBggId.TryGetValue(excelRow.BggId.Value, out var bggMatch))
            {
                duplicateBggIds.Add(excelRow);
                matchedGameIds.Add(bggMatch.Id);
                continue;
            }

            // ── Check title duplicate ──
            if (gameByTitle.TryGetValue(title, out var titleMatch))
            {
                matchedGameIds.Add(titleMatch.Id);
                var changedFields = DetectChanges(titleMatch, excelRow);

                if (changedFields.Count > 0)
                {
                    var isProtected = titleMatch.Status != GameStatus.Draft;
                    modifiedGames.Add(new ExcelGameModification(
                        titleMatch.Id,
                        titleMatch.Title,
                        titleMatch.Status.ToString(),
                        excelRow,
                        isProtected
                            ? [$"PROTECTED (status: {titleMatch.Status})", .. changedFields]
                            : changedFields));
                }
                else
                {
                    unchangedCount++;
                }
                continue;
            }

            // ── New game ──
            newGames.Add(excelRow);
        }

        // ── Removed games: in DB but not in Excel ──
        var removedGames = existingGames
            .Where(g => !matchedGameIds.Contains(g.Id))
            .Select(g => new ExcelGameRemoval(
                g.Id,
                g.Title,
                g.Status.ToString(),
                g.GameDataStatus.ToString(),
                g.Status != GameStatus.Draft))
            .ToList();

        _logger.LogInformation(
            "Excel preview: {Total} rows, {New} new, {Modified} modified, {Removed} removed, {Duplicates} duplicate BGG IDs, {Unchanged} unchanged, {Errors} errors",
            totalDataRows, newGames.Count, modifiedGames.Count, removedGames.Count,
            duplicateBggIds.Count, unchangedCount, errors.Count);

        return new ExcelPreviewResult(
            totalDataRows, newGames, modifiedGames, removedGames,
            duplicateBggIds, errors, unchangedCount);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static string GetCellString(
        IXLWorksheet ws, int row, Dictionary<string, int> columns, string columnName)
    {
        return columns.TryGetValue(columnName, out var col)
            ? ws.Cell(row, col).GetString().Trim()
            : string.Empty;
    }

    private static int? GetCellInt(
        IXLWorksheet ws, int row, Dictionary<string, int> columns, string columnName)
    {
        var str = GetCellString(ws, row, columns, columnName);
        if (string.IsNullOrWhiteSpace(str)) return null;
        return int.TryParse(str, CultureInfo.InvariantCulture, out var val) ? val : null;
    }

    private static ExcelGameRow? ParseRow(
        IXLWorksheet ws, int row, Dictionary<string, int> columns,
        string title, List<ExcelRowError> errors)
    {
        if (title.Length > 500)
        {
            errors.Add(new ExcelRowError(row, "Name", "Name exceeds 500 characters"));
            return null;
        }

        var bggIdStr = GetCellString(ws, row, columns, "BggId");
        int? bggId = null;
        if (!string.IsNullOrWhiteSpace(bggIdStr))
        {
            if (int.TryParse(bggIdStr, CultureInfo.InvariantCulture, out var parsed))
            {
                if (parsed <= 0)
                {
                    errors.Add(new ExcelRowError(row, "BggId", "BggId must be a positive integer"));
                    return null;
                }
                bggId = parsed;
            }
            else
            {
                errors.Add(new ExcelRowError(row, "BggId", "BggId must be a valid integer"));
                return null;
            }
        }

        return new ExcelGameRow(
            row,
            title,
            bggId,
            GetCellInt(ws, row, columns, "YearPublished"),
            GetCellInt(ws, row, columns, "MinPlayers"),
            GetCellInt(ws, row, columns, "MaxPlayers"),
            GetCellInt(ws, row, columns, "PlayingTime"),
            GetCellInt(ws, row, columns, "MinAge"),
            GetCellString(ws, row, columns, "Description"));
    }

    private static List<string> DetectChanges(ExistingGameProjection existing, ExcelGameRow excelRow)
    {
        var changes = new List<string>();

        if (!string.Equals(existing.Title, excelRow.Title, StringComparison.OrdinalIgnoreCase))
            changes.Add($"Title: '{existing.Title}' → '{excelRow.Title}'");

        if (excelRow.BggId.HasValue && existing.BggId != excelRow.BggId)
            changes.Add($"BggId: {existing.BggId} → {excelRow.BggId}");

        if (excelRow.YearPublished.HasValue && existing.YearPublished != excelRow.YearPublished)
            changes.Add($"YearPublished: {existing.YearPublished} → {excelRow.YearPublished}");

        if (excelRow.MinPlayers.HasValue && existing.MinPlayers != excelRow.MinPlayers)
            changes.Add($"MinPlayers: {existing.MinPlayers} → {excelRow.MinPlayers}");

        if (excelRow.MaxPlayers.HasValue && existing.MaxPlayers != excelRow.MaxPlayers)
            changes.Add($"MaxPlayers: {existing.MaxPlayers} → {excelRow.MaxPlayers}");

        if (excelRow.PlayingTimeMinutes.HasValue && existing.PlayingTimeMinutes != excelRow.PlayingTimeMinutes)
            changes.Add($"PlayingTime: {existing.PlayingTimeMinutes} → {excelRow.PlayingTimeMinutes}");

        if (excelRow.MinAge.HasValue && existing.MinAge != excelRow.MinAge)
            changes.Add($"MinAge: {existing.MinAge} → {excelRow.MinAge}");

        if (!string.IsNullOrWhiteSpace(excelRow.Description) &&
            !string.Equals(existing.Description, excelRow.Description, StringComparison.Ordinal))
            changes.Add("Description: changed");

        return changes;
    }
}
