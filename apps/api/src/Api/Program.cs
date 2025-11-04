using Api.Configuration; // CHAT-02
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Logging;
using Api.Middleware;
using Api.Models;
using Api.Observability;
using Api.Routing;
using Api.Services;
using Api.Services.Chat; // CHAT-02
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc; // EDIT-05: For [FromServices] attribute
using Microsoft.AspNetCore.ResponseCompression; // PERF-11: Response compression
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly; // AI-13: Retry policies for BGG API
using Polly.Extensions.Http; // AI-13: HTTP-specific retry policies
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using System;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Claims;
using AspNetIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

// OPS-04: Configure Serilog with environment-based settings and sensitive data redaction
Log.Logger = LoggingConfiguration.ConfigureSerilog(builder).CreateLogger();

builder.Host.UseSerilog();

// PERF-11: Configure Response Compression (Brotli + Gzip)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Enable compression for HTTPS (secure)
    options.Providers.Add<BrotliCompressionProvider>(); // Brotli (better compression)
    options.Providers.Add<GzipCompressionProvider>(); // Gzip (fallback, widely supported)

    // Compress these MIME types
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/json; charset=utf-8",
        "text/plain",
        "text/json",
        "application/xml",
        "text/xml",
        "image/svg+xml"
    });
});

// Configure Brotli compression level (optimal balance)
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest; // Fastest: lower CPU, good compression (1-5x faster than Optimal)
});

// Configure Gzip compression level (optimal balance)
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest; // Fastest: lower CPU, good compression
});

var forwardedHeadersSection = builder.Configuration.GetSection("ForwardedHeaders");
var forwardedHeadersEnabled = forwardedHeadersSection.GetValue<bool?>("Enabled") ?? true;

if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        var knownProxies = forwardedHeadersSection.GetSection("KnownProxies").Get<string[]>() ?? Array.Empty<string>();
        foreach (var proxy in knownProxies)
        {
            if (!string.IsNullOrWhiteSpace(proxy) && IPAddress.TryParse(proxy, out var proxyAddress))
            {
                options.KnownProxies.Add(proxyAddress);
            }
        }

        var knownNetworks = forwardedHeadersSection.GetSection("KnownNetworks").Get<string[]>() ?? Array.Empty<string>();
        foreach (var network in knownNetworks)
        {
            if (string.IsNullOrWhiteSpace(network))
            {
                continue;
            }

            if (AspNetIpNetwork.TryParse(network, out var ipNetwork))
            {
                options.KnownNetworks.Add(ipNetwork);
                continue;
            }

            var parts = network.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var networkAddress) && int.TryParse(parts[1], out var prefixLength))
            {
                options.KnownNetworks.Add(new AspNetIpNetwork(networkAddress, prefixLength));
            }
        }

        var forwardLimit = forwardedHeadersSection.GetValue<int?>("ForwardLimit");
        if (forwardLimit.HasValue)
        {
            options.ForwardLimit = forwardLimit.Value;
        }

        var requireHeaderSymmetry = forwardedHeadersSection.GetValue<bool?>("RequireHeaderSymmetry");
        if (requireHeaderSymmetry.HasValue)
        {
            options.RequireHeaderSymmetry = requireHeaderSymmetry.Value;
        }
    });
}

builder.Services.Configure<SessionCookieConfiguration>(builder.Configuration.GetSection("Authentication:SessionCookie"));
builder.Services.Configure<SessionManagementConfiguration>(builder.Configuration.GetSection("Authentication:SessionManagement"));
builder.Services.Configure<OAuthConfiguration>(builder.Configuration.GetSection("Authentication:OAuth")); // AUTH-06
builder.Services.Configure<RateLimitConfiguration>(builder.Configuration.GetSection("RateLimit"));
builder.Services.Configure<PdfProcessingConfiguration>(builder.Configuration.GetSection("PdfProcessing"));
builder.Services.Configure<FollowUpQuestionsConfiguration>(builder.Configuration.GetSection("FollowUpQuestions")); // CHAT-02
builder.Services.Configure<RagPromptsConfiguration>(builder.Configuration.GetSection("RagPrompts")); // AI-07.1: RAG prompt templates
builder.Services.Configure<HybridCacheConfiguration>(builder.Configuration.GetSection("HybridCache")); // PERF-05: HybridCache configuration
builder.Services.Configure<HybridSearchConfiguration>(builder.Configuration.GetSection("HybridSearch")); // AI-14: Hybrid search configuration

