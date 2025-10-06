using Api.Infrastructure;
using System;
using System.Linq;
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

// Background task execution
builder.Services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

// AI-01: Vector search services
builder.Services.AddSingleton<IQdrantClientAdapter, QdrantClientAdapter>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILlmService, LlmService>();
builder.Services.AddScoped<ITextChunkingService, TextChunkingService>();

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
        var qdrant = scope.ServiceProvider.GetRequiredService<IQdrantService>();
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

    // Get rate limit key (prioritize authenticated user, fallback to IP)
    string rateLimitKey;
    RateLimitConfig config;

    if (context.Items.TryGetValue(nameof(ActiveSession), out var sessionObj) && sessionObj is ActiveSession session)
    {
        // Authenticated: rate limit per user + role
        rateLimitKey = $"user:{session.User.id}";
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

app.MapGet("/logs", async (AiRequestLogService logService, CancellationToken ct) =>
{
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
            payload.email,
            payload.password,
            payload.displayName,
            payload.role,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        logger.LogInformation("User registration attempt for {Email}", payload.email);
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
            payload.email,
            payload.password,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        logger.LogInformation("Login attempt for {Email}", payload.email);
        var result = await auth.LoginAsync(command, ct);
        if (result == null)
        {
            logger.LogWarning("Login failed for {Email}", payload.email);
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

app.MapPost("/agents/qa", async (QaRequest req, HttpContext context, RagService rag, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
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
        session.User.id, req.gameId, req.query);

    try
    {
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

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.id,
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

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.id,
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

app.MapPost("/agents/explain", async (ExplainRequest req, HttpContext context, RagService rag, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
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
        session.User.id, req.gameId, req.topic);

    try
    {
        var resp = await rag.ExplainAsync(req.gameId, req.topic, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Explain response delivered for game {GameId}, estimated {Minutes} min read",
            req.gameId, resp.estimatedReadingTimeMinutes);

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.id,
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

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.id,
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
app.MapPost("/agents/setup", async (SetupGuideRequest req, HttpContext context, SetupGuideService setupGuide, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
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
        session.User.id, req.gameId);

    try
    {
        var resp = await setupGuide.GenerateSetupGuideAsync(req.gameId, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Setup guide delivered for game {GameId}, {StepCount} steps, estimated {Minutes} min",
            req.gameId, resp.steps.Count, resp.estimatedSetupTimeMinutes);

        // ADM-01: Log AI request
        var responseSnippet = resp.steps.Count > 0
            ? string.Join("; ", resp.steps.Take(3).Select(s => s.instruction))
            : "No steps generated";
        if (responseSnippet.Length > 500)
        {
            responseSnippet = responseSnippet.Substring(0, 500);
        }

        await aiLog.LogRequestAsync(
            session.User.id,
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

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.id,
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

    if (!string.Equals(req.userId, session.User.id, StringComparison.Ordinal))
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
            session.User.id,
            string.IsNullOrWhiteSpace(req.outcome) ? null : req.outcome,
            req.gameId,
            ct);

        logger.LogInformation(
            "Recorded feedback {Outcome} for message {MessageId} on endpoint {Endpoint} by user {UserId}",
            req.outcome ?? "cleared",
            req.messageId,
            req.endpoint,
            session.User.id);

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

    var result = await pdfStorage.UploadPdfAsync(gameId, session.User.id, file!, ct);

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
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
    {
        return Results.Unauthorized();
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("User {UserId} generating RuleSpec from PDF {PdfId}", session.User.id, pdfId);
        var ruleSpec = await ruleSpecService.GenerateRuleSpecFromPdfAsync(pdfId, ct);
        return Results.Json(ruleSpec);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Unable to generate RuleSpec for PDF {PdfId}", pdfId);
        return Results.BadRequest(new { error = ex.Message });
    }
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
        var updated = await ruleSpecService.UpdateRuleSpecAsync(gameId, ruleSpec, session.User.id, ct);
        logger.LogInformation("RuleSpec updated successfully for game {GameId}, version {Version}", gameId, updated.version);

        // Audit trail for RuleSpec changes
        await auditService.LogAsync(
            session.User.id,
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} creating n8n config: {Name}", session.User.id, request.Name);
        var config = await n8nService.CreateConfigAsync(session.User.id, request, ct);
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} updating n8n config {ConfigId}", session.User.id, configId);
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} deleting n8n config {ConfigId}", session.User.id, configId);
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

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} testing n8n config {ConfigId}", session.User.id, configId);
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
