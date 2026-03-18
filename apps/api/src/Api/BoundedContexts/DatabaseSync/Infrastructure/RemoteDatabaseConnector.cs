using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

/// <summary>
/// Opens NpgsqlConnection to the remote (staging) database, gated by SSH tunnel state.
/// Connection is only attempted when the tunnel sidecar reports <see cref="TunnelState.Open"/>.
/// </summary>
internal sealed class RemoteDatabaseConnector : IRemoteDatabaseConnector
{
    private readonly ISshTunnelClient _tunnelClient;
    private readonly string _connectionString;
    private readonly ILogger<RemoteDatabaseConnector> _logger;

    public RemoteDatabaseConnector(
        ISshTunnelClient tunnelClient,
        string connectionString,
        ILogger<RemoteDatabaseConnector> logger)
    {
        _tunnelClient = tunnelClient ?? throw new ArgumentNullException(nameof(tunnelClient));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var status = await _tunnelClient.GetStatusAsync(ct).ConfigureAwait(false);
        if (status.Status != TunnelState.Open)
        {
            throw new InvalidOperationException(
                $"SSH tunnel is not open (current state: {status.Status}). " +
                "Open the tunnel first via POST /api/v1/admin/db-sync/tunnel/open");
        }

        var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Opened connection to remote database");
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
