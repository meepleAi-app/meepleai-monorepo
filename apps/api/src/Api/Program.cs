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

builder.Services.AddScoped<RuleSpecService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RateLimitService>();

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
    db.Database.Migrate();
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

app.MapPost("/auth/login", async (LoginPayload payload, HttpContext context, AuthService auth, ILogger<Program> logger, CancellationToken ct) =>
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

app.MapPost("/agents/qa", async (QaRequest req, HttpContext context, RagService rag, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(req.tenantId, session.User.tenantId, StringComparison.Ordinal))
    {
        logger.LogWarning("Tenant mismatch: User {UserId} attempted to access tenant {RequestedTenantId}",
            session.User.id, req.tenantId);
        return Results.Forbid();
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

app.MapPost("/ingest/pdf", (HttpContext context) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    return Results.Json(new IngestPdfResponse(Guid.NewGuid().ToString("N")));
});

app.MapPost("/admin/seed", async (SeedRequest request, HttpContext context, RuleSpecService rules, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    if (!string.Equals(session.User.tenantId, request.tenantId, StringComparison.Ordinal))
    {
        return Results.Forbid();
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
