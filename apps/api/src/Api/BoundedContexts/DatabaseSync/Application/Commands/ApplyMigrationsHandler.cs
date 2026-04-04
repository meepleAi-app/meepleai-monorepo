using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Entities;
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
            var sql = migrator.GenerateScript(fromMigration: lastStagingMigration, toMigration: null, options: MigrationsSqlGenerationOptions.Idempotent);

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

                        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

                        _logger.LogInformation("Applied {Count} migrations to staging. OperationId={OpId}",
                            diff.LocalOnly.Count, operationId);

                        var auditLog = new AuditLog(
                            id: Guid.NewGuid(),
                            userId: command.AdminUserId,
                            action: "DatabaseSync.ApplyMigrations",
                            resource: "Schema",
                            result: "Success",
                            resourceId: operationId.ToString(),
                            details: JsonSerializer.Serialize(new
                            {
                                direction = "LocalToStaging",
                                migrations = diff.LocalOnly.Select(m => m.MigrationId).ToArray(),
                                sqlHash
                            }));
                        _dbContext.Set<AuditLog>().Add(auditLog);
                        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                        return new SyncResult(true, diff.LocalOnly.Count, 0, operationId);
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        _logger.LogError(ex, "Failed to apply migrations to staging");
                        return new SyncResult(false, 0, 0, operationId, ex.Message);
                    }
                }
            }
            finally
            {
                if (lockAcquired)
                {
                    var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtext($1))", remoteConn);
                    await using (unlockCmd.ConfigureAwait(false))
                    {
                        unlockCmd.Parameters.AddWithValue("db_sync_schema_apply");
                        await unlockCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
