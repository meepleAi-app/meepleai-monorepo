using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

internal static class DatabaseSyncServiceExtensions
{
    public static IServiceCollection AddDatabaseSyncContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        var sidecarBaseUrl = Environment.GetEnvironmentVariable("SIDECAR_BASE_URL") ?? "http://ssh-tunnel-sidecar:2222";
#pragma warning restore S1075
        var sidecarAuthToken = Environment.GetEnvironmentVariable("SIDECAR_AUTH_TOKEN") ?? "";

        services.AddHttpClient("SshTunnelSidecar", client =>
        {
            client.BaseAddress = new Uri(sidecarBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<ISshTunnelClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient("SshTunnelSidecar");
            var logger = sp.GetRequiredService<ILogger<SshTunnelClient>>();
            return new SshTunnelClient(httpClient, sidecarAuthToken, logger);
        });

        var remoteHost = Environment.GetEnvironmentVariable("REMOTE_DB_HOST") ?? "ssh-tunnel-sidecar";
        var remotePort = Environment.GetEnvironmentVariable("REMOTE_DB_PORT") ?? "15432";
        var remoteDb = Environment.GetEnvironmentVariable("REMOTE_DB_NAME") ?? "meepleai";
        var remoteUser = Environment.GetEnvironmentVariable("REMOTE_DB_USER") ?? "meepleai";
        var remotePassword = Environment.GetEnvironmentVariable("REMOTE_DB_PASSWORD") ?? "";
        var remoteConnStr = $"Host={remoteHost};Port={remotePort};Database={remoteDb};Username={remoteUser};Password={remotePassword};Timeout=10";

        services.AddScoped<IRemoteDatabaseConnector>(sp =>
        {
            var tunnelClient = sp.GetRequiredService<ISshTunnelClient>();
            var logger = sp.GetRequiredService<ILogger<RemoteDatabaseConnector>>();
            return new RemoteDatabaseConnector(tunnelClient, remoteConnStr, logger);
        });

        return services;
    }
}
