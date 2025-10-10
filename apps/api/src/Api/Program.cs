using Api.Infrastructure;
using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using AspNetIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

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

// Background task execution
builder.Services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

// AI-01: Vector search services
builder.Services.AddSingleton<IQdrantClientAdapter, QdrantClientAdapter>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILlmService, LlmService>();
builder.Services.AddScoped<ITextChunkingService, TextChunkingService>();
builder.Services.AddScoped<PdfIndexingService>();

// AI-05: AI response caching
builder.Services.AddSingleton<IAiResponseCacheService, AiResponseCacheService>();

builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<RuleSpecService>();
builder.Services.AddScoped<RuleSpecDiffService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<SetupGuideService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AiRequestLogService>();
builder.Services.AddScoped<AgentFeedbackService>();
builder.Services.AddScoped<RateLimitService>();
builder.Services.AddScoped<PdfTextExtractionService>();
builder.Services.AddScoped<PdfTableExtractionService>();
builder.Services.AddScoped<PdfStorageService>();
builder.Services.AddScoped<N8nConfigService>();
builder.Services.AddScoped<ChatService>();

// CHESS-03: Chess knowledge indexing service
builder.Services.AddScoped<IChessKnowledgeService, ChessKnowledgeService>();

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
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    if (ShouldSkipMigrations(app, db))
    {
        // Test or in-memory environments create the schema elsewhere.
    }
    else
    {
        db.Database.Migrate();

        // AI-01: Initialize Qdrant collection
        var qdrant = scope.ServiceProvider.GetRequiredService<IQdrantService>();
        await qdrant.EnsureCollectionExistsAsync();
    }
}

