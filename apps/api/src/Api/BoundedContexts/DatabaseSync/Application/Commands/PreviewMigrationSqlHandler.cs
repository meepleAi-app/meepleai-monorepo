using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Api.BoundedContexts.DatabaseSync.Application.Commands;

internal class PreviewMigrationSqlHandler : ICommandHandler<PreviewMigrationSqlCommand, string>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;

    public PreviewMigrationSqlHandler(MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _remoteConnector = remoteConnector ?? throw new ArgumentNullException(nameof(remoteConnector));
    }

    public async Task<string> Handle(PreviewMigrationSqlCommand command, CancellationToken cancellationToken)
    {
        var remoteConn = await _remoteConnector.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (remoteConn.ConfigureAwait(false))
        {
            var stagingMigrations = await SchemaDiffEngine.ReadMigrationsAsync(remoteConn, cancellationToken).ConfigureAwait(false);
            var lastStagingMigration = stagingMigrations.Count > 0 ? stagingMigrations[^1].MigrationId : null;

            var migrator = _dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
            return migrator.GenerateScript(
                fromMigration: lastStagingMigration,
                toMigration: null,
                options: MigrationsSqlGenerationOptions.Idempotent);
        }
    }
}
