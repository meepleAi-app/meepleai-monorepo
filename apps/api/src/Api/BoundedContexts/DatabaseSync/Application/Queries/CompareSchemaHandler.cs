using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Queries;

internal class CompareSchemaHandler : IQueryHandler<CompareSchemaQuery, SchemaDiffResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;

    public CompareSchemaHandler(MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _remoteConnector = remoteConnector ?? throw new ArgumentNullException(nameof(remoteConnector));
    }

    public async Task<SchemaDiffResult> Handle(CompareSchemaQuery query, CancellationToken cancellationToken)
    {
        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
        {
            await localConn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var localMigrations = await SchemaDiffEngine.ReadMigrationsAsync(localConn, cancellationToken).ConfigureAwait(false);

        var remoteConn = await _remoteConnector.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (remoteConn.ConfigureAwait(false))
        {
            var remoteMigrations = await SchemaDiffEngine.ReadMigrationsAsync(remoteConn, cancellationToken).ConfigureAwait(false);
            return SchemaDiffEngine.ComputeDiff(localMigrations, remoteMigrations);
        }
    }
}
