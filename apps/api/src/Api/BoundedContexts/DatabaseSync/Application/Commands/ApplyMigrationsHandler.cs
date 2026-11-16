using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Commands;

internal class ApplyMigrationsHandler : ICommandHandler<ApplyMigrationsCommand, SyncResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;
    private readonly ILogger<ApplyMigrationsHandler> _logger;

    public ApplyMigrationsHandler(
        MeepleAiDbContext dbContext,
        IRemoteDatabaseConnector remoteConnector,
        ILogger<ApplyMigrationsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _remoteConnector = remoteConnector ?? throw new ArgumentNullException(nameof(remoteConnector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SyncResult> Handle(ApplyMigrationsCommand command, CancellationToken cancellationToken)
    {
        if (command.Direction == SyncDirection.StagingToLocal)
        {
            return new SyncResult(false, 0, 0, Guid.Empty,
                "Applying staging-only migrations to local is not supported in v1.");
        }

        var remoteConn = await _remoteConnector.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (remoteConn.ConfigureAwait(false))
        {
            var remoteMigrations = await SchemaDiffEngine.ReadMigrationsAsync(remoteConn, cancellationToken).ConfigureAwait(false);
            var lastStagingMigration = remoteMigrations.Count > 0 ? remoteMigrations[^1].MigrationId : null;

            var migrator = _dbContext.GetInfrastructure().GetRequiredService<IMigrator>();
            // #5002: NoTransactions, because the script runs inside the transaction opened below. Without it EF wraps
            // every migration in START TRANSACTION ... COMMIT, and the first COMMIT confirmed the handler's own
            // transaction: the migrations stayed applied one by one and CommitAsync threw. A migration that must run
            // outside a transaction (suppressTransaction) would now fail and roll the whole apply back; none does.
            var sql = migrator.GenerateScript(
                fromMigration: lastStagingMigration,
                toMigration: null,
                options: MigrationsSqlGenerationOptions.Idempotent | MigrationsSqlGenerationOptions.NoTransactions);

            if (string.IsNullOrWhiteSpace(sql))
            {
                return new SyncResult(true, 0, 0, Guid.NewGuid(), "No migrations to apply");
            }

            var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
                ?? throw new InvalidOperationException("Expected NpgsqlConnection");
            if (localConn.State != System.Data.ConnectionState.Open)
            {
                await localConn.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var localMigrations = await SchemaDiffEngine.ReadMigrationsAsync(localConn, cancellationToken).ConfigureAwait(false);
            var diff = SchemaDiffEngine.ComputeDiff(localMigrations, remoteMigrations);

            var expectedConfirmation = $"APPLY {diff.LocalOnly.Count} MIGRATIONS TO STAGING";
            if (!string.Equals(command.Confirmation, expectedConfirmation, StringComparison.Ordinal))
            {
                return new SyncResult(false, 0, 0, Guid.Empty,
                    $"Confirmation mismatch. Expected: \"{expectedConfirmation}\"");
            }

            var sqlHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)))[..16];
            var operationId = Guid.NewGuid();

            // Acquire advisory lock to prevent concurrent migration operations
            var lockAcquired = false;
            try
            {
                var lockCmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(hashtext($1))", remoteConn);
                await using (lockCmd.ConfigureAwait(false))
                {
                    lockCmd.Parameters.AddWithValue("db_sync_schema_apply");
                    lockAcquired = (bool)(await lockCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
                    if (!lockAcquired)
                        return new SyncResult(false, 0, 0, Guid.Empty, "Another migration apply operation is in progress");
                }

                var tx = await remoteConn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using (tx.ConfigureAwait(false))
                {
                    try
                    {
                        var cmd = new NpgsqlCommand(sql, remoteConn, tx);
                        await using (cmd.ConfigureAwait(false))
                        {
                            cmd.CommandTimeout = 120;
                            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }

                        // #5002: the commit is the last statement of the try, so the catch only ever rolls back a
                        // transaction that is still open. The audit follows, out of it.
                        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // CancellationToken.None here and below: once the outcome is decided, an aborted request must not stop the
                        // rollback, the audit or the lock release.
                        await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                        _logger.LogError(ex, "Failed to apply migrations to staging");

                        await TryWriteAuditAsync(command, operationId, "Failure", JsonSerializer.Serialize(new
                        {
                            direction = "LocalToStaging",
                            migrations = diff.LocalOnly.Select(m => m.MigrationId).ToArray(),
                            sqlHash,
                            error = ex.Message
                        })).ConfigureAwait(false);

                        return new SyncResult(false, 0, 0, operationId, ex.Message);
                    }
                }

                _logger.LogInformation("Applied {Count} migrations to staging. OperationId={OpId}",
                    diff.LocalOnly.Count, operationId);

                var audited = await TryWriteAuditAsync(command, operationId, "Success", JsonSerializer.Serialize(new
                {
                    direction = "LocalToStaging",
                    migrations = diff.LocalOnly.Select(m => m.MigrationId).ToArray(),
                    sqlHash
                })).ConfigureAwait(false);

                // The migrations are committed: the answer is a success either way, and says so when the audit is missing.
                return new SyncResult(true, diff.LocalOnly.Count, 0, operationId,
                    audited ? null : $"Migrations applied, but the audit row could not be written. OperationId={operationId}");
            }
            finally
            {
                if (lockAcquired)
                {
                    var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtext($1))", remoteConn);
                    await using (unlockCmd.ConfigureAwait(false))
                    {
                        unlockCmd.Parameters.AddWithValue("db_sync_schema_apply");
                        await unlockCmd.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private Task<bool> TryWriteAuditAsync(ApplyMigrationsCommand command, Guid operationId, string result, string details) =>
        DatabaseSyncAuditWriter.TryWriteAsync(
            _dbContext, _logger, command.AdminUserId, "DatabaseSync.ApplyMigrations", "Schema", operationId, result, details);
}
