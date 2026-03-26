using System.Globalization;
using System.Text;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for bulk API key export to CSV.
/// Returns CSV string with metadata only (no actual keys for security).
/// Format: userId,keyName,scopes,expiresAt,metadata
/// </summary>
internal class BulkExportApiKeysQueryHandler : IQueryHandler<BulkExportApiKeysQuery, string>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ILogger<BulkExportApiKeysQueryHandler> _logger;

    public BulkExportApiKeysQueryHandler(
        IApiKeyRepository apiKeyRepository,
        ILogger<BulkExportApiKeysQueryHandler> logger)
    {
        _apiKeyRepository = apiKeyRepository ?? throw new ArgumentNullException(nameof(apiKeyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> Handle(BulkExportApiKeysQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _logger.LogInformation("Exporting API keys to CSV with filters: UserId={UserId}, IsActive={IsActive}, SearchTerm={SearchTerm}",
            query.UserId, query.IsActive, query.SearchTerm);

        // Get all API keys
        var apiKeys = await _apiKeyRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Apply filters
        if (query.UserId.HasValue)
        {
            apiKeys = apiKeys.Where(k => k.UserId == query.UserId.Value).ToList();
        }

        if (query.IsActive.HasValue)
        {
            var now = DateTime.UtcNow;
            if (query.IsActive.Value)
            {
                // Active = not revoked AND not expired
                apiKeys = apiKeys.Where(k =>
                    k.IsActive &&
                    k.RevokedAt == null &&
                    (!k.ExpiresAt.HasValue || k.ExpiresAt.Value > now)
                ).ToList();
            }
            else
            {
                // Inactive = revoked OR expired OR explicitly inactive
                apiKeys = apiKeys.Where(k =>
                    !k.IsActive ||
                    k.RevokedAt != null ||
                    (k.ExpiresAt.HasValue && k.ExpiresAt.Value <= now)
                ).ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchLower = query.SearchTerm.ToLowerInvariant();
            apiKeys = apiKeys.Where(k =>
                k.KeyName.Contains(searchLower, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // Generate CSV
        var csv = new StringBuilder();
        csv.AppendLine("userId,keyName,scopes,expiresAt,metadata");

        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        foreach (var apiKey in apiKeys)
        {
            var userId = apiKey.UserId.ToString("D", CultureInfo.InvariantCulture);
            var keyName = EscapeCsvField(apiKey.KeyName);
            var scopes = EscapeCsvField(apiKey.Scopes);
            var expiresAt = apiKey.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
            var metadata = EscapeCsvField(apiKey.Metadata ?? string.Empty);

            csv.AppendLine($"{userId},{keyName},{scopes},{expiresAt},{metadata}");
        }

        _logger.LogInformation("Exported {Count} API keys to CSV", apiKeys.Count);
        return csv.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
