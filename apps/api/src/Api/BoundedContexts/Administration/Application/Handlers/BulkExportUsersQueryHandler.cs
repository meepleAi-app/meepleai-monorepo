using System.Globalization;
using System.Text;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for bulk user export to CSV.
/// Returns CSV string with format: email,displayName,role,createdAt
/// </summary>
internal class BulkExportUsersQueryHandler : IQueryHandler<BulkExportUsersQuery, string>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<BulkExportUsersQueryHandler> _logger;

    public BulkExportUsersQueryHandler(
        IUserRepository userRepository,
        ILogger<BulkExportUsersQueryHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> Handle(BulkExportUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _logger.LogInformation("Exporting users to CSV with filters: Role={Role}, SearchTerm={SearchTerm}",
            query.Role, query.SearchTerm);

        // Get all users
        var users = await _userRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleFilter = Role.Parse(query.Role);
            users = users.Where(u => u.Role.Value.Equals(roleFilter.Value, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchLower = query.SearchTerm.ToLowerInvariant();
            users = users.Where(u =>
                u.Email.Value.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                u.DisplayName.Contains(searchLower, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // Generate CSV
        var csv = new StringBuilder();
        csv.AppendLine("email,displayName,role,createdAt");

        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        foreach (var user in users)
        {
            var email = EscapeCsvField(user.Email.Value);
            var displayName = EscapeCsvField(user.DisplayName);
            var role = user.Role.Value;
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
