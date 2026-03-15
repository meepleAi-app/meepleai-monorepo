# Excel Game Management (Round-Trip) Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable admin to download the game catalog as Excel, modify it offline (add/edit/remove rows), re-upload it with diff preview, assign BGG IDs, enrich from BGG, and confirm changes.

**Architecture:** Enhances existing `AdminCatalogIngestionEndpoints` with a 2-step import flow: preview (parse + diff) → confirm (apply changes). Reuses existing `BggImportQueueService` for BGG enrichment and `ClosedXML` for Excel I/O. Domain model gets a new `AssignBggId()` method. Published games are protected from modification/deletion.

**Tech Stack:** .NET 9, MediatR (CQRS), ClosedXML, EF Core, FluentValidation

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| **Modify** | `SharedGameCatalog/Domain/Aggregates/SharedGame.cs` | Add `AssignBggId()` method |
| **Modify** | `SharedGameCatalog/Domain/Repositories/ISharedGameRepository.cs` | Add `GetAllNonDeletedAsync()` |
| **Modify** | `SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs` | Implement `GetAllNonDeletedAsync()` |
| **Modify** | `SharedGameCatalog/Application/Commands/ExportGamesToExcelCommand.cs` | Add Id, Description, MinAge, ComplexityRating, AverageRating columns |
| **Create** | `SharedGameCatalog/Application/Queries/PreviewExcelImportQuery.cs` | Parse Excel, diff with DB, return preview |
| **Create** | `SharedGameCatalog/Application/Commands/ConfirmExcelImportCommand.cs` | Apply diff: create/update/soft-delete |
| **Create** | `SharedGameCatalog/Application/Commands/AssignBggIdCommand.cs` | Assign BGG ID to skeleton game |
| **Modify** | `Routing/AdminCatalogIngestionEndpoints.cs` | Add 3 new endpoints |

All paths relative to `apps/api/src/Api/BoundedContexts/` (or `apps/api/src/Api/` for Routing).

---

## Chunk 1: Domain & Repository Changes

