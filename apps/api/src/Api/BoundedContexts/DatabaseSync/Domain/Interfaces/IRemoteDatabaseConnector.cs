using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Domain.Interfaces;

/// <summary>
/// Provides NpgsqlConnection to the remote (staging) database via the SSH tunnel sidecar.
/// </summary>
internal interface IRemoteDatabaseConnector
{
    /// <summary>
    /// Creates and opens a connection to the remote database.
    /// Throws if tunnel is not open.
    /// </summary>
    Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default);
}
