using System.Globalization;
using System.Text;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for bulk user export to CSV.
/// Returns CSV string with format: email,displayName,role,createdAt
/// </summary>
internal class BulkExportUsersQueryHandler : IQueryHandler<BulkExportUsersQuery, string>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<BulkExportUsersQueryHandler> _logger;

    public BulkExportUsersQueryHandler(
        IUserProfileRepository userProfileRepository,
        ILogger<BulkExportUsersQueryHandler> logger)
    {
        _userProfileRepository = userProfileRepository ?? throw new ArgumentNullException(nameof(userProfileRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> Handle(BulkExportUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _logger.LogInformation("Exporting users to CSV with filters: Role={Role}, SearchTerm={SearchTerm}",
            query.Role, query.SearchTerm);

        // Get all users
        var users = await _userProfileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            users = users.Where(u => u.Role.Equals(query.Role, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchLower = query.SearchTerm.ToLowerInvariant();
            users = users.Where(u =>
                u.Email.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                (u.DisplayName != null && u.DisplayName.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Generate CSV
        var csv = new StringBuilder();
        csv.AppendLine("email,displayName,role,createdAt");

        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        foreach (var user in users)
        {
            var email = EscapeCsvField(user.Email);
            var displayName = EscapeCsvField(user.DisplayName ?? string.Empty);
            var role = user.Role;
            var createdAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            csv.AppendLine($"{email},{displayName},{role},{createdAt}");
        }

        _logger.LogInformation("Exported {Count} users to CSV", users.Count);
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
