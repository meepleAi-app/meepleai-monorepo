using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DatabaseSync.Application.Commands;

internal record PreviewMigrationSqlCommand() : ICommand<string>;
