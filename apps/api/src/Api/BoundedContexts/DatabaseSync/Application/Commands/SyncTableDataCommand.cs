using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DatabaseSync.Application.Commands;

internal record SyncTableDataCommand(
    string TableName,
    SyncDirection Direction,
    string Confirmation,
    Guid AdminUserId
) : ICommand<SyncResult>;
