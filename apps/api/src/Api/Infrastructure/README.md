# Infrastructure Layer

## Responsabilità

Contiene l'infrastruttura condivisa dell'applicazione: EF Core DbContext, Entity Configurations, Background Tasks, Security e Telemetry.

## Struttura

```
Infrastructure/
├── BackgroundTasks/         Background jobs e scheduled tasks
├── Entities/                EF Core entities (organizzate per bounded context)
│   ├── Administration/
│   ├── Authentication/
│   ├── DocumentProcessing/
│   ├── GameManagement/
│   ├── KnowledgeBase/
│   ├── SystemConfiguration/
│   └── WorkflowIntegration/
├── EntityConfigurations/    Fluent API configurations (organizzate per bounded context)
│   ├── Administration/
│   ├── Authentication/
│   ├── DocumentProcessing/
│   ├── GameManagement/
│   ├── KnowledgeBase/
│   ├── SystemConfiguration/
│   └── WorkflowIntegration/
├── Security/                Security utilities (encryption, hashing, token management)
├── Telemetry/               OpenTelemetry, logging, metrics setup
├── MeepleAiDbContext.cs     EF Core DbContext principale
├── MeepleAiDbContextFactory.cs  Design-time factory per migrations
├── QdrantHealthCheck.cs     Health check per Qdrant
└── SecretsHelper.cs         Helper per gestione secrets
```

## MeepleAiDbContext

**Responsabilità**: EF Core DbContext principale per accesso al database PostgreSQL.

### Features
- **DbSets**: Espone tutte le entities dei 7 bounded contexts
- **Auto-Discovery**: Applica automaticamente tutte le EntityConfigurations
- **Soft Delete**: Implementa global query filter per soft delete
- **Audit Fields**: Automatic tracking di CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
- **Domain Events**: Pubblica domain events dopo SaveChanges
- **Connection Resilience**: Retry policy per transient failures

### Configuration
```csharp
services.AddDbContext<MeepleAiDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null
        );
    });
    options.EnableSensitiveDataLogging(isDevelopment);
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // Default
});
```

### DbSets (Per Context)

**Authentication**:
- `Users`
- `Sessions`
- `ApiKeys`
- `OAuthAccounts`

**GameManagement**:
- `Games`
- `PlaySessions`

**KnowledgeBase**:
- `ChatThreads`
- `Messages`
- `VectorDocuments`
- `RuleSpecs`

**DocumentProcessing**:
- `PdfDocuments`
- `ExtractionResults`
- `QualityReports`

**WorkflowIntegration**:
- `N8nConfigs`
- `WorkflowExecutions`
- `WorkflowErrors`

**SystemConfiguration**:
- `ConfigurationSettings`
- `ConfigurationHistories`
- `FeatureFlags`

**Administration**:
- `AuditLogs`
- `Alerts`
- `SystemMetrics`

## Entities/

**Organizzazione**: Le entities sono organizzate per bounded context in sottocartelle.

### Naming Convention
- **Entity**: `{Name}Entity` (es. `UserEntity`, `GameEntity`)
- **Table**: Plurale del nome (es. `Users`, `Games`)
- **Schema**: Default `public` (opzionale: schema per context)

### Base Entity
Tutte le entities ereditano da `BaseEntity`:
```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }  // Soft delete
}
```

### Example Entity
```csharp
// Entities/GameManagement/GameEntity.cs
public class GameEntity : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public int? Year { get; set; }
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int PlayingTimeMinutes { get; set; }
    public double? ComplexityRating { get; set; }

    // Navigation properties
    public ICollection<PdfDocumentEntity> PdfDocuments { get; set; } = new List<PdfDocumentEntity>();
    public ICollection<PlaySessionEntity> PlaySessions { get; set; } = new List<PlaySessionEntity>();
}
```

## EntityConfigurations/

**Organizzazione**: Configurazioni Fluent API organizzate per bounded context.

### Naming Convention
- **Configuration**: `{EntityName}Configuration` (es. `UserEntityConfiguration`)

### Example Configuration
```csharp
// EntityConfigurations/GameManagement/GameEntityConfiguration.cs
public class GameEntityConfiguration : IEntityTypeConfiguration<GameEntity>
{
    public void Configure(EntityTypeBuilder<GameEntity> builder)
    {
        builder.ToTable("Games");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.Publisher)
            .HasMaxLength(100);

        builder.HasIndex(g => g.Title);

        // Relationships
        builder.HasMany(g => g.PdfDocuments)
            .WithOne(p => p.Game)
            .HasForeignKey(p => p.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete global filter
        builder.HasQueryFilter(g => !g.IsDeleted);
    }
}
```

### Auto-Discovery
Le configurazioni sono applicate automaticamente dal DbContext:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeepleAiDbContext).Assembly);
}
```

## BackgroundTasks/

**Responsabilità**: Background jobs e scheduled tasks.

### Example Background Tasks
- **CacheWarmupTask**: Riscaldamento cache all'avvio
- **MetricsAggregationTask**: Aggregazione metriche ogni ora
- **AuditLogCleanupTask**: Pulizia audit logs vecchi (7 anni retention)
- **AlertEvaluationTask**: Valutazione soglie alert ogni minuto

### Implementation
Usa `IHostedService` o `BackgroundService`:
```csharp
public class CacheWarmupTask : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Warmup logic
    }
}

