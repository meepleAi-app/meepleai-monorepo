using Api.Infrastructure;
using System.Security.Claims;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["ConnectionStrings__Postgres"]
    ?? throw new InvalidOperationException("Missing Postgres connection string");

builder.Services.AddDbContext<MeepleAiDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Redis
var redisUrl = builder.Configuration["REDIS_URL"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisUrl);
    configuration.AbortOnConnectFail = false; // Fail gracefully if Redis unavailable
    return ConnectionMultiplexer.Connect(configuration);
});

// Configure HttpClient for EmbeddingService
builder.Services.AddHttpClient();

// AUTH-02: Tenant context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Background task execution
builder.Services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

// AI-01: Vector search services
builder.Services.AddSingleton<IQdrantService, QdrantService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<TextChunkingService>();

builder.Services.AddScoped<RuleSpecService>();
builder.Services.AddScoped<RuleSpecDiffService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<RateLimitService>();
builder.Services.AddScoped<PdfTextExtractionService>();
builder.Services.AddScoped<PdfTableExtractionService>();
builder.Services.AddScoped<PdfStorageService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.WithOrigins("http://localhost:3000");
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    // Use EnsureCreated for testing, Migrate for production
    if (app.Environment.IsEnvironment("Testing"))
    {
        // Test environment: database is already created by WebApplicationFactory
    }
    else
    {
        db.Database.Migrate();

        // AI-01: Initialize Qdrant collection
        var qdrant = scope.ServiceProvider.GetRequiredService<QdrantService>();
        await qdrant.EnsureCollectionExistsAsync();
    }
}

app.UseCors("web");

// Request logging with correlation ID
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
        diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("RemoteIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserId", httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            diagnosticContext.Set("UserEmail", httpContext.User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown");
            diagnosticContext.Set("TenantId", httpContext.User.FindFirst("tenant")?.Value ?? "unknown");
        }
    };
});

// Add correlation ID to response headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
    await next();
});

// Authentication middleware
app.Use(async (context, next) =>
{
    if (context.Request.Cookies.TryGetValue(AuthService.SessionCookieName, out var token) &&
        !string.IsNullOrWhiteSpace(token))
    {
        var auth = context.RequestServices.GetRequiredService<AuthService>();
        var session = await auth.ValidateSessionAsync(token, context.RequestAborted);
        if (session != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.User.id),
                new(ClaimTypes.Email, session.User.email),
                new("tenant", session.User.tenantId),
                new("displayName", session.User.displayName ?? string.Empty),
                new(ClaimTypes.Role, session.User.role)
            };
            var identity = new ClaimsIdentity(claims, "session");
            context.User = new ClaimsPrincipal(identity);
            context.Items[nameof(ActiveSession)] = session;
        }
    }

    await next();
});

// Rate limiting middleware
app.Use(async (context, next) =>
{
    var rateLimiter = context.RequestServices.GetRequiredService<RateLimitService>();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Get rate limit key (prioritize tenant, fallback to IP)
    string rateLimitKey;
    RateLimitConfig config;

    if (context.Items.TryGetValue(nameof(ActiveSession), out var sessionObj) && sessionObj is ActiveSession session)
    {
        // Authenticated: rate limit per tenant + role
        rateLimitKey = $"tenant:{session.User.tenantId}";
        config = RateLimitService.GetConfigForRole(session.User.role);
    }
    else
    {
        // Anonymous: rate limit per IP
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        rateLimitKey = $"ip:{ip}";
        config = RateLimitService.GetConfigForRole(null); // Default anonymous limits
    }

    var result = await rateLimiter.CheckRateLimitAsync(
        rateLimitKey,
        config.MaxTokens,
        config.RefillRate,
        context.RequestAborted);

    // Add rate limit headers
    context.Response.Headers["X-RateLimit-Limit"] = config.MaxTokens.ToString();
    context.Response.Headers["X-RateLimit-Remaining"] = result.TokensRemaining.ToString();

    if (!result.Allowed)
    {
        context.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
        context.Response.StatusCode = 429; // Too Many Requests

        logger.LogWarning("Rate limit exceeded for {Key}. Retry after {RetryAfter}s",
            rateLimitKey, result.RetryAfterSeconds);

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded",
            retryAfter = result.RetryAfterSeconds,
            message = $"Too many requests. Please try again in {result.RetryAfterSeconds} seconds."
        });
        return;
    }

    await next();
});

app.MapGet("/", () => Results.Json(new { ok = true, name = "MeepleAgentAI" }));

