using System.Globalization;
using System.Text;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.ApiKeys;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// Handler for bulk API key import from CSV.
/// CSV format: userId,keyName,scopes,expiresAt,metadata
/// Generates new API keys for security and returns plaintext keys (shown once).
/// </summary>
internal class BulkImportApiKeysCommandHandler : ICommandHandler<BulkImportApiKeysCommand, BulkOperationResult<ApiKeyImportResultDto>>
{
    private const int MaxBulkSize = 1000;
    private const int MaxCsvSizeBytes = 10 * 1024 * 1024; // 10MB
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkImportApiKeysCommandHandler> _logger;

    public BulkImportApiKeysCommandHandler(
        IApiKeyRepository apiKeyRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<BulkImportApiKeysCommandHandler> logger)
    {
        _apiKeyRepository = apiKeyRepository ?? throw new ArgumentNullException(nameof(apiKeyRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkOperationResult<ApiKeyImportResultDto>> Handle(BulkImportApiKeysCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin {RequesterId} initiating bulk API key import from CSV",
            command.RequesterId);

        try
        {
            // Step 1: Validate CSV and parse records
            var keyRecords = await ValidateCsvAndParseAsync(command.CsvContent, cancellationToken).ConfigureAwait(false);

            // Step 2: Validate all user IDs exist
            await ValidateUserExistenceAsync(keyRecords, cancellationToken).ConfigureAwait(false);

            // Step 3: Validate no duplicates (in CSV or database)
            await ValidateDuplicatesAsync(keyRecords, cancellationToken).ConfigureAwait(false);

            // Step 4: Process all API key imports
            var (successCount, errors, importedKeys) = await ProcessApiKeyImportsAsync(
                keyRecords, cancellationToken).ConfigureAwait(false);

            // Commit transaction if any success
            if (successCount > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Bulk API key import completed: {SuccessCount} succeeded, {FailedCount} failed",
                    successCount, errors.Count);
            }
            else
            {
                _logger.LogWarning("Bulk API key import failed: no keys created");
            }

            return new BulkOperationResult<ApiKeyImportResultDto>(
                TotalRequested: keyRecords.Count,
                SuccessCount: successCount,
                FailedCount: errors.Count,
                Errors: errors,
                Data: importedKeys
            );
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during bulk API key import");
            throw new DomainException($"Bulk API key import failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates CSV content and parses API key records.
    /// </summary>
    private async Task<List<ApiKeyImportRecord>> ValidateCsvAndParseAsync(
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
        var keyRecords = ParseCsv(csvContent, errors);

        if (keyRecords.Count > MaxBulkSize)
        {
            throw new DomainException($"Bulk operation exceeds maximum limit of {MaxBulkSize} API keys");
        }

        return await Task.FromResult(keyRecords).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates that all user IDs in import records exist in database.
    /// </summary>
    private async Task ValidateUserExistenceAsync(
        List<ApiKeyImportRecord> keyRecords,
        CancellationToken cancellationToken)
    {
        var userIds = keyRecords.Select(r => r.UserId).Distinct().ToList();
        var existingUserIds = new HashSet<Guid>();

        foreach (var userId in userIds)
        {
            var userExists = await _userRepository.ExistsAsync(userId, cancellationToken).ConfigureAwait(false);
            if (userExists)
            {
                existingUserIds.Add(userId);
            }
        }

        var missingUserIds = userIds.Except(existingUserIds).ToList();
        if (missingUserIds.Any())
        {
            throw new DomainException($"The following user IDs do not exist: {string.Join(", ", missingUserIds)}");
        }
    }

    /// <summary>
    /// Validates no duplicate key names exist in CSV or database.
    /// </summary>
    private async Task ValidateDuplicatesAsync(
        List<ApiKeyImportRecord> keyRecords,
        CancellationToken cancellationToken)
    {
        // Check for duplicate key names per user in CSV
        var duplicateKeys = keyRecords
            .GroupBy(k => new { k.UserId, KeyName = k.KeyName.ToLowerInvariant() }, (key, group) => new { key.UserId, key.KeyName, Count = group.Count() })
            .Where(g => g.Count > 1)
            .Select(g => $"{g.KeyName} (User: {g.UserId})")
            .ToList();

        if (duplicateKeys.Any())
        {
            throw new DomainException($"CSV contains duplicate key names for same user: {string.Join(", ", duplicateKeys)}");
        }

        // Check for existing key names per user in database
        var existingKeyNames = new List<string>();
        foreach (var record in keyRecords)
        {
            var userKeys = await _apiKeyRepository.GetByUserIdAsync(record.UserId, cancellationToken).ConfigureAwait(false);
            var nameExists = userKeys.Any(k => k.KeyName.Equals(record.KeyName, StringComparison.OrdinalIgnoreCase));
            if (nameExists)
            {
                existingKeyNames.Add($"{record.KeyName} (User: {record.UserId})");
            }
        }

        if (existingKeyNames.Any())
        {
            throw new DomainException($"The following key names already exist for their users: {string.Join(", ", existingKeyNames)}");
        }
    }

    /// <summary>
    /// Processes all API key import records and creates keys.
    /// Returns (successCount, errors, importedKeys).
    /// </summary>
    private async Task<(int successCount, List<string> errors, List<ApiKeyImportResultDto> importedKeys)> ProcessApiKeyImportsAsync(
        List<ApiKeyImportRecord> keyRecords,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var successCount = 0;
        var lineNumber = 0;
        var importedKeys = new List<ApiKeyImportResultDto>();

        foreach (var record in keyRecords)
        {
            lineNumber++;
            try
            {
                // Generate new API key with plaintext key
                var (apiKey, plaintextKey) = ApiKey.Create(
                    id: Guid.NewGuid(),
                    userId: record.UserId,
                    keyName: record.KeyName.Trim(),
                    scopes: record.Scopes,
                    expiresAt: record.ExpiresAt,
                    metadata: record.Metadata
                );

                await _apiKeyRepository.AddAsync(apiKey, cancellationToken).ConfigureAwait(false);

                importedKeys.Add(new ApiKeyImportResultDto(
                    Id: apiKey.Id,
                    KeyName: apiKey.KeyName,
                    PlaintextKey: plaintextKey,
                    UserId: apiKey.UserId,
                    Scopes: apiKey.Scopes,
                    ExpiresAt: apiKey.ExpiresAt
                ));

                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing API key at line {LineNumber}", lineNumber);
                errors.Add($"Line {lineNumber} ({record.KeyName}): {ex.Message}");
            }
        }

        return (successCount, errors, importedKeys);
    }

    private static List<ApiKeyImportRecord> ParseCsv(string csvContent, List<string> errors)
    {
        var records = new List<ApiKeyImportRecord>();
        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            throw new DomainException("CSV file is empty");
        }

        // Validate header
        var header = lines[0].Split(',');
        if (header.Length != 5 ||
            !header[0].Equals("userId", StringComparison.OrdinalIgnoreCase) ||
            !header[1].Equals("keyName", StringComparison.OrdinalIgnoreCase) ||
            !header[2].Equals("scopes", StringComparison.OrdinalIgnoreCase) ||
            !header[3].Equals("expiresAt", StringComparison.OrdinalIgnoreCase) ||
            !header[4].Equals("metadata", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Invalid CSV header. Expected: userId,keyName,scopes,expiresAt,metadata");
        }

        // Parse data rows
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseCsvLine(line);
            if (fields.Count != 5)
            {
                errors.Add($"Line {i + 1}: Invalid format (expected 5 fields, got {fields.Count})");
                continue;
            }

            var userIdStr = fields[0].Trim();
            var keyName = fields[1].Trim();
            var scopes = fields[2].Trim();
            var expiresAtStr = fields[3].Trim();
            var metadata = fields[4].Trim();

            // Basic validation
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                errors.Add($"Line {i + 1}: Invalid user ID format");
                continue;
            }

            if (string.IsNullOrWhiteSpace(keyName))
            {
                errors.Add($"Line {i + 1}: Key name is required");
                continue;
            }

            if (string.IsNullOrWhiteSpace(scopes))
            {
                errors.Add($"Line {i + 1}: Scopes are required");
                continue;
            }

            DateTime? expiresAt = null;
            if (!string.IsNullOrWhiteSpace(expiresAtStr))
            {
                if (DateTime.TryParseExact(expiresAtStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate))
                {
                    expiresAt = parsedDate.ToUniversalTime();

                    // Validate expiry is in the future
                    if (expiresAt <= DateTime.UtcNow)
                    {
                        errors.Add($"Line {i + 1}: Expiry date must be in the future");
                        continue;
                    }
                }
                else
                {
                    errors.Add($"Line {i + 1}: Invalid expiry date format (expected: yyyy-MM-dd HH:mm:ss)");
                    continue;
                }
            }

            // Metadata can be null or empty
            var metadataValue = string.IsNullOrWhiteSpace(metadata) || metadata.Equals("null", StringComparison.OrdinalIgnoreCase)
                ? null
                : metadata;

            records.Add(new ApiKeyImportRecord(userId, keyName, scopes, expiresAt, metadataValue));
        }

        return records;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        var insideQuotes = false;

        // S127: Use while loop to avoid modifying loop counter in body
        int i = 0;
        while (i < line.Length)
        {
            var c = line[i];

            if (c == '"')
            {
                if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentField.Append('"');
                    i++; // Skip next quote (safe in while loop)
                }
                else
                {
                    // Toggle quotes
                    insideQuotes = !insideQuotes;
                }
            }
            else if (c == ',' && !insideQuotes)
            {
                // Field separator
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }

            i++; // Advance to next character
        }

        // Add last field
        fields.Add(currentField.ToString());

        return fields;
    }

    private sealed record ApiKeyImportRecord(Guid UserId, string KeyName, string Scopes, DateTime? ExpiresAt, string? Metadata);
}