// Infrastructure services (DB, Cache, HTTP clients)
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

// Application services (Domain, AI, Admin)
builder.Services.AddApplicationServices();

// Authentication services (Auth, OAuth, 2FA, API keys, Sessions)
builder.Services.AddAuthenticationServices(builder.Configuration);


// Observability services (OpenTelemetry, Health checks, Swagger)
builder.Services.AddObservabilityServices(builder.Configuration, builder.Environment);

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var corsOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        var topLevelOrigins = builder.Configuration
            .GetSection("AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        var configuredOrigins = corsOrigins
            .Concat(topLevelOrigins)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (configuredOrigins.Length == 0)
        {
            policy.WithOrigins("http://localhost:3000");
        }
        else
        {
            policy.WithOrigins(configuredOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetService<MeepleAiDbContext>();

    if (db == null)
    {
        // Logging tests and other scenarios without DB context
        app.Logger.LogInformation("Skipping database migrations (MeepleAiDbContext not registered)");
    }
    else if (ShouldSkipMigrations(app, db))
    {
        // Test or in-memory environments create the schema elsewhere.
    }
    else
    {
        db.Database.Migrate();

        // AI-01: Initialize Qdrant collection
        var qdrant = scope.ServiceProvider.GetRequiredService<IQdrantService>();
        await qdrant.EnsureCollectionExistsAsync();

        // Validate embedding configuration consistency
        var embedding = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var embeddingDimensions = embedding.GetEmbeddingDimensions();
        var provider = app.Configuration["EMBEDDING_PROVIDER"]?.ToLowerInvariant() ?? "ollama";
        var model = app.Configuration["EMBEDDING_MODEL"] ?? "nomic-embed-text";

        app.Logger.LogInformation("✓ Embedding configuration validated: Provider={Provider}, Model={Model}, Dimensions={Dimensions}",
            provider, model, embeddingDimensions);
    }
}

// Configure middleware pipeline
app.ConfigureMiddlewarePipeline(forwardedHeadersEnabled);

// Unversioned endpoints (keep for backwards compatibility and infrastructure)
app.MapGet("/", () => Results.Json(new { ok = true, name = "MeepleAgentAI" }));

// OPS-01: Health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                tags = e.Value.Tags
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") || check.Tags.Contains("cache") || check.Tags.Contains("vector")
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Just check if the app is running
});

// API-01: Create v1 API route group and map routing files
var v1Api = app.MapGroup("/api/v1");

v1Api.MapAuthEndpoints();
v1Api.MapGameEndpoints();
v1Api.MapAiEndpoints();
v1Api.MapPdfEndpoints();
v1Api.MapRuleSpecEndpoints();
v1Api.MapChatEndpoints();
v1Api.MapAdminEndpoints();

app.Run();

// CHAT-06: Helper method to map ChatLogEntity to ChatMessageResponse (kept for backward compatibility if needed elsewhere)
static ChatMessageResponse MapToChatMessageResponse(ChatLogEntity entity)
{
    return new ChatMessageResponse(
        entity.Id,
        entity.ChatId,
        entity.UserId,
        entity.Level,
        entity.Message,
        entity.SequenceNumber,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.IsDeleted,
        entity.DeletedAt,
        entity.DeletedByUserId,
        entity.IsInvalidated,
        entity.MetadataJson
    );
}

// OPS-01: Helper method for database migration logic
static bool ShouldSkipMigrations(WebApplication app, MeepleAiDbContext db)
{
    if (app.Environment.IsEnvironment("Testing"))
    {
        return true;
    }

    if (app.Configuration.GetValue<bool?>("SkipMigrations") == true)
    {
        return true;
    }

    var providerName = db.Database.ProviderName;
    if (providerName != null && providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var connection = db.Database.GetDbConnection();
        var connectionString = connection?.ConnectionString;
        var dataSource = connection?.DataSource;

        if (!string.IsNullOrWhiteSpace(connectionString) &&
            (connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase) ||
             connectionString.Contains("mode=memory", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(dataSource) && dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

public partial class Program { }