### Task 1: Add `AssignBggId()` to SharedGame

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs`

- [ ] **Step 1: Add `AssignBggId` method after the `EnrichFromBgg` method (around line 516)**

```csharp
/// <summary>
/// Assigns a BGG ID to this game. Only allowed for Skeleton or Failed games.
/// Enables subsequent BGG enrichment.
/// </summary>
public void AssignBggId(int bggId, Guid modifiedBy)
{
    if (bggId <= 0)
        throw new ArgumentException("BggId must be a positive integer.", nameof(bggId));

    if (modifiedBy == Guid.Empty)
        throw new ArgumentException("ModifiedBy cannot be empty.", nameof(modifiedBy));

    if (_gameDataStatus != GameDataStatus.Skeleton && _gameDataStatus != GameDataStatus.Failed)
        throw new InvalidOperationException(
            $"Cannot assign BggId in {_gameDataStatus} state. Must be Skeleton or Failed.");

    _bggId = bggId;
    _modifiedBy = modifiedBy;
    _modifiedAt = DateTime.UtcNow;
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs
git commit -m "feat(shared-game): add AssignBggId domain method for skeleton games"
```

### Task 2: Add repository method for full catalog query

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/ISharedGameRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs`

- [ ] **Step 1: Add interface method to `ISharedGameRepository.cs`**

```csharp
/// <summary>
/// Gets all non-deleted shared games as lightweight projections for Excel diff.
/// Returns Id, Title, BggId, Status, GameDataStatus, and core fields.
/// </summary>
Task<List<SharedGame>> GetAllNonDeletedAsync(CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Implement in `SharedGameRepository.cs`**

Find the repository implementation and add:

```csharp
public async Task<List<SharedGame>> GetAllNonDeletedAsync(CancellationToken cancellationToken = default)
{
    return await _context.SharedGames
        .Where(g => !g.IsDeleted)
        .OrderBy(g => g.Title)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/ISharedGameRepository.cs
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs
git commit -m "feat(shared-game): add GetAllNonDeletedAsync repository method"
```

---

## Chunk 2: Enhance Excel Export

### Task 3: Add missing columns to ExportGamesToExcelCommand

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ExportGamesToExcelCommand.cs`

The existing export has: Name, BggId, Status, YearPublished, MinPlayers, MaxPlayers, PlayingTime, HasPdf, CreatedAt.

We need to add: **Id** (for round-trip matching), **Description**, **MinAge**, **ComplexityRating**, **AverageRating**. Also add **GameDataStatus** for admin context.

- [ ] **Step 1: Update the Select projection (around line 61)**

Replace the existing `Select` with:

```csharp
.Select(g => new
{
    g.Id,
    g.Title,
    g.BggId,
    Status = ((GameStatus)g.Status).ToString(),
    GameDataStatus = ((GameDataStatus)g.GameDataStatus).ToString(),
    g.YearPublished,
    g.MinPlayers,
    g.MaxPlayers,
    g.PlayingTimeMinutes,
    g.MinAge,
    g.ComplexityRating,
    g.AverageRating,
    g.Description,
    g.HasUploadedPdf,
    g.CreatedAt
})
```

Note: The `Status` and `GameDataStatus` fields on `SharedGame` are stored as `int` in the DB projection. Cast them to their enum types for `.ToString()`. Check the actual field types — if EF already maps them as enums, you can use `.ToString()` directly. Otherwise use `((GameStatus)g.Status).ToString()`.

- [ ] **Step 2: Update the headers array (around line 88)**

```csharp
var headers = new[]
{
    "Id", "Name", "BggId", "Status", "GameDataStatus",
    "YearPublished", "MinPlayers", "MaxPlayers", "PlayingTime",
    "MinAge", "ComplexityRating", "AverageRating",
    "Description", "HasPdf", "CreatedAt"
};
```

- [ ] **Step 3: Update the data row writing (around line 103)**

```csharp
for (var i = 0; i < games.Count; i++)
{
    var game = games[i];
    var row = i + 2;

    worksheet.Cell(row, 1).Value = game.Id.ToString();
    worksheet.Cell(row, 2).Value = game.Title;
    worksheet.Cell(row, 3).Value = game.BggId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    worksheet.Cell(row, 4).Value = game.Status;
    worksheet.Cell(row, 5).Value = game.GameDataStatus;
    worksheet.Cell(row, 6).Value = game.YearPublished;
    worksheet.Cell(row, 7).Value = game.MinPlayers;
    worksheet.Cell(row, 8).Value = game.MaxPlayers;
    worksheet.Cell(row, 9).Value = game.PlayingTimeMinutes;
    worksheet.Cell(row, 10).Value = game.MinAge;
    worksheet.Cell(row, 11).Value = game.ComplexityRating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    worksheet.Cell(row, 12).Value = game.AverageRating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    worksheet.Cell(row, 13).Value = game.Description;
    worksheet.Cell(row, 14).Value = game.HasUploadedPdf ? "Yes" : "No";
    worksheet.Cell(row, 15).Value = game.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 4: Build to verify**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ExportGamesToExcelCommand.cs
git commit -m "feat(excel-export): add Id, Description, MinAge, ratings to export columns"
```

---

## Chunk 3: Preview Excel Import Query

### Task 4: Create PreviewExcelImportQuery

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/PreviewExcelImportQuery.cs`

This is the core of the feature. It parses the uploaded Excel, compares each row against DB, and returns a categorized diff.

**Matching logic:**
1. Row has `Id` column value → match by Guid (re-imported row)
2. Row has `BggId` → check if BGG ID already exists in DB → if yes, mark as "duplicate BGG ID"
3. Row has only `Name` → check `ExistsByTitleAsync` → if exists, it's a "duplicate title"
4. Otherwise → new game
5. DB games not present in Excel → marked as "removed"
6. Published games are NEVER modified or deleted

**Change detection for existing games:**
Compare Title, YearPublished, MinPlayers, MaxPlayers, PlayingTimeMinutes, MinAge, Description, BggId.

- [ ] **Step 1: Create the query file**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using ClosedXML.Excel;
using FluentValidation;
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

// ── Query ────────────────────────────────────────────────────────────

public sealed record PreviewExcelImportQuery(IFormFile File) : IQuery<ExcelPreviewResult>;

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

        // Name is required
        if (!columns.ContainsKey("Name"))
        {
            return new ExcelPreviewResult(0, [], [], [], [],
                [new ExcelRowError(1, "Name", "Required column 'Name' not found in header row")], 0);
        }

        // ── Load all existing games from DB ──
        var existingGames = await _context.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.BggId,
                g.Status,
                g.GameDataStatus,
                g.YearPublished,
                g.MinPlayers,
                g.MaxPlayers,
                g.PlayingTimeMinutes,
                g.MinAge,
                g.Description
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gameById = existingGames.ToDictionary(g => g.Id);
        var gameByBggId = existingGames
            .Where(g => g.BggId.HasValue)
            .ToDictionary(g => g.BggId!.Value);
        var gameByTitle = existingGames
            .ToDictionary(g => g.Title, StringComparer.OrdinalIgnoreCase);

        var matchedGameIds = new HashSet<Guid>();
        var newGames = new List<ExcelGameRow>();
        var modifiedGames = new List<ExcelGameModification>();
        var duplicateBggIds = new List<ExcelGameRow>();
        var errors = new List<ExcelRowError>();
        var unchangedCount = 0;
        var totalDataRows = 0;

        var lastRow = Math.Min(
            worksheet.LastRowUsed()?.RowNumber() ?? 1,
            MaxRows + 1); // +1 for header

        for (var row = 2; row <= lastRow; row++)
        {
            var title = GetCellString(worksheet, row, columns, "Name");

            // Skip completely empty rows
            if (string.IsNullOrWhiteSpace(title))
                continue;

            totalDataRows++;

            // Parse all fields
            var excelRow = ParseRow(worksheet, row, columns, title, errors);
            if (excelRow is null) continue; // error already added

            // ── Try match by Id first ──
            var idStr = GetCellString(worksheet, row, columns, "Id");
            if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var existingId))
            {
                if (gameById.TryGetValue(existingId, out var existing))
                {
                    matchedGameIds.Add(existingId);

                    // Check for changes (only Draft games can be modified)
                    var changedFields = DetectChanges(existing, excelRow);

                    if (changedFields.Count > 0)
                    {
                        var isProtected = (GameStatus)existing.Status != GameStatus.Draft;
                        modifiedGames.Add(new ExcelGameModification(
                            existingId,
                            existing.Title,
                            ((GameStatus)existing.Status).ToString(),
                            excelRow,
                            isProtected
                                ? [$"PROTECTED (status: {((GameStatus)existing.Status)})", .. changedFields]
                                : changedFields));
                    }
                    else
                    {
                        unchangedCount++;
                    }
                    continue;
                }
                // Id not found in DB → treat as new game (ignore stale Id)
            }

            // ── Check BGG ID duplicate ──
            if (excelRow.BggId.HasValue && gameByBggId.ContainsKey(excelRow.BggId.Value))
            {
                duplicateBggIds.Add(excelRow);
                var matched = gameByBggId[excelRow.BggId.Value];
                matchedGameIds.Add(matched.Id);
                continue;
            }

            // ── Check title duplicate ──
            if (gameByTitle.TryGetValue(title, out var titleMatch))
            {
                matchedGameIds.Add(titleMatch.Id);

                // Existing game matched by title — check for changes
                var changedFields = DetectChanges(titleMatch, excelRow);
                if (changedFields.Count > 0)
                {
                    var isProtected = (GameStatus)titleMatch.Status != GameStatus.Draft;
                    modifiedGames.Add(new ExcelGameModification(
                        titleMatch.Id,
                        titleMatch.Title,
                        ((GameStatus)titleMatch.Status).ToString(),
                        excelRow,
                        isProtected
                            ? [$"PROTECTED (status: {((GameStatus)titleMatch.Status)})", .. changedFields]
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
                ((GameStatus)g.Status).ToString(),
                ((GameDataStatus)g.GameDataStatus).ToString(),
                (GameStatus)g.Status != GameStatus.Draft))
            .ToList();

        _logger.LogInformation(
            "Excel preview: {Total} rows, {New} new, {Modified} modified, {Removed} removed, {Duplicates} duplicate BGG IDs, {Unchanged} unchanged, {Errors} errors",
            totalDataRows, newGames.Count, modifiedGames.Count, removedGames.Count,
            duplicateBggIds.Count, unchangedCount, errors.Count);

        return new ExcelPreviewResult(
            totalDataRows, newGames, modifiedGames, removedGames,
            duplicateBggIds, errors, unchangedCount);
    }

    // ── Helper methods ───────────────────────────────────────────────

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
        return int.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, out var val)
            ? val : null;
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
            if (int.TryParse(bggIdStr, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
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

    private static List<string> DetectChanges(dynamic existing, ExcelGameRow excelRow)
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
```

**Important implementation notes:**
- Uses `MeepleAiDbContext` directly (query-side pattern, same as `ExportGamesToExcelCommand`)
- The `Status` and `GameDataStatus` fields may be mapped as `int` by EF. Check the `SharedGame` configuration to see if EF maps the backing field `_status` as int. The anonymous projection `g.Status` will call the public property which returns `GameStatus` enum. Cast accordingly.
- `ExcelRowError` record is already defined in `ImportGamesFromExcelCommand.cs`. Either reuse it or create a separate one in this file. If reusing, add a `using` for the Commands namespace.

- [ ] **Step 2: Verify ExcelRowError reuse**

Check if `ExcelRowError` from `ImportGamesFromExcelCommand.cs` is accessible. If it's in the same namespace (`Api.BoundedContexts.SharedGameCatalog.Application.Commands`), add a using. If it causes conflicts, define a local `PreviewRowError` instead.

- [ ] **Step 3: Build to verify**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/PreviewExcelImportQuery.cs
git commit -m "feat(excel-import): add PreviewExcelImportQuery with diff detection"
```

---

## Chunk 4: Confirm Excel Import Command

### Task 5: Create ConfirmExcelImportCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ConfirmExcelImportCommand.cs`

This command receives the admin's confirmed selections and applies them:
- Creates new skeleton games
- Updates Draft games with changed fields
- Soft-deletes games marked for removal (only Draft)

- [ ] **Step 1: Create the command file**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

// ── DTOs ─────────────────────────────────────────────────────────────

public sealed record ConfirmExcelImportRequest(
    IReadOnlyList<NewGameRequest> NewGames,
    IReadOnlyList<ModifyGameRequest> ModifiedGames,
    IReadOnlyList<Guid> RemovedGameIds);

public sealed record NewGameRequest(
    string Title,
    int? BggId,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge,
    string? Description);

public sealed record ModifyGameRequest(
    Guid GameId,
    string Title,
    int? BggId,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge,
    string? Description);

public sealed record ConfirmExcelImportResult(
    int Created,
    int Updated,
    int Deleted,
    int ProtectedSkipped,
    IReadOnlyList<string> Errors);

// ── Command ──────────────────────────────────────────────────────────

public sealed record ConfirmExcelImportCommand(
    ConfirmExcelImportRequest Request,
    Guid UserId) : ICommand<ConfirmExcelImportResult>;

// ── Validator ────────────────────────────────────────────────────────

internal sealed class ConfirmExcelImportCommandValidator
    : AbstractValidator<ConfirmExcelImportCommand>
{
    public ConfirmExcelImportCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.NewGames.Count + x.Request.ModifiedGames.Count + x.Request.RemovedGameIds.Count)
            .GreaterThan(0)
            .WithMessage("At least one change must be specified");
    }
}

