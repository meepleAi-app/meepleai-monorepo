using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.Infrastructure;
using Api.Infrastructure.Persistence;
using Api.Tests.TestHelpers;
using Api.Services;
using Api.Tests.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Npgsql;
using Pgvector.EntityFrameworkCore; // Issue #3547: Enable pgvector type mapping
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Integration.FrontendSdk;

/// <summary>
/// WebApplicationFactory for Frontend SDK integration tests.
///
/// Provides a TestServer that simulates real API behavior for testing
/// scenarios that the frontend SDK encounters:
/// - HTTP retry logic (503, 429, timeouts)
/// - Error handling (4xx, 5xx responses)
/// - Authentication flows (sessions, API keys, 2FA, OAuth)
/// - Request deduplication
/// - Circuit breaker behavior
///
/// Uses Testcontainers for real database to ensure accurate
/// integration testing of the full request pipeline.
/// </summary>
public class FrontendSdkTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private string? _connectionString;

    /// <summary>
    /// Il database creato da questa fixture su un server esterno (CI), da cancellare alla fine.
    /// <c>null</c> quando il server è un container suo, che sparisce con tutto il contenuto.
    /// </summary>
    private string? _ownedExternalDatabase;

    /// <summary>
    /// L'indirizzo da dare ai client dei test. #4518: il cookie di sessione esce con l'attributo
    /// <c>secure</c> anche su HTTP (<c>SessionCookieConfiguration.Secure</c> lo forza di proposito),
    /// e un <c>CookieContainer</c> non lo rimanda a un indirizzo <c>http://</c>. Con l'indirizzo di
    /// default di <c>WebApplicationFactory</c> ogni test «login, poi richiesta autenticata» riceveva
    /// 401 dopo un login riuscito. Il client dell'SDK parla HTTPS: è il test a doversi adeguare.
    /// </summary>
    public static readonly Uri HttpsBaseAddress = new("https://localhost");

    /// <summary>
    /// Issue #3102: static constructor set DISABLE_RATE_LIMITING before host creation, because
    /// AddRateLimitingServices read it during service registration — which runs before
    /// ConfigureWebHost is applied.
    /// Issue #3887: that read moved after Build(), so this factory's in-memory configuration is now
    /// authoritative. The assignment is kept as defence in depth: it is set once and never
    /// restored, which is deterministic — it was the *unsetting* at teardown that opened a window.
    /// </summary>
    static FrontendSdkTestFactory()
    {
        Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", "true");
    }

    /// <summary>
    /// Initialize PostgreSQL container before tests.
    /// Container is shared across all tests in the collection for performance.
    /// </summary>
    [Obsolete]
    public async ValueTask InitializeAsync()
    {
        // Check if external connection string is provided (for CI/CD)
        var externalConn = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvPostgresConnectionString);
        if (!string.IsNullOrWhiteSpace(externalConn))
        {
            // #4518: in CI questa variabile punta al database CONDIVISO del service container
            // (meepleai_test). Usarlo così com'è voleva dire EnsureDeletedAsync su quel database
            // all'avvio e TRUNCATE di tutte le sue tabelle a ogni test: è il «conflitto con il
            // service container» per cui #3102 aveva escluso questi test da ogni gate. Qui si
            // prende solo il server, e si lavora su un database proprio, cancellato in DisposeAsync.
            var builder = new NpgsqlConnectionStringBuilder(externalConn)
            {
                Database = $"frontend_sdk_{Guid.NewGuid():N}",
            };
            _ownedExternalDatabase = builder.Database;
            _connectionString = builder.ConnectionString;
        }
        else
        {
            // Issue #2031 fix: Use ContainerBuilder instead of PostgreSqlBuilder
            // to avoid exec-based wait strategy that causes "cannot hijack" errors
            _postgresContainer = new ContainerBuilder()
                .WithImage("pgvector/pgvector:pg16")  // Issue #3547: Use pgvector image for Vector column support
                .WithEnvironment("POSTGRES_USER", "testuser")
                .WithEnvironment("POSTGRES_PASSWORD", "testpass")
                .WithEnvironment("POSTGRES_DB", "frontend_sdk_test")
                .WithPortBinding(5432, assignRandomHostPort: true)
                .WithCleanUp(true)
                .Build();

            await _postgresContainer.StartAsync();

            var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
            // Issue #2577: Enable connection pooling to prevent timeout failures
            // Same optimization as SharedTestcontainersFixture for consistency
            _connectionString = $"Host=localhost;Port={postgresPort};Database=frontend_sdk_test;Username=testuser;Password=testpass;Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=10;Pooling=true;MinPoolSize=2;MaxPoolSize=50;Timeout=30;CommandTimeout=60;ConnectionIdleLifetime=60;ConnectionPruningInterval=10;";

            // Issue #2031: Wait for PostgreSQL to accept connections with retry
            await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        }

        // Initialize database schema with migrations
        await InitializeDatabaseAsync();
    }

    /// <summary>
    /// Initialize database schema by running EF migrations.
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString, o =>
            {
                o.UseVector(); // Issue #3547: Enable pgvector type mapping
                o.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
            })
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        await using var context = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

        // Ensure database is empty and run all migrations
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Cleanup PostgreSQL container after all tests complete.
    /// </summary>
    public new async ValueTask DisposeAsync()
    {
        // #3887: do NOT blank DISABLE_RATE_LIMITING here. The variable is process-global, so
        // clearing it at teardown turned rate limiting back ON for any host that a parallel xUnit
        // collection happened to build afterwards — a 429 landing on an unrelated test. Setting it
        // to "true" and leaving it set is deterministic; unsetting it is what opens the window.

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
        await base.DisposeAsync();

        if (_ownedExternalDatabase != null)
        {
            await DropOwnedExternalDatabaseAsync(_ownedExternalDatabase);
        }
    }

    private async Task DropOwnedExternalDatabaseAsync(string databaseName)
    {
        // WITH (FORCE) chiude le connessioni che il pool dell'host tiene ancora aperte sul database;
        // ClearPool toglie dal pool di questo processo quelle che il DROP sta per far cadere. Solo
        // il pool di questa stringa: ClearAllPools toccherebbe le collection che girano in parallelo.
        await using (var own = new NpgsqlConnection(_connectionString))
        {
            NpgsqlConnection.ClearPool(own);
        }

        var builder = new NpgsqlConnectionStringBuilder(_connectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // il nome lo genera InitializeAsync: prefisso fisso + GUID in esadecimale
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);";
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Svuota tutte le tabelle del database dei test (tranne la storia delle migrazioni). Lo chiama
    /// ogni classe di test in <c>InitializeAsync</c>, per l'isolamento fra test.
    /// </summary>
    /// <remarks>
    /// #4518: prima ogni classe lo faceva da sé, con un <c>TRUNCATE ... CASCADE</c> per tabella. Il
    /// 2026-10-19, con 220 tabelle nello schema, erano ~7 secondi a test misurati in locale: la
    /// maggior parte del tempo della cartella. Qui è un solo comando, con i <c>DELETE</c> in un unico
    /// batch e i trigger (compresi quelli delle chiavi esterne) sospesi da
    /// <c>session_replication_role</c>: lo stesso giorno, fra 13 e 112 millisecondi. Svuotando TUTTE
    /// le tabelle non restano riferimenti orfani. Serve un superuser,
    /// come già serviva prima: lo sono l'utente del container locale e quello del service container
    /// di CI.
    /// </remarks>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var list = connection.CreateCommand())
        {
            list.CommandText =
                "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'";
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        if (tables.Count == 0)
        {
            return;
        }

        var sql = new System.Text.StringBuilder("SET session_replication_role = 'replica'; ");
        foreach (var table in tables)
        {
            sql.Append("DELETE FROM \"").Append(table.Replace("\"", "\"\"", StringComparison.Ordinal)).Append("\"; ");
        }
        sql.Append("SET session_replication_role = 'origin';");

        await using var reset = connection.CreateCommand();
#pragma warning disable CA2100 // i nomi vengono da pg_tables, quotati come identificatori
        reset.CommandText = sql.ToString();
#pragma warning restore CA2100
        await reset.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // #3978 / REQ-AI-TEST-001 — guardia fail-closed sugli host AI a pagamento.
        // Questa factory era una delle sei (su otto che costruiscono un host) che NON la
        // installava: senza, una richiesta verso un provider a consumo USCIVA dalla macchina
        // e moriva con 401 lato provider, cioe il divieto valeva per fortuna, non per costruzione.
        builder.ConfigureServices(services => services.AddPaidAiHostGuard());

        builder.UseEnvironment("Testing");

        // Issue #2705 / #3102: redundant with the static constructor and with the in-memory
        // configuration below (#3887); kept because it is set-and-never-restored, i.e. harmless.
        Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", "true");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            // Provide test configuration
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-openrouter-key",
                ["OPENROUTER_API_KEY_FILE"] = null,
                ["ConnectionStrings:Postgres"] = _connectionString,
                // Disable health checks for external services in tests
                ["HealthChecks:Qdrant:Enabled"] = "false",
                ["HealthChecks:Redis:Enabled"] = "false",
                // Issue #2705: Disable rate limiting for integration tests to prevent 429 errors
                ["RateLimiting:Enabled"] = "false",
                // Configure Redis/Qdrant URLs to dummy values (services are mocked)
                ["REDIS_URL"] = "localhost:6379",
                ["QDRANT_URL"] = "http://localhost:6333",
                // Enable retry and circuit breaker for testing
                ["RetryPolicy:MaxRetries"] = "3",
                ["RetryPolicy:InitialDelayMs"] = "100",
                ["CircuitBreaker:FailureThreshold"] = "3",
                ["CircuitBreaker:ResetTimeoutSeconds"] = "5",
                // Configure CORS for tests
                ["Cors:Origins:0"] = "http://localhost:3000",
                ["Cors:Origins:1"] = "http://localhost:3001",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace DbContext with test database
            services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
            services.RemoveAll(typeof(MeepleAiDbContext));

            // #4518: in ambiente "Testing" AddDatabaseServices non registra niente, quindi il sink
            // degli snapshot di audit va registrato qui, come fa IntegrationWebApplicationFactory.
            // Senza, AuditLoggingBehavior non si risolve e OGNI richiesta che passa da MediatR
            // risponde 500: è il secondo motivo, dopo gli IHostedService, per cui questi test
            // erano tutti rossi.
            services.RemoveAll<ScopedAuditSnapshotSink>();
            services.RemoveAll<IAuditSnapshotSink>();
            services.RemoveAll<AuditingSaveChangesInterceptor>();
            services.AddScoped<ScopedAuditSnapshotSink>();
            services.AddScoped<IAuditSnapshotSink>(sp => sp.GetRequiredService<ScopedAuditSnapshotSink>());
            services.AddScoped<AuditingSaveChangesInterceptor>();

            services.AddDbContext<MeepleAiDbContext>((serviceProvider, options) =>
            {
                options.UseNpgsql(_connectionString, o =>
                {
                    o.UseVector(); // Issue #3547: Enable pgvector type mapping
                    // Issue #4301: Testcontainers on Docker Desktop Windows can produce transient
                    // EndOfStreamException/NpgsqlException during container warm-up and
                    // under load. EnableRetryOnFailure makes EF re-open the connection
                    // automatically instead of surfacing a 500 to the test assertions.
                    o.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                });
                options.EnableSensitiveDataLogging(); // Better error messages
                // #4518: come in produzione (PERF-06) e in IntegrationWebApplicationFactory.
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.AddInterceptors(serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>());
            });

            // Mock external services that aren't needed for SDK tests
            services.RemoveAll(typeof(IConnectionMultiplexer));
            var mockRedis = new Mock<IConnectionMultiplexer>();
            var mockDatabase = new Mock<IDatabase>();
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);
            services.AddSingleton(mockRedis.Object);

            // #4518: il mock sopra copre GetDatabase() ma non GetSubscriber(), che quindi torna
            // null. HybridCacheInvalidationSubscriber (e ProviderCacheInvalidationSubscriber) lo
            // chiamano in StartAsync: NullReferenceException, l'host non parte e tutti i test di
            // questa cartella falliscono in InitializeAsync, prima di arrivare a una sola rotta.
            // Nessuno se n'era accorto perché nessun workflow li eseguiva.
            //
            // Stessa correzione di #3622 (DevToolsEndpointsIntegrationTests) e #3633
            // (CorsHeaderWhitelistTests), e stessa scelta di IntegrationWebApplicationFactory:
            // via tutti gli IHostedService. Mockare il subscriber non basta, perché
            // SubscribeAsync restituisce ChannelMessageQueue, sealed e con costruttore internal;
            // e un elenco di servizi da togliere uno per uno si romperebbe in silenzio al
            // prossimo servizio di background che usa Redis. Questi test esercitano la pipeline
            // HTTP (errori, autenticazione, header): nessun servizio di background vi partecipa.
            services.RemoveAll(typeof(IHostedService));

            // Mock vector/embedding services (not needed for HTTP/auth tests)
            services.RemoveAll(typeof(IEmbeddingService));
            services.AddScoped<IEmbeddingService>(_ => Mock.Of<IEmbeddingService>());

            // Mock cache service
            services.RemoveAll(typeof(IHybridCacheService));
            services.AddSingleton<IHybridCacheService>(_ => Mock.Of<IHybridCacheService>());

            // Mock configuration service: enable public registration and return defaults for other keys.
            // Moq matches in reverse setup order (last wins), so general fallbacks are set first.
            services.RemoveAll(typeof(IConfigurationService));
            var mockConfigService = new Mock<IConfigurationService>();
            mockConfigService
                .Setup(c => c.GetConfigurationByKeyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SystemConfigurationDto?)null);
            mockConfigService
                .Setup(c => c.GetValueAsync<bool?>(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string?>()))
                .ReturnsAsync((string key, bool? def, string? env) => def);
            mockConfigService
                .Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
                .ReturnsAsync((string key, int? def, string? env) => def);
            mockConfigService
                .Setup(c => c.GetValueAsync<string?>(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync((string key, string? def, string? env) => def);
            // Specific override (last = highest priority): enable public registration
            mockConfigService
                .Setup(c => c.GetValueAsync<bool?>("Registration:PublicEnabled", It.IsAny<bool?>(), It.IsAny<string?>()))
                .ReturnsAsync(true);
            services.AddScoped<IConfigurationService>(_ => mockConfigService.Object);

            // Ensure domain event collector is registered
            services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        });

        builder.ConfigureTestServices(services =>
        {
            // Additional test-specific service configuration can go here
        });
    }
}

/// <summary>
/// xUnit collection definition for sharing FrontendSdkTestFactory across test classes.
/// This improves performance by reusing the TestServer and database container.
/// </summary>
[CollectionDefinition(nameof(FrontendSdkTestCollection))]
public class FrontendSdkTestCollection : ICollectionFixture<FrontendSdkTestFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
