using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to import multiple users from CSV data.
/// CSV format: email,displayName,role,password
/// </summary>
/// <param name="CsvContent">CSV file content as string.</param>
/// <param name="RequesterId">The ID of the admin requesting the operation.</param>
internal record BulkImportUsersCommand(
    string CsvContent,
    Guid RequesterId
) : ICommand<BulkOperationResult>;