app.MapPost("/auth/register", async (RegisterPayload payload, HttpContext context, AuthService auth, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        var command = new RegisterCommand(
            payload.tenantId,
            payload.tenantName,
            payload.email,
            payload.password,
            payload.displayName,
            payload.role,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        logger.LogInformation("User registration attempt for {Email} in tenant {TenantId}", payload.email, payload.tenantId);
        var result = await auth.RegisterAsync(command, ct);
        WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
        logger.LogInformation("User {UserId} registered successfully with role {Role}", result.User.id, result.User.role);
        return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Registration validation failed for {Email}: {Error}", payload.email, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Registration conflict for {Email}: {Error}", payload.email, ex.Message);
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/auth/login", async (LoginPayload? payload, HttpContext context, AuthService auth, ILogger<Program> logger, CancellationToken ct) =>
{
    if (payload == null)
    {
        logger.LogWarning("Login failed: payload is null");
        return Results.BadRequest(new { error = "Invalid request payload" });
    }

    try
    {
        var command = new LoginCommand(
            payload.tenantId,
            payload.email,
            payload.password,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        logger.LogInformation("Login attempt for {Email} in tenant {TenantId}", payload.email, payload.tenantId);
        var result = await auth.LoginAsync(command, ct);
        if (result == null)
        {
            logger.LogWarning("Login failed for {Email} in tenant {TenantId}", payload.email, payload.tenantId);
            RemoveSessionCookie(context);
            return Results.Unauthorized();
        }

        WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
        logger.LogInformation("User {UserId} logged in successfully", result.User.id);
        return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Login endpoint error");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapPost("/auth/logout", async (HttpContext context, AuthService auth, CancellationToken ct) =>
{
    if (context.Request.Cookies.TryGetValue(AuthService.SessionCookieName, out var token) &&
        !string.IsNullOrWhiteSpace(token))
    {
        await auth.LogoutAsync(token, ct);
    }

    RemoveSessionCookie(context);
    return Results.Json(new { ok = true });
});

app.MapGet("/auth/me", (HttpContext context) =>
{
    if (context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession session)
    {
        return Results.Json(new AuthResponse(session.User, session.ExpiresAt));
    }

    return Results.Unauthorized();
});

app.MapPost("/agents/qa", async (QaRequest req, HttpContext context, RagService rag, AuditService audit, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(req.tenantId, session.User.tenantId, StringComparison.Ordinal))
    {
        await audit.LogTenantAccessDeniedAsync(
            session.User.tenantId,
            req.tenantId,
            session.User.id,
            "qa_endpoint",
            req.gameId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            ct);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(req.tenantId) || string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "tenantId and gameId are required" });
    }

    logger.LogInformation("QA request from user {UserId} for game {GameId}: {Query}",
        session.User.id, req.gameId, req.query);
    var resp = await rag.AskAsync(req.tenantId, req.gameId, req.query, ct);
    logger.LogInformation("QA response delivered for game {GameId}", req.gameId);
    return Results.Json(resp);
});

app.MapPost("/agents/explain", async (ExplainRequest req, HttpContext context, RagService rag, AuditService audit, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(req.tenantId, session.User.tenantId, StringComparison.Ordinal))
    {
        await audit.LogTenantAccessDeniedAsync(
            session.User.tenantId,
            req.tenantId,
            session.User.id,
            "explain_endpoint",
            req.gameId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            ct);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(req.tenantId) || string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "tenantId and gameId are required" });
    }

    logger.LogInformation("Explain request from user {UserId} for game {GameId}: {Topic}",
        session.User.id, req.gameId, req.topic);
    var resp = await rag.ExplainAsync(req.tenantId, req.gameId, req.topic, ct);
    logger.LogInformation("Explain response delivered for game {GameId}, estimated {Minutes} min read",
        req.gameId, resp.estimatedReadingTimeMinutes);
    return Results.Json(resp);
});

