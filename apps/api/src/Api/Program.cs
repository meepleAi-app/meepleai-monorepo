using Api.Infrastructure;
using System.Security.Claims;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["ConnectionStrings__Postgres"]
    ?? throw new InvalidOperationException("Missing Postgres connection string");

builder.Services.AddDbContext<MeepleAiDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<RuleSpecService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<AuthService>();

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

app.MapGet("/", () => Results.Json(new { ok = true, name = "MeepleAgentAI" }));

app.MapPost("/auth/register", async (RegisterPayload payload, HttpContext context, AuthService auth, CancellationToken ct) =>
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

        var result = await auth.RegisterAsync(command, ct);
        WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
        return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/auth/login", async (LoginPayload payload, HttpContext context, AuthService auth, CancellationToken ct) =>
{
    var command = new LoginCommand(
        payload.tenantId,
        payload.email,
        payload.password,
        context.Connection.RemoteIpAddress?.ToString(),
        context.Request.Headers.UserAgent.ToString());

    var result = await auth.LoginAsync(command, ct);
    if (result == null)
    {
        RemoveSessionCookie(context);
        return Results.Unauthorized();
    }

    WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
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

app.MapPost("/agents/qa", async (QaRequest req, HttpContext context, RagService rag, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(req.tenantId, session.User.tenantId, StringComparison.Ordinal))
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(req.tenantId) || string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "tenantId and gameId are required" });
    }

    var resp = await rag.AskAsync(req.tenantId, req.gameId, req.query, ct);
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
