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
public class BulkImportUsersCommandHandler : ICommandHandler<BulkImportUsersCommand, BulkOperationResult>
{
    private const int MaxBulkSize = 1000;
    private const int MaxCsvSizeBytes = 10 * 1024 * 1024; // 10MB
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
        // Validation: Check CSV size
        if (string.IsNullOrWhiteSpace(command.CsvContent))
        {
            throw new DomainException("CSV content cannot be null or empty");
        }

        var csvSizeBytes = Encoding.UTF8.GetByteCount(command.CsvContent);
        if (csvSizeBytes > MaxCsvSizeBytes)
        {
            throw new DomainException($"CSV file size ({csvSizeBytes} bytes) exceeds maximum limit of {MaxCsvSizeBytes} bytes");
        }

        _logger.LogInformation("Admin {RequesterId} initiating bulk user import from CSV ({Size} bytes)",
            command.RequesterId, csvSizeBytes);

        var errors = new List<string>();
        var successCount = 0;
        var lineNumber = 0;

        try
        {
            // Parse CSV
            var userRecords = ParseCsv(command.CsvContent, errors);

            // Validation: Check bulk size limit
            if (userRecords.Count > MaxBulkSize)
            {
                throw new DomainException($"Bulk operation exceeds maximum limit of {MaxBulkSize} users");
            }

            // Check for duplicate emails in CSV
            var duplicateEmails = userRecords
                .GroupBy(u => u.Email.ToLowerInvariant(), StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateEmails.Any())
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

            if (existingEmails.Any())
            {
                throw new DomainException($"The following emails already exist: {string.Join(", ", existingEmails)}");
            }

            // Process all users in a single transaction
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing user at line {LineNumber}", lineNumber);
                    errors.Add($"Line {lineNumber} ({record.Email}): {ex.Message}");
                }
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during bulk user import");
            throw new DomainException($"Bulk user import failed: {ex.Message}", ex);
        }
    }

    private static List<UserImportRecord> ParseCsv(string csvContent, List<string> errors)
    {
        var records = new List<UserImportRecord>();
        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

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

    private record UserImportRecord(string Email, string DisplayName, string Role, string Password);
}