if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
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
    var sessionCookieName = GetSessionCookieName(context);

    if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
        !string.IsNullOrWhiteSpace(token))
    {
        var auth = context.RequestServices.GetRequiredService<AuthService>();
        var session = await auth.ValidateSessionAsync(token, context.RequestAborted);
        if (session != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.User.Id),
                new(ClaimTypes.Email, session.User.Email),
                new("displayName", session.User.DisplayName ?? string.Empty),
                new(ClaimTypes.Role, session.User.Role)
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

    // Get rate limit key (prioritize authenticated user, fallback to IP)
    string rateLimitKey;
    RateLimitConfig config;

    if (context.Items.TryGetValue(nameof(ActiveSession), out var sessionObj) && sessionObj is ActiveSession session)
    {
        // Authenticated: rate limit per user + role
        rateLimitKey = $"user:{session.User.Id}";
        config = RateLimitService.GetConfigForRole(session.User.Role);
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

app.MapGet("/logs", async (HttpContext context, AiRequestLogService logService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var entries = await logService.GetRequestsAsync(limit: 100, ct: ct);

    var response = entries
        .Select(log =>
        {
            var level = string.Equals(log.Status, "Error", StringComparison.OrdinalIgnoreCase)
                ? "ERROR"
                : "INFO";

            var message = !string.IsNullOrWhiteSpace(log.ResponseSnippet)
                ? log.ResponseSnippet!
                : !string.IsNullOrWhiteSpace(log.Query)
                    ? log.Query!
                    : $"{log.Endpoint} request ({log.Status})";

            return new LogEntryResponse(
                log.CreatedAt,
                level,
                message,
                log.Id,
                log.UserId,
                log.GameId
            );
        })
        .ToList();

    return Results.Json(response);
});

app.MapPost("/auth/register", async (RegisterPayload payload, HttpContext context, AuthService auth, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        var command = new RegisterCommand(
            payload.Email,
            payload.Password,
            payload.DisplayName,
            payload.Role,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        logger.LogInformation("User registration attempt for {Email}", payload.Email);
        var result = await auth.RegisterAsync(command, ct);
        WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
        logger.LogInformation("User {UserId} registered successfully with role {Role}", result.User.Id, result.User.Role);
        return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Registration validation failed for {Email}: {Error}", payload.Email, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Registration conflict for {Email}: {Error}", payload.Email, ex.Message);
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
            payload.Email,
            payload.Password,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        logger.LogInformation("Login attempt for {Email}", payload.Email);
        var result = await auth.LoginAsync(command, ct);
        if (result == null)
        {
            logger.LogWarning("Login failed for {Email}", payload.Email);
            RemoveSessionCookie(context);
            return Results.Unauthorized();
        }

        WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
        logger.LogInformation("User {UserId} logged in successfully", result.User.Id);
        return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Login endpoint error");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

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
             connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(dataSource) &&
            dataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

app.MapPost("/auth/logout", async (HttpContext context, AuthService auth, CancellationToken ct) =>
{
    var sessionCookieName = GetSessionCookieName(context);

    if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
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

app.MapPost("/agents/qa", async (QaRequest req, HttpContext context, RagService rag, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("QA request from user {UserId} for game {GameId}: {Query}",
        session.User.Id, req.gameId, req.query);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                req.query,
                new { endpoint = "qa", gameId = req.gameId },
                ct);
        }

        var resp = await rag.AskAsync(req.gameId, req.query, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("QA response delivered for game {GameId}", req.gameId);

        string? model = null;
        string? finishReason = null;
        if (resp.metadata != null)
        {
            if (resp.metadata.TryGetValue("model", out var metadataModel))
            {
                model = metadataModel;
            }

            if (resp.metadata.TryGetValue("finish_reason", out var metadataFinish))
            {
                finishReason = metadataFinish;
            }
        }

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                resp.answer,
                new
                {
                    endpoint = "qa",
                    gameId = req.gameId,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    model,
                    finishReason,
                    snippetCount = resp.snippets.Count
                },
                ct);
        }

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa",
            req.query,
            resp.answer?.Length > 500 ? resp.answer.Substring(0, 500) : resp.answer,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: model,
            finishReason: finishReason,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process QA request: {ex.Message}",
                    new { endpoint = "qa", gameId = req.gameId, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa",
            req.query,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

app.MapPost("/agents/explain", async (ExplainRequest req, HttpContext context, RagService rag, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Explain request from user {UserId} for game {GameId}: {Topic}",
        session.User.Id, req.gameId, req.topic);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                $"Explain: {req.topic}",
                new { endpoint = "explain", gameId = req.gameId, topic = req.topic },
                ct);
        }

        var resp = await rag.ExplainAsync(req.gameId, req.topic, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Explain response delivered for game {GameId}, estimated {Minutes} min read",
            req.gameId, resp.estimatedReadingTimeMinutes);

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                resp.script,
                new
                {
                    endpoint = "explain",
                    gameId = req.gameId,
                    topic = req.topic,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    estimatedReadingTimeMinutes = resp.estimatedReadingTimeMinutes,
                    outline = resp.outline,
                    citationCount = resp.citations.Count
                },
                ct);
        }

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "explain",
            req.topic,
            resp.script?.Length > 500 ? resp.script.Substring(0, 500) : resp.script,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: null,
            finishReason: null,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process Explain request: {ex.Message}",
                    new { endpoint = "explain", gameId = req.gameId, topic = req.topic, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "explain",
            req.topic,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

// AI-03: RAG Setup Guide endpoint
app.MapPost("/agents/setup", async (SetupGuideRequest req, HttpContext context, SetupGuideService setupGuide, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Setup guide request from user {UserId} for game {GameId}",
        session.User.Id, req.gameId);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                "Generate setup guide",
                new { endpoint = "setup", gameId = req.gameId },
                ct);
        }

        var resp = await setupGuide.GenerateSetupGuideAsync(req.gameId, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Setup guide delivered for game {GameId}, {StepCount} steps, estimated {Minutes} min",
            req.gameId, resp.steps.Count, resp.estimatedSetupTimeMinutes);

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            var setupSummary = resp.steps.Count > 0
                ? string.Join("; ", resp.steps.Take(3).Select(s => $"{s.stepNumber}. {s.title}")) + (resp.steps.Count > 3 ? "..." : "")
                : "No steps generated";

            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                $"Setup guide for {resp.gameTitle}: {setupSummary}",
                new
                {
                    endpoint = "setup",
                    gameId = req.gameId,
                    gameTitle = resp.gameTitle,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    estimatedSetupTimeMinutes = resp.estimatedSetupTimeMinutes,
                    stepCount = resp.steps.Count,
                    steps = resp.steps
                },
                ct);
        }

        // ADM-01: Log AI request
        var responseSnippet = resp.steps.Count > 0
            ? string.Join("; ", resp.steps.Take(3).Select(s => s.instruction))
            : "No steps generated";
        if (responseSnippet.Length > 500)
        {
            responseSnippet = responseSnippet.Substring(0, 500);
        }

        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "setup",
            "setup_guide",
            responseSnippet,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: null,
            finishReason: null,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to generate setup guide: {ex.Message}",
                    new { endpoint = "setup", gameId = req.gameId, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "setup",
            "setup_guide",
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

app.MapPost("/agents/feedback", async (AgentFeedbackRequest req, HttpContext context, AgentFeedbackService feedbackService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(req.userId, session.User.Id, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "Invalid user" });
    }

    if (string.IsNullOrWhiteSpace(req.messageId) || string.IsNullOrWhiteSpace(req.endpoint))
    {
        return Results.BadRequest(new { error = "messageId and endpoint are required" });
    }

    try
    {
        await feedbackService.RecordFeedbackAsync(
            req.messageId,
            req.endpoint,
            session.User.Id,
            string.IsNullOrWhiteSpace(req.outcome) ? null : req.outcome,
            req.gameId,
            ct);

        logger.LogInformation(
            "Recorded feedback {Outcome} for message {MessageId} on endpoint {Endpoint} by user {UserId}",
            req.outcome ?? "cleared",
            req.messageId,
            req.endpoint,
            session.User.Id);

        return Results.Json(new { ok = true });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to record feedback for message {MessageId}", req.messageId);
        return Results.Problem(detail: "Unable to record feedback", statusCode: 500);
    }
});