app.MapPost("/ingest/pdf", async (HttpContext context, PdfStorageService pdfStorage, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var form = await context.Request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    var gameId = form["gameId"].ToString();

    if (string.IsNullOrWhiteSpace(gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    logger.LogInformation("User {UserId} uploading PDF for game {GameId}", session.User.id, gameId);

    var result = await pdfStorage.UploadPdfAsync(session.User.tenantId, gameId, session.User.id, file!, ct);

    if (!result.Success)
    {
        logger.LogWarning("PDF upload failed for game {GameId}: {Error}", gameId, result.Message);
        return Results.BadRequest(new { error = result.Message });
    }

    logger.LogInformation("PDF uploaded successfully: {PdfId}", result.Document!.Id);
    return Results.Json(new { documentId = result.Document.Id, fileName = result.Document.FileName });
});

app.MapGet("/games/{gameId}/pdfs", async (string gameId, HttpContext context, PdfStorageService pdfStorage, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdfs = await pdfStorage.GetPdfsByGameAsync(session.User.tenantId, gameId, ct);
    return Results.Json(new { pdfs });
});

app.MapGet("/pdfs/{pdfId}/text", async (string pdfId, HttpContext context, MeepleAiDbContext db, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId && p.TenantId == session.User.tenantId)
        .Select(p => new
        {
            p.Id,
            p.FileName,
            p.ExtractedText,
            p.ProcessingStatus,
            p.ProcessedAt,
            p.PageCount,
            p.CharacterCount,
            p.ProcessingError
        })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    return Results.Json(pdf);
});

app.MapGet("/games/{gameId}/rulespec", async (string gameId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("Fetching RuleSpec for game {GameId} in tenant {TenantId}", gameId, session.User.tenantId);
    var ruleSpec = await ruleSpecService.GetRuleSpecAsync(session.User.tenantId, gameId, ct);

    if (ruleSpec == null)
    {
        logger.LogInformation("RuleSpec not found for game {GameId}", gameId);
        return Results.NotFound(new { error = "RuleSpec not found" });
    }

    return Results.Json(ruleSpec);
});

app.MapPut("/games/{gameId}/rulespec", async (string gameId, RuleSpec ruleSpec, HttpContext context, RuleSpecService ruleSpecService, AuditService auditService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to update RuleSpec without permission", session.User.id, session.User.role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!string.Equals(ruleSpec.gameId, gameId, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "gameId in URL does not match gameId in RuleSpec" });
    }

    try
    {
        logger.LogInformation("User {UserId} updating RuleSpec for game {GameId}", session.User.id, gameId);
        var updated = await ruleSpecService.UpdateRuleSpecAsync(session.User.tenantId, gameId, ruleSpec, ct);
        logger.LogInformation("RuleSpec updated successfully for game {GameId}, version {Version}", gameId, updated.version);

        // Audit trail for RuleSpec changes
        await auditService.LogAsync(
            session.User.tenantId,
            session.User.id,
            "UPDATE_RULESPEC",
            "RuleSpec",
            gameId,
            "Success",
            $"Updated RuleSpec to version {updated.version}",
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        return Results.Json(updated);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update RuleSpec for game {GameId}: {Error}", gameId, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// RULE-02: Get version history
app.MapGet("/games/{gameId}/rulespec/history", async (string gameId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("Fetching RuleSpec version history for game {GameId}", gameId);
    var history = await ruleSpecService.GetVersionHistoryAsync(session.User.tenantId, gameId, ct);
    return Results.Json(history);
});

// RULE-02: Get specific version
app.MapGet("/games/{gameId}/rulespec/versions/{version}", async (string gameId, string version, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("Fetching RuleSpec version {Version} for game {GameId}", version, gameId);
    var ruleSpec = await ruleSpecService.GetVersionAsync(session.User.tenantId, gameId, version, ct);

    if (ruleSpec == null)
    {
        logger.LogInformation("RuleSpec version {Version} not found for game {GameId}", version, gameId);
        return Results.NotFound(new { error = "RuleSpec version not found" });
    }

    return Results.Json(ruleSpec);
});

// RULE-02: Compare two versions (diff)
app.MapGet("/games/{gameId}/rulespec/diff", async (string gameId, string? from, string? to, HttpContext context, RuleSpecService ruleSpecService, RuleSpecDiffService diffService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
    {
        return Results.BadRequest(new { error = "Both 'from' and 'to' version parameters are required" });
    }

    logger.LogInformation("Computing diff between versions {FromVersion} and {ToVersion} for game {GameId}", from, to, gameId);

    var fromSpec = await ruleSpecService.GetVersionAsync(session.User.tenantId, gameId, from, ct);
    var toSpec = await ruleSpecService.GetVersionAsync(session.User.tenantId, gameId, to, ct);

    if (fromSpec == null || toSpec == null)
    {
        return Results.NotFound(new { error = "One or both RuleSpec versions not found" });
    }

    var diff = diffService.ComputeDiff(fromSpec, toSpec);
    return Results.Json(diff);
});

app.MapPost("/admin/seed", async (SeedRequest request, HttpContext context, RuleSpecService rules, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!string.Equals(session.User.tenantId, request.tenantId, StringComparison.Ordinal))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.tenantId) || string.IsNullOrWhiteSpace(request.gameId))
    {
        return Results.BadRequest(new { error = "tenantId and gameId are required" });
    }

    var spec = await rules.GetOrCreateDemoAsync(request.tenantId, request.gameId, ct);
    return Results.Json(new { ok = true, spec });
});

app.Run();

static void WriteSessionCookie(HttpContext context, string token, DateTime expiresAt)
{
    var options = new CookieOptions
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = new DateTimeOffset(expiresAt)
    };

    context.Response.Cookies.Append(AuthService.SessionCookieName, token, options);
}

static void RemoveSessionCookie(HttpContext context)
{
    var options = new CookieOptions
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = DateTimeOffset.UnixEpoch
    };

    context.Response.Cookies.Delete(AuthService.SessionCookieName, options);
}
public partial class Program { }
