using System.Globalization;
using System.Text;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for bulk user import from CSV.
/// CSV format: email,displayName,role,password
/// </summary>
internal class BulkImportUsersCommandHandler : ICommandHandler<BulkImportUsersCommand, BulkOperationResult>
{
    private const int MaxBulkSize = 1000;
    private const int MaxCsvSizeBytes = 10 * 1024 * 1024; // 10MB
    private static readonly char[] NewLineSeparators = { '\r', '\n' };
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkImportUsersCommandHandler> _logger;

    public BulkImportUsersCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<BulkImportUsersCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkOperationResult> Handle(BulkImportUsersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _logger.LogInformation("Admin {RequesterId} initiating bulk user import from CSV",
            command.RequesterId);

        try
        {
            // Step 1: Validate CSV and parse user records
            var userRecords = await ValidateCsvAndParseUsersAsync(command.CsvContent).ConfigureAwait(false);

            // Step 2: Validate no duplicates (in CSV or database)
            await ValidateUserDuplicatesAsync(userRecords, cancellationToken).ConfigureAwait(false);

            // Step 3: Process all user imports
            var (successCount, errors) = await ProcessUserImportsAsync(
                userRecords, cancellationToken).ConfigureAwait(false);

            // Commit transaction if any success
            if (successCount > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Bulk user import completed: {SuccessCount} succeeded, {FailedCount} failed",
                    successCount, errors.Count);
            }
            else
            {
                _logger.LogWarning("Bulk user import failed: no users created");
            }

            return new BulkOperationResult(
                TotalRequested: userRecords.Count,
                SuccessCount: successCount,
                FailedCount: errors.Count,
                Errors: errors
            );
        }
        catch (DomainException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - Wraps unexpected infrastructure failures
        // DomainException caught separately above and rethrown.
        // Generic catch wraps unexpected exceptions (DB, network, memory) in DomainException
        // for consistent API error handling. Logs with full context before wrapping.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during bulk user import");
            throw new DomainException($"Bulk user import failed: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Validates CSV content and parses user records.
    /// </summary>
    private async Task<List<UserImportRecord>> ValidateCsvAndParseUsersAsync(
        string csvContent
                )
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            throw new DomainException("CSV content cannot be null or empty");
        }

        var csvSizeBytes = Encoding.UTF8.GetByteCount(csvContent);
        if (csvSizeBytes > MaxCsvSizeBytes)
        {
            throw new DomainException($"CSV file size ({csvSizeBytes} bytes) exceeds maximum limit of {MaxCsvSizeBytes} bytes");
        }

        var errors = new List<string>();
        var userRecords = ParseCsv(csvContent, errors);

        if (userRecords.Count > MaxBulkSize)
        {
            throw new DomainException($"Bulk operation exceeds maximum limit of {MaxBulkSize} users");
        }

        return await Task.FromResult(userRecords).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates no duplicate emails exist in CSV or database.
    /// </summary>
    private async Task ValidateUserDuplicatesAsync(
        List<UserImportRecord> userRecords,
        CancellationToken cancellationToken)
    {
        // Check for duplicate emails in CSV
        var duplicateEmails = userRecords
            .GroupBy(u => u.Email.ToLowerInvariant(), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateEmails.Count > 0)
        {
            throw new DomainException($"CSV contains duplicate emails: {string.Join(", ", duplicateEmails)}");
        }

        // Check for existing users in database
        var existingEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in userRecords)
        {
            var email = new Email(record.Email);
            var exists = await _userRepository.ExistsByEmailAsync(email, cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                existingEmails.Add(record.Email);
            }
        }

        if (existingEmails.Count > 0)
        {
            throw new DomainException($"The following emails already exist: {string.Join(", ", existingEmails)}");
        }
    }

    /// <summary>
    /// Processes all user import records and creates users.
    /// Returns (successCount, errors).
    /// </summary>
    private async Task<(int successCount, List<string> errors)> ProcessUserImportsAsync(
        List<UserImportRecord> userRecords,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var successCount = 0;
        var lineNumber = 0;

        foreach (var record in userRecords)
        {
            lineNumber++;
            try
            {
                var email = new Email(record.Email);
                var role = Role.Parse(record.Role);
                var passwordHash = PasswordHash.Create(record.Password);

                var user = new User(
                    id: Guid.NewGuid(),
                    email: email,
                    displayName: record.DisplayName.Trim(),
                    passwordHash: passwordHash,
                    role: role
                );

                await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
                successCount++;
            }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // HANDLER BOUNDARY: BULK OPERATION PATTERN - Individual import failure handling
            // Catches all exceptions during user creation (validation, DB constraints, etc.)
            // to collect errors without stopping batch processing. Each failure is logged
            // and added to error list for reporting. Allows partial success in bulk import.
#pragma warning restore S125
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing user at line {LineNumber}", lineNumber);
                errors.Add($"Line {lineNumber} ({record.Email}): {ex.Message}");
            }
#pragma warning restore CA1031
        }

        return (successCount, errors);
    }

    private static List<UserImportRecord> ParseCsv(string csvContent, List<string> errors)
    {
        var records = new List<UserImportRecord>();
        var lines = csvContent.Split(NewLineSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            throw new DomainException("CSV file is empty");
        }

        // Validate header
        var header = lines[0].Split(',');
        if (header.Length != 4 ||
            !header[0].Equals("email", StringComparison.OrdinalIgnoreCase) ||
            !header[1].Equals("displayName", StringComparison.OrdinalIgnoreCase) ||
            !header[2].Equals("role", StringComparison.OrdinalIgnoreCase) ||
            !header[3].Equals("password", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Invalid CSV header. Expected: email,displayName,role,password");
        }

        // Parse data rows
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = line.Split(',');
            if (fields.Length != 4)
            {
                errors.Add($"Line {i + 1}: Invalid format (expected 4 fields, got {fields.Length})");
                continue;
            }

            var email = fields[0].Trim();
            var displayName = fields[1].Trim();
            var role = fields[2].Trim();
            var password = fields[3].Trim();

            // Basic validation
            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add($"Line {i + 1}: Email is required");
                continue;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add($"Line {i + 1}: Display name is required");
                continue;
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                errors.Add($"Line {i + 1}: Role is required");
                continue;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                errors.Add($"Line {i + 1}: Password must be at least 8 characters");
                continue;
            }

            records.Add(new UserImportRecord(email, displayName, role, password));
        }

        return records;
    }

    private sealed record UserImportRecord(string Email, string DisplayName, string Role, string Password);
}
