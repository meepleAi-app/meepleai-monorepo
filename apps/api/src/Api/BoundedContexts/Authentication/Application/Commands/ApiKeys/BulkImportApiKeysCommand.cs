using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.ApiKeys;

/// <summary>
/// Command to import multiple API keys from CSV data.
/// CSV format: userId,keyName,scopes,expiresAt,metadata
/// Generates new API keys for each record and returns plaintext keys (shown once).
/// </summary>
/// <param name="CsvContent">CSV file content as string.</param>
/// <param name="RequesterId">The ID of the admin requesting the operation.</param>
public record BulkImportApiKeysCommand(
    string CsvContent,
    Guid RequesterId
) : ICommand<BulkOperationResult<ApiKeyImportResultDto>>;

/// <summary>
/// Result DTO for imported API key with plaintext key.
/// </summary>
public record ApiKeyImportResultDto(
    Guid Id,
    string KeyName,
    string PlaintextKey,
    Guid UserId,
    string Scopes,
    DateTime? ExpiresAt
);
