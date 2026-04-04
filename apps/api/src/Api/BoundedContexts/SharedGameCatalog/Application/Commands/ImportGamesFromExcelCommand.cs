using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using ClosedXML.Excel;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to import games from an Excel file, creating skeleton SharedGame entries.
/// Issue: Admin Bulk Excel Import.
/// </summary>
public sealed record ImportGamesFromExcelCommand(
    IFormFile File, Guid UserId) : ICommand<ExcelImportResult>;

public sealed record ExcelImportResult(
    int Total, int Created, int Duplicates, int Errors,
    IReadOnlyList<ExcelRowError> RowErrors);

public sealed record ExcelRowError(int RowNumber, string? ColumnName, string ErrorMessage);

/// <summary>
/// Validator for ImportGamesFromExcelCommand.
/// </summary>
internal sealed class ImportGamesFromExcelCommandValidator : AbstractValidator<ImportGamesFromExcelCommand>
{
    public ImportGamesFromExcelCommandValidator()
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
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required");
    }
}

/// <summary>
/// Handler for ImportGamesFromExcelCommand.
/// Parses Excel, validates rows, deduplicates against DB, creates skeleton games.
/// </summary>
internal sealed class ImportGamesFromExcelCommandHandler
    : ICommandHandler<ImportGamesFromExcelCommand, ExcelImportResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ImportGamesFromExcelCommandHandler> _logger;

    private const int MaxNameLength = 500;

    public ImportGamesFromExcelCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ImportGamesFromExcelCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExcelImportResult> Handle(
        ImportGamesFromExcelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var workbook = new XLWorkbook(command.File.OpenReadStream());
        var worksheet = workbook.Worksheets.First();

        // Find header columns
        var headerRow = worksheet.Row(1);
        int nameCol = -1, bggIdCol = -1;

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (header.Equals("Name", StringComparison.OrdinalIgnoreCase))
                nameCol = cell.Address.ColumnNumber;
            else if (header.Equals("BggId", StringComparison.OrdinalIgnoreCase))
                bggIdCol = cell.Address.ColumnNumber;
        }

        if (nameCol == -1)
        {
            return new ExcelImportResult(0, 0, 0, 1,
                [new ExcelRowError(1, "Name", "Required column 'Name' not found in header row")]);
        }

        var created = 0;
        var duplicates = 0;
        var errors = 0;
        var rowErrors = new List<ExcelRowError>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBggIds = new HashSet<int>();
        var totalDataRows = 0;

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var nameValue = worksheet.Cell(row, nameCol).GetString().Trim();

            // Skip completely empty rows
            if (string.IsNullOrWhiteSpace(nameValue) &&
                (bggIdCol == -1 || string.IsNullOrWhiteSpace(worksheet.Cell(row, bggIdCol).GetString())))
                continue;

            totalDataRows++;

            // Validate name
            if (string.IsNullOrWhiteSpace(nameValue))
            {
                errors++;
                rowErrors.Add(new ExcelRowError(row, "Name", "Name is required"));
                continue;
            }

            if (nameValue.Length > MaxNameLength)
            {
                errors++;
                rowErrors.Add(new ExcelRowError(row, "Name", $"Name exceeds {MaxNameLength} characters"));
                continue;
            }

            // Parse BggId (optional)
            int? bggId = null;
            if (bggIdCol != -1)
            {
                var bggIdValue = worksheet.Cell(row, bggIdCol).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(bggIdValue))
                {
                    if (int.TryParse(bggIdValue, System.Globalization.CultureInfo.InvariantCulture, out var parsedId))
                    {
                        if (parsedId <= 0)
                        {
                            errors++;
                            rowErrors.Add(new ExcelRowError(row, "BggId", "BggId must be a positive integer"));
                            continue;
                        }
                        bggId = parsedId;
                    }
                    else
                    {
                        errors++;
                        rowErrors.Add(new ExcelRowError(row, "BggId", "BggId must be a valid integer"));
                        continue;
                    }
                }
            }

            // Intra-file duplicate check
            if (!seenNames.Add(nameValue))
            {
                duplicates++;
                continue;
            }

            if (bggId.HasValue && !seenBggIds.Add(bggId.Value))
            {
                duplicates++;
                continue;
            }

            // DB duplicate check
            if (await _repository.ExistsByTitleAsync(nameValue, cancellationToken).ConfigureAwait(false))
            {
                duplicates++;
                continue;
            }

            if (bggId.HasValue &&
                await _repository.ExistsByBggIdAsync(bggId.Value, cancellationToken).ConfigureAwait(false))
            {
                duplicates++;
                continue;
            }

            // Create skeleton game
            try
            {
                var game = Domain.Aggregates.SharedGame.CreateSkeleton(
                    nameValue, command.UserId, _timeProvider, bggId);

                await _repository.AddAsync(game, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                created++;

                _logger.LogInformation(
                    "Created skeleton game '{Title}' (BggId={BggId}) from Excel row {Row}",
                    nameValue, bggId, row);
            }
            catch (Exception ex)
            {
                errors++;
                rowErrors.Add(new ExcelRowError(row, null, $"Failed to create game: {ex.Message}"));
                _logger.LogWarning(ex,
                    "Failed to create skeleton game '{Title}' from Excel row {Row}",
                    nameValue, row);
            }
        }

        _logger.LogInformation(
            "Excel import completed: {Total} rows, {Created} created, {Duplicates} duplicates, {Errors} errors",
            totalDataRows, created, duplicates, errors);

        return new ExcelImportResult(totalDataRows, created, duplicates, errors, rowErrors);
    }
}