// Registration
services.AddHostedService<CacheWarmupTask>();
```

## Security/

**Responsabilità**: Utilities per sicurezza (encryption, hashing, token management).

### Components
- **PasswordHasher**: PBKDF2 hashing con 210k iterations
- **ApiKeyGenerator**: Generazione API keys in formato `mpl_{env}_{base64}`
- **TokenEncryption**: Encryption/decryption OAuth tokens
- **SecretsManager**: Gestione secrets (Azure Key Vault, Docker secrets)

### Example
```csharp
// Security/PasswordHasher.cs
public static class PasswordHasher
{
    public static string Hash(string password)
    {
        // PBKDF2 with 210,000 iterations
    }

    public static bool Verify(string password, string hash)
    {
        // Constant-time comparison
    }
}
```

## Telemetry/

**Responsabilità**: Setup OpenTelemetry, logging, metrics, tracing.

### Components
- **OpenTelemetrySetup**: Configurazione traces, metrics, logging
- **ActivitySource**: Custom activity sources per tracing
- **MetricsExporter**: Prometheus exporter
- **LoggingSetup**: Serilog configuration

### Observability Stack
- **Logs**: Serilog → Seq (structured logging)
- **Traces**: OpenTelemetry → Jaeger (W3C Trace Context)
- **Metrics**: Prometheus → Grafana

### Example
```csharp
// Telemetry/OpenTelemetrySetup.cs
public static void ConfigureOpenTelemetry(IServiceCollection services)
{
    services.AddOpenTelemetry()
        .WithTracing(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("MeepleAI.*")
                .AddJaegerExporter();
        })
        .WithMetrics(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddPrometheusExporter();
        });
}
```

## MeepleAiDbContextFactory

**Responsabilità**: Design-time factory per EF Core migrations.

### Usage
```bash
# Add migration
dotnet ef migrations add MigrationName --project src/Api

# Update database
dotnet ef database update --project src/Api

# Rollback migration
dotnet ef database update PreviousMigrationName --project src/Api
```

### Implementation
```csharp
public class MeepleAiDbContextFactory : IDesignTimeDbContextFactory<MeepleAiDbContext>
{
    public MeepleAiDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("Postgres"));

        return new MeepleAiDbContext(optionsBuilder.Options);
    }
}
```

## QdrantHealthCheck

**Responsabilità**: Health check per Qdrant vector database.

### Registration
```csharp
services.AddHealthChecks()
    .AddCheck<QdrantHealthCheck>("qdrant")
    .AddNpgSql(configuration.GetConnectionString("Postgres")!, name: "postgres")
    .AddRedis(configuration.GetConnectionString("Redis")!, name: "redis");
```

### Endpoints
- `/health/live`: Liveness probe (sempre healthy se app è running)
- `/health/ready`: Readiness probe (checks Postgres, Redis, Qdrant)

## SecretsHelper

**Responsabilità**: Helper per gestione secrets da varie fonti.

### Supported Sources
- **Environment Variables**: Default
- **Azure Key Vault**: Production
- **Docker Secrets**: `/run/secrets/`
- **User Secrets**: Development (dotnet user-secrets)

### Example
```csharp
var apiKey = SecretsHelper.GetSecret("OpenRouter__ApiKey");
// Tries:
// 1. Environment variable OPENROUTER__APIKEY
// 2. Docker secret /run/secrets/openrouter_apikey
// 3. Azure Key Vault (if configured)
// 4. User Secrets (development)
```

## Migrations

### Location
`apps/api/src/Api/Migrations/`

### Auto-Apply
Le migrations sono applicate automaticamente all'avvio in Development:
```csharp
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    await dbContext.Database.MigrateAsync();
}
```

### Manual Apply (Production)
```bash
dotnet ef database update --project src/Api
```

## Connection Strings

### Development (appsettings.Development.json)
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=meepleai;Username=meepleai;Password=dev123",
    "Redis": "localhost:6379",
    "Qdrant": "http://localhost:6333"
  }
}
```

### Production (Environment Variables)
```bash
CONNECTIONSTRINGS__POSTGRES=Host=prod-pg;Port=5432;Database=meepleai;Username=meepleai;Password=${DB_PASSWORD}
CONNECTIONSTRINGS__REDIS=prod-redis:6379
CONNECTIONSTRINGS__QDRANT=http://prod-qdrant:6333
```

## Performance Optimizations

### Connection Pooling
- **PostgreSQL**: Pool size 10-100 (default 100)
- **Redis**: Connection multiplexer with pooling
- **HTTP Clients**: IHttpClientFactory con connection pooling

### AsNoTracking
Query di lettura usano `.AsNoTracking()` di default (30% faster):
```csharp
options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
```

Override per tracking quando necessario:
```csharp
var user = await dbContext.Users.AsTracking().FirstOrDefaultAsync(u => u.Id == userId);
```

### Compiled Queries
Query frequenti sono precompilate per performance:
```csharp
private static readonly Func<MeepleAiDbContext, Guid, Task<UserEntity?>> _compiledQuery =
    EF.CompileAsyncQuery((MeepleAiDbContext db, Guid id) => db.Users.FirstOrDefault(u => u.Id == id));
```

## Testing

### Test Database
Integration tests usano Testcontainers per PostgreSQL:
```csharp
[Collection("Database")]
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private MeepleAiDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().Build();
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }
}
```

## Dipendenze

- **Npgsql.EntityFrameworkCore.PostgreSQL**: PostgreSQL provider
- **Microsoft.EntityFrameworkCore**: EF Core
- **Serilog**: Logging
- **OpenTelemetry**: Tracing e metrics
- **StackExchange.Redis**: Redis client
- **Qdrant.Client**: Qdrant vector database
- **Testcontainers**: Integration testing

## Related Documentation

- `docs/02-development/database-migrations.md`
- `docs/02-development/testing/integration-testing.md`
- `docs/06-security/secrets-management.md`
- `docs/01-architecture/overview/system-architecture.md` (Infrastructure section)