app.MapPost("/ingest/pdf", async (HttpContext context, PdfStorageService pdfStorage, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
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

    logger.LogInformation("User {UserId} uploading PDF for game {GameId}", session.User.Id, gameId);

    var result = await pdfStorage.UploadPdfAsync(gameId, session.User.Id, file!, ct);

    if (!result.Success)
    {
        logger.LogWarning("PDF upload failed for game {GameId}: {Error}", gameId, result.Message);
        return Results.BadRequest(new { error = result.Message });
    }

    logger.LogInformation("PDF uploaded successfully: {PdfId}", result.Document!.Id);
    return Results.Json(new { documentId = result.Document.Id, fileName = result.Document.FileName });
});

app.MapGet("/games", async (HttpContext context, GameService gameService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
    {
        return Results.Unauthorized();
    }

    var games = await gameService.GetGamesAsync(ct);
    var response = games.Select(g => new GameResponse(g.Id, g.Name, g.CreatedAt)).ToList();
    return Results.Json(response);
});

app.MapPost("/games", async (CreateGameRequest? request, HttpContext context, GameService gameService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning(
            "User {UserId} with role {Role} attempted to create a game without permission",
            session.User.Id,
            session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request is null)
    {
        return Results.BadRequest(new { error = "Request body is required" });
    }

    try
    {
        var game = await gameService.CreateGameAsync(request.Name, request.GameId, ct);
        logger.LogInformation("Created game {GameId}", game.Id);
        return Results.Created($"/games/{game.Id}", new GameResponse(game.Id, game.Name, game.CreatedAt));
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning(ex, "Invalid game creation request");
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Conflict creating game");
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapGet("/games/{gameId}/pdfs", async (string gameId, HttpContext context, PdfStorageService pdfStorage, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdfs = await pdfStorage.GetPdfsByGameAsync(gameId, ct);
    return Results.Json(new { pdfs });
});

app.MapGet("/pdfs/{pdfId}/text", async (string pdfId, HttpContext context, MeepleAiDbContext db, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId)
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

app.MapPost("/ingest/pdf/{pdfId}/rulespec", async (string pdfId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("User {UserId} generating RuleSpec from PDF {PdfId}", session.User.Id, pdfId);
        var ruleSpec = await ruleSpecService.GenerateRuleSpecFromPdfAsync(pdfId, ct);
        return Results.Json(ruleSpec);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Unable to generate RuleSpec for PDF {PdfId}", pdfId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// AI-01: Index PDF for semantic search
app.MapPost("/ingest/pdf/{pdfId}/index", async (string pdfId, HttpContext context, PdfIndexingService indexingService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to index PDF without permission", session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("User {UserId} indexing PDF {PdfId}", session.User.Id, pdfId);

    var result = await indexingService.IndexPdfAsync(pdfId, ct);

    if (!result.Success)
    {
        logger.LogWarning("PDF indexing failed for {PdfId}: {Error}", pdfId, result.ErrorMessage);

        return result.ErrorCode switch
        {
            PdfIndexingErrorCode.PdfNotFound => Results.NotFound(new { error = result.ErrorMessage }),
            PdfIndexingErrorCode.TextExtractionRequired => Results.BadRequest(new { error = result.ErrorMessage }),
            _ => Results.BadRequest(new { error = result.ErrorMessage })
        };
    }

    logger.LogInformation("PDF {PdfId} indexed successfully: {ChunkCount} chunks", pdfId, result.ChunkCount);

    return Results.Json(new
    {
        success = true,
        vectorDocumentId = result.VectorDocumentId,
        chunkCount = result.ChunkCount,
        indexedAt = result.IndexedAt
    });
});

app.MapGet("/games/{gameId}/rulespec", async (string gameId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("Fetching RuleSpec for game {GameId}", gameId);
    var ruleSpec = await ruleSpecService.GetRuleSpecAsync(gameId, ct);

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

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to update RuleSpec without permission", session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!string.Equals(ruleSpec.gameId, gameId, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "gameId in URL does not match gameId in RuleSpec" });
    }

    try
    {
        logger.LogInformation("User {UserId} updating RuleSpec for game {GameId}", session.User.Id, gameId);
        var updated = await ruleSpecService.UpdateRuleSpecAsync(gameId, ruleSpec, session.User.Id, ct);
        logger.LogInformation("RuleSpec updated successfully for game {GameId}, version {Version}", gameId, updated.version);

        // Audit trail for RuleSpec changes
        await auditService.LogAsync(
            session.User.Id,
            "UPDATE_RULESPEC",
            "RuleSpec",
            gameId,
            "Success",
            $"Updated RuleSpec to version {updated.version}",
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            ct);

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

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Fetching RuleSpec version history for game {GameId}", gameId);
    var history = await ruleSpecService.GetVersionHistoryAsync(gameId, ct);
    return Results.Json(history);
});

// RULE-02: Get specific version
app.MapGet("/games/{gameId}/rulespec/versions/{version}", async (string gameId, string version, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Fetching RuleSpec version {Version} for game {GameId}", version, gameId);
    var ruleSpec = await ruleSpecService.GetVersionAsync(gameId, version, ct);

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

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
    {
        return Results.BadRequest(new { error = "Both 'from' and 'to' version parameters are required" });
    }

    logger.LogInformation("Computing diff between versions {FromVersion} and {ToVersion} for game {GameId}", from, to, gameId);

    var fromSpec = await ruleSpecService.GetVersionAsync(gameId, from, ct);
    var toSpec = await ruleSpecService.GetVersionAsync(gameId, to, ct);

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

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var spec = await rules.GetOrCreateDemoAsync(request.gameId, ct);
    return Results.Json(new { ok = true, spec });
});

// ADM-01: Admin dashboard endpoints
app.MapGet("/admin/requests", async (HttpContext context, AiRequestLogService logService, int limit = 100, int offset = 0, string? endpoint = null, string? userId = null, string? gameId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var requests = await logService.GetRequestsAsync(
        limit,
        offset,
        endpoint,
        userId,
        gameId,
        startDate,
        endDate,
        ct);

    return Results.Json(new { requests });
});

app.MapGet("/admin/stats", async (HttpContext context, AiRequestLogService logService, AgentFeedbackService feedbackService, DateTime? startDate = null, DateTime? endDate = null, string? userId = null, string? gameId = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var stats = await logService.GetStatsAsync(startDate, endDate, userId, gameId, ct);
    var feedbackStats = await feedbackService.GetStatsAsync(null, userId, gameId, startDate, endDate, ct);

    return Results.Json(new
    {
        stats.TotalRequests,
        stats.AvgLatencyMs,
        stats.TotalTokens,
        stats.SuccessRate,
        stats.EndpointCounts,
        feedbackCounts = feedbackStats.OutcomeCounts,
        totalFeedback = feedbackStats.TotalFeedback,
        feedbackByEndpoint = feedbackStats.EndpointOutcomeCounts
    });
});

// ADM-02: n8n workflow configuration endpoints
app.MapGet("/admin/n8n", async (HttpContext context, N8nConfigService n8nService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var configs = await n8nService.GetConfigsAsync(ct);
    return Results.Json(new { configs });
});

app.MapGet("/admin/n8n/{configId}", async (string configId, HttpContext context, N8nConfigService n8nService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var config = await n8nService.GetConfigAsync(configId, ct);

    if (config == null)
    {
        return Results.NotFound(new { error = "Configuration not found" });
    }

    return Results.Json(config);
});

app.MapPost("/admin/n8n", async (CreateN8nConfigRequest request, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} creating n8n config: {Name}", session.User.Id, request.Name);
        var config = await n8nService.CreateConfigAsync(session.User.Id, request, ct);
        logger.LogInformation("n8n config {ConfigId} created successfully", config.Id);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPut("/admin/n8n/{configId}", async (string configId, UpdateN8nConfigRequest request, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} updating n8n config {ConfigId}", session.User.Id, configId);
        var config = await n8nService.UpdateConfigAsync(configId, request, ct);
        logger.LogInformation("n8n config {ConfigId} updated successfully", config.Id);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/admin/n8n/{configId}", async (string configId, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} deleting n8n config {ConfigId}", session.User.Id, configId);
    var deleted = await n8nService.DeleteConfigAsync(configId, ct);

    if (!deleted)
    {
        return Results.NotFound(new { error = "Configuration not found" });
    }

    logger.LogInformation("n8n config {ConfigId} deleted successfully", configId);
    return Results.Json(new { ok = true });
});

app.MapPost("/admin/n8n/{configId}/test", async (string configId, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} testing n8n config {ConfigId}", session.User.Id, configId);
        var result = await n8nService.TestConnectionAsync(configId, ct);
        logger.LogInformation("n8n config {ConfigId} test result: {Success}", configId, result.Success);
        return Results.Json(result);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to test n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// CHESS-03: Chess knowledge indexing endpoints
app.MapPost("/chess/index", async (HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to index chess knowledge without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session.User.Id);

    var result = await chessService.IndexChessKnowledgeAsync(ct);

    if (!result.Success)
    {
        logger.LogError("Chess knowledge indexing failed: {Error}", result.ErrorMessage);
        return Results.BadRequest(new { error = result.ErrorMessage });
    }

    logger.LogInformation("Chess knowledge indexing completed: {TotalItems} items, {TotalChunks} chunks",
        result.TotalKnowledgeItems, result.TotalChunks);

    return Results.Json(new
    {
        success = true,
        totalItems = result.TotalKnowledgeItems,
        totalChunks = result.TotalChunks,
        categoryCounts = result.CategoryCounts
    });
});

app.MapGet("/chess/search", async (string? q, int? limit, HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { error = "Query parameter 'q' is required" });
    }

    logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session.User.Id, q);

    var searchResult = await chessService.SearchChessKnowledgeAsync(q, limit ?? 5, ct);

    if (!searchResult.Success)
    {
        logger.LogError("Chess knowledge search failed: {Error}", searchResult.ErrorMessage);
        return Results.BadRequest(new { error = searchResult.ErrorMessage });
    }

    logger.LogInformation("Chess knowledge search completed: {ResultCount} results", searchResult.Results.Count);

    return Results.Json(new
    {
        success = true,
        results = searchResult.Results.Select(r => new
        {
            score = r.Score,
            text = r.Text,
            page = r.Page,
            chunkIndex = r.ChunkIndex
        })
    });
});

app.MapDelete("/chess/index", async (HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to delete chess knowledge without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} deleting all chess knowledge", session.User.Id);

    var success = await chessService.DeleteChessKnowledgeAsync(ct);

    if (!success)
    {
        logger.LogError("Chess knowledge deletion failed");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    logger.LogInformation("Chess knowledge deletion completed successfully");
    return Results.Json(new { success = true });
});

// UI-01: Chat management endpoints
app.MapGet("/chats", async (HttpContext context, ChatService chatService, string? gameId, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var chats = string.IsNullOrWhiteSpace(gameId)
        ? await chatService.GetUserChatsAsync(session.User.Id, 50, ct)
        : await chatService.GetUserChatsByGameAsync(session.User.Id, gameId, 50, ct);

    var response = chats.Select(c => new ChatDto(
        c.Id,
        c.GameId,
        c.Game.Name,
        c.AgentId,
        c.Agent.Name,
        c.StartedAt,
        c.LastMessageAt
    )).ToList();

    return Results.Json(response);
});

app.MapGet("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var chat = await chatService.GetChatByIdAsync(chatId, session.User.Id, ct);
    if (chat == null)
    {
        return Results.NotFound(new { error = "Chat not found" });
    }

    var messages = chat.Logs.Select(l => new ChatMessageDto(
        l.Id,
        l.Level,
        l.Message,
        l.MetadataJson,
        l.CreatedAt
    )).ToList();

    var response = new ChatWithHistoryDto(
        chat.Id,
        chat.GameId,
        chat.Game.Name,
        chat.AgentId,
        chat.Agent.Name,
        chat.StartedAt,
        chat.LastMessageAt,
        messages
    );

    return Results.Json(response);
});

app.MapPost("/chats", async (CreateChatRequest? request, HttpContext context, ChatService chatService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (request == null)
    {
        return Results.BadRequest(new { error = "Request body is required" });
    }

    if (string.IsNullOrWhiteSpace(request.GameId) || string.IsNullOrWhiteSpace(request.AgentId))
    {
        return Results.BadRequest(new { error = "GameId and AgentId are required" });
    }

    try
    {
        var chat = await chatService.CreateChatAsync(session.User.Id, request.GameId, request.AgentId, ct);

        // Reload with navigations
        var fullChat = await chatService.GetChatByIdAsync(chat.Id, session.User.Id, ct);
        if (fullChat == null)
        {
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var response = new ChatDto(
            fullChat.Id,
            fullChat.GameId,
            fullChat.Game.Name,
            fullChat.AgentId,
            fullChat.Agent.Name,
            fullChat.StartedAt,
            fullChat.LastMessageAt
        );

        logger.LogInformation("User {UserId} created chat {ChatId} for game {GameId}", session.User.Id, chat.Id, request.GameId);
        return Results.Created($"/chats/{chat.Id}", response);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Invalid chat creation request");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    try
    {
        var deleted = await chatService.DeleteChatAsync(chatId, session.User.Id, ct);
        if (!deleted)
        {
            return Results.NotFound(new { error = "Chat not found" });
        }

        logger.LogInformation("User {UserId} deleted chat {ChatId}", session.User.Id, chatId);
        return Results.NoContent();
    }
    catch (UnauthorizedAccessException ex)
    {
        logger.LogWarning(ex, "Unauthorized chat deletion attempt");
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
});

app.MapGet("/games/{gameId}/agents", async (string gameId, HttpContext context, ChatService chatService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
    {
        return Results.Unauthorized();
    }

    var agents = await chatService.GetAgentsForGameAsync(gameId, ct);
    var response = agents.Select(a => new AgentDto(
        a.Id,
        a.GameId,
        a.Name,
        a.Kind,
        a.CreatedAt
    )).ToList();

    return Results.Json(response);
});

app.Run();

static CookieOptions CreateSessionCookieOptions(HttpContext context, DateTime expiresAt)
{
    var options = BuildSessionCookieOptions(context);
    options.Expires = expiresAt;
    return options;
}

static void WriteSessionCookie(HttpContext context, string token, DateTime expiresAt)
{
    var options = CreateSessionCookieOptions(context, expiresAt);
    var sessionCookieName = GetSessionCookieName(context);
    context.Response.Cookies.Append(sessionCookieName, token, options);
}

static void RemoveSessionCookie(HttpContext context)
{
    var options = BuildSessionCookieOptions(context);
    options.Expires = DateTimeOffset.UnixEpoch;

    var sessionCookieName = GetSessionCookieName(context);
    context.Response.Cookies.Delete(sessionCookieName, options);
}

static CookieOptions BuildSessionCookieOptions(HttpContext context)
{
    var configuration = GetSessionCookieConfiguration(context);

    var isHttps = context.Request.IsHttps;

    if (!isHttps && configuration.UseForwardedProto &&
        context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
    {
        foreach (var proto in forwardedProto)
        {
            if (string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase))
            {
                isHttps = true;
                break;
            }
        }
    }

    var secure = configuration.Secure ?? isHttps;
    var secureForced = false;

    if (!secure && !configuration.Secure.HasValue)
    {
        secure = true;
        secureForced = true;
    }

    var sameSite = configuration.SameSite ?? (secure ? SameSiteMode.None : SameSiteMode.Lax);

    if (secureForced && sameSite != SameSiteMode.None)
    {
        sameSite = SameSiteMode.None;
    }
    var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;

    var options = new CookieOptions
    {
        HttpOnly = configuration.HttpOnly,
        Secure = secure,
        SameSite = sameSite,
        Path = path
    };

    if (!string.IsNullOrWhiteSpace(configuration.Domain))
    {
        options.Domain = configuration.Domain;
    }

    return options;
}

static string GetSessionCookieName(HttpContext context)
{
    var configuration = GetSessionCookieConfiguration(context);
    return string.IsNullOrWhiteSpace(configuration.Name)
        ? AuthService.SessionCookieName
        : configuration.Name;
}

static SessionCookieConfiguration GetSessionCookieConfiguration(HttpContext context)
{
    return context.RequestServices
        .GetRequiredService<IOptions<SessionCookieConfiguration>>()
        .Value;
}
public partial class Program { }