// ── Handler ──────────────────────────────────────────────────────────

internal sealed class ConfirmExcelImportCommandHandler
    : ICommandHandler<ConfirmExcelImportCommand, ConfirmExcelImportResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConfirmExcelImportCommandHandler> _logger;

    public ConfirmExcelImportCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ConfirmExcelImportCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfirmExcelImportResult> Handle(
        ConfirmExcelImportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var deleted = 0;
        var protectedSkipped = 0;
        var request = command.Request;

        // ── Create new games ──
        foreach (var newGame in request.NewGames)
        {
            try
            {
                var game = SharedGame.CreateSkeleton(
                    newGame.Title, command.UserId, _timeProvider, newGame.BggId);

                await _repository.AddAsync(game, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to create '{newGame.Title}': {ex.Message}");
                _logger.LogWarning(ex, "Failed to create game '{Title}' from Excel confirm", newGame.Title);
            }
        }

        // ── Update modified games (Draft only) ──
        foreach (var mod in request.ModifiedGames)
        {
            try
            {
                var game = await _repository
                    .GetByIdAsync(mod.GameId, cancellationToken)
                    .ConfigureAwait(false);

                if (game is null)
                {
                    errors.Add($"Game {mod.GameId} not found");
                    continue;
                }

                if (game.Status != GameStatus.Draft)
                {
                    protectedSkipped++;
                    continue;
                }

                // Update fields that have values
                // For skeleton games, use UpdateInfo with current values as defaults
                game.UpdateInfo(
                    mod.Title,
                    mod.YearPublished ?? game.YearPublished,
                    !string.IsNullOrWhiteSpace(mod.Description) ? mod.Description : game.Description,
                    mod.MinPlayers ?? game.MinPlayers,
                    mod.MaxPlayers ?? game.MaxPlayers,
                    mod.PlayingTimeMinutes ?? game.PlayingTimeMinutes,
                    mod.MinAge ?? game.MinAge,
                    game.ComplexityRating,
                    game.AverageRating,
                    game.ImageUrl,
                    game.ThumbnailUrl,
                    game.Rules,
                    command.UserId);

                // Assign BGG ID if provided and game is skeleton/failed
                if (mod.BggId.HasValue && !game.BggId.HasValue &&
                    (game.GameDataStatus == Domain.Enums.GameDataStatus.Skeleton ||
                     game.GameDataStatus == Domain.Enums.GameDataStatus.Failed))
                {
                    game.AssignBggId(mod.BggId.Value, command.UserId);
                }

                _repository.Update(game);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                updated++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to update game {mod.GameId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to update game {GameId} from Excel confirm", mod.GameId);
            }
        }

        // ── Soft-delete removed games (Draft only) ──
        foreach (var gameId in request.RemovedGameIds)
        {
            try
            {
                var game = await _repository
                    .GetByIdAsync(gameId, cancellationToken)
                    .ConfigureAwait(false);

                if (game is null)
                {
                    errors.Add($"Game {gameId} not found for deletion");
                    continue;
                }

                if (game.Status != GameStatus.Draft)
                {
                    protectedSkipped++;
                    continue;
                }

                game.Delete(command.UserId);
                _repository.Update(game);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                deleted++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to delete game {gameId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to delete game {GameId} from Excel confirm", gameId);
            }
        }

        _logger.LogInformation(
            "Excel import confirmed: {Created} created, {Updated} updated, {Deleted} deleted, {Protected} protected, {Errors} errors",
            created, updated, deleted, protectedSkipped, errors.Count);

        return new ConfirmExcelImportResult(created, updated, deleted, protectedSkipped, errors);
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ConfirmExcelImportCommand.cs
git commit -m "feat(excel-import): add ConfirmExcelImportCommand for applying diff"
```

---

## Chunk 5: AssignBggId Command

### Task 6: Create AssignBggIdCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AssignBggIdCommand.cs`

- [ ] **Step 1: Create the command file**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

public sealed record AssignBggIdRequest(Guid SharedGameId, int BggId);

public sealed record AssignBggIdCommand(
    Guid SharedGameId, int BggId, Guid UserId) : ICommand<bool>;

internal sealed class AssignBggIdCommandValidator : AbstractValidator<AssignBggIdCommand>
{
    public AssignBggIdCommandValidator()
    {
        RuleFor(x => x.SharedGameId).NotEmpty();
        RuleFor(x => x.BggId).GreaterThan(0);
        RuleFor(x => x.UserId).NotEmpty();
    }
}

internal sealed class AssignBggIdCommandHandler
    : ICommandHandler<AssignBggIdCommand, bool>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssignBggIdCommandHandler> _logger;

    public AssignBggIdCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AssignBggIdCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        AssignBggIdCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check if another game already has this BGG ID
        if (await _repository.ExistsByBggIdAsync(command.BggId, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("BGG ID {BggId} already assigned to another game", command.BggId);
            return false;
        }

        var game = await _repository
            .GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            _logger.LogWarning("Game {GameId} not found for BGG ID assignment", command.SharedGameId);
            return false;
        }

        game.AssignBggId(command.BggId, command.UserId);
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Assigned BGG ID {BggId} to game '{Title}' ({GameId})",
            command.BggId, game.Title, game.Id);

        return true;
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AssignBggIdCommand.cs
git commit -m "feat(shared-game): add AssignBggIdCommand for skeleton games"
```

---

## Chunk 6: Endpoints

### Task 7: Add endpoints to AdminCatalogIngestionEndpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs`

Add 3 new endpoints after the existing `/excel-export` endpoint:

- [ ] **Step 1: Add `POST /excel-preview` endpoint**

Add before `return group;` at the end of the method:

```csharp
// POST /api/v1/admin/catalog-ingestion/excel-preview
// Upload Excel and get a diff preview (new/modified/removed games)
group.MapPost("/excel-preview", async (
    IFormFile file,
    HttpContext context,
    IMediator mediator,
    CancellationToken ct) =>
{
    var (authorized, _, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var result = await mediator.Send(
        new PreviewExcelImportQuery(file), ct).ConfigureAwait(false);

    return Results.Ok(result);
})
.DisableAntiforgery()
.WithName("ExcelPreview")
.WithOpenApi(operation =>
{
    operation.Summary = "Preview Excel import diff";
    operation.Description = "Parses .xlsx file, compares with existing catalog, returns categorized diff (new/modified/removed/unchanged). Does not modify data.";
    return operation;
});
```

- [ ] **Step 2: Add `POST /excel-confirm` endpoint**

```csharp
// POST /api/v1/admin/catalog-ingestion/excel-confirm
// Apply confirmed Excel import changes (create/update/delete)
group.MapPost("/excel-confirm", async (
    ConfirmExcelImportRequest request,
    HttpContext context,
    IMediator mediator,
    CancellationToken ct) =>
{
    var (authorized, session, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var result = await mediator.Send(
        new ConfirmExcelImportCommand(request, session!.User!.Id), ct).ConfigureAwait(false);

    return Results.Ok(result);
})
.WithName("ExcelConfirm")
.WithOpenApi(operation =>
{
    operation.Summary = "Confirm Excel import changes";
    operation.Description = "Applies previewed changes: creates new games, updates Draft games, soft-deletes removed Draft games. Published games are protected.";
    return operation;
});
```

- [ ] **Step 3: Add `POST /assign-bgg-id` endpoint**

```csharp
// POST /api/v1/admin/catalog-ingestion/assign-bgg-id
// Assign a BGG ID to a skeleton game
group.MapPost("/assign-bgg-id", async (
    AssignBggIdRequest request,
    HttpContext context,
    IMediator mediator,
    CancellationToken ct) =>
{
    var (authorized, session, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var success = await mediator.Send(
        new AssignBggIdCommand(request.SharedGameId, request.BggId, session!.User!.Id), ct)
        .ConfigureAwait(false);

    return success
        ? Results.Ok(new { assigned = true })
        : Results.BadRequest(new { assigned = false, error = "BGG ID already in use or game not found/eligible" });
})
.WithName("AssignBggId")
.WithOpenApi(operation =>
{
    operation.Summary = "Assign BGG ID to skeleton game";
    operation.Description = "Sets BGG ID on a Skeleton/Failed game, enabling BGG enrichment. Rejects if BGG ID is already assigned to another game.";
    return operation;
});
```

- [ ] **Step 4: Add required usings at the top of the file**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
```

Verify that `ConfirmExcelImportRequest` and `AssignBggIdRequest` are accessible from the Commands namespace (already imported).

- [ ] **Step 5: Build to verify**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/AdminCatalogIngestionEndpoints.cs
git commit -m "feat(admin): add excel-preview, excel-confirm, assign-bgg-id endpoints"
```

---

## Chunk 7: Integration Verification

### Task 8: Build and verify all endpoints

- [ ] **Step 1: Full build**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded with 0 errors

- [ ] **Step 2: Verify endpoint registration**

Check that `AdminCatalogIngestionEndpoints` is registered in the app's routing configuration. Search for `MapAdminCatalogIngestionEndpoints` in the routing setup files.

- [ ] **Step 3: Run existing tests**

Run: `cd apps/api/src/Api && dotnet test --filter "SharedGameCatalog" --no-build`
Expected: All existing tests pass (no regressions)

- [ ] **Step 4: Final commit with all changes**

If any fixups were needed during verification:

```bash
git add -A
git commit -m "fix: resolve build issues from excel management feature"
```

---

## API Summary

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/v1/admin/catalog-ingestion/excel-export` | GET | Download catalog as Excel (enhanced) |
| `/api/v1/admin/catalog-ingestion/excel-preview` | POST | Upload Excel → get diff preview |
| `/api/v1/admin/catalog-ingestion/excel-confirm` | POST | Apply confirmed changes |
| `/api/v1/admin/catalog-ingestion/assign-bgg-id` | POST | Assign BGG ID to skeleton game |
| `/api/v1/admin/catalog-ingestion/enqueue-enrichment` | POST | Enqueue for BGG enrichment (existing) |
| `/api/v1/bgg/search?q={term}` | GET | Search BGG by name (existing) |

## Flow Diagram

```
Admin: Scarica Excel → GET /excel-export
       ↓
Admin: Modifica Excel offline (aggiunge/modifica/rimuove righe)
       ↓
Admin: Carica Excel → POST /excel-preview
       ↓
Sistema: Ritorna preview {new, modified, removed, duplicates, unchanged}
       ↓
Admin: Per ogni nuovo gioco senza BGG ID:
       → Cerca BGG per nome: GET /bgg/search?q=Catan
       → Assegna BGG ID: POST /assign-bgg-id
       ↓
Admin: Conferma import → POST /excel-confirm
       ↓
Sistema: Crea nuovi skeleton, aggiorna Draft, soft-delete Draft
       ↓
Admin: Arricchisci da BGG → POST /enqueue-enrichment (esistente)
       ↓
Background: BggImportQueueService processa a 1 req/sec
```
