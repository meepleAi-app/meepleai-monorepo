using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to export users to CSV format based on filters.
/// </summary>
/// <param name="Role">Optional role filter (admin/user/editor).</param>
/// <param name="SearchTerm">Optional search term for email or display name.</param>
internal record BulkExportUsersQuery(
    string? Role = null,
    string? SearchTerm = null
) : IQuery<string>;
