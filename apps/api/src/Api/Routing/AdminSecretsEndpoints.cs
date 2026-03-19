using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;

namespace Api.Routing;

/// <summary>
/// Admin secrets management endpoints.
/// Allows viewing, editing .secret files on disk, and restarting the API to apply changes.
/// Staging/dev only — not for production use.
/// </summary>
internal static class AdminSecretsEndpoints
{
    private static readonly HashSet<string> InfraFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "database.secret", "redis.secret", "qdrant.secret", "jwt.secret", "admin.secret"
    };

    private static readonly char[] TitleCaseSeparators = ['-', '_'];

    private static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openrouter.secret"] = "OpenRouter",
        ["database.secret"] = "Database",
        ["redis.secret"] = "Redis",
        ["qdrant.secret"] = "Qdrant",
        ["jwt.secret"] = "JWT",
        ["admin.secret"] = "Admin",
        ["oauth.secret"] = "OAuth",
        ["bgg.secret"] = "BGG",
        ["email.secret"] = "Email",
        ["embedding-service.secret"] = "Embedding Service",
        ["reranker-service.secret"] = "Reranker Service",
        ["smoldocling-service.secret"] = "SmolDocling",
        ["unstructured-service.secret"] = "Unstructured",
        ["monitoring.secret"] = "Monitoring",
        ["storage.secret"] = "Storage",
        ["slack.secret"] = "Slack",
        ["traefik.secret"] = "Traefik",
        ["n8n.secret"] = "n8n",
    };

    public static IEndpointRouteBuilder MapAdminSecretsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/secrets")
            .WithTags("Admin", "Secrets")
            .RequireAuthorization();

        group.MapGet("/", HandleGetSecrets).WithName("GetSecrets");
        group.MapPut("/", HandleUpdateSecrets).WithName("UpdateSecrets");
        group.MapPost("/restart", HandleRestart).WithName("RestartApi");

        return app;
    }

    private static IResult HandleGetSecrets(HttpContext context, IConfiguration config, ILogger<Program> logger)
    {
        var (authorized, session, error) = context.RequireSuperAdminSession();
        if (!authorized) return error!;

        var reveal = context.Request.Query.ContainsKey("reveal");
        var secretsDir = GetSecretsDirectory(config);
        if (secretsDir == null)
            return Results.Problem("SECRETS_DIRECTORY not configured", statusCode: 500);

        var files = Directory.GetFiles(secretsDir, "*.secret")
            .OrderBy(f => InfraFiles.Contains(Path.GetFileName(f)) ? 1 : 0)
            .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .Select(filePath =>
            {
                var fileName = Path.GetFileName(filePath);
                var lines = File.ReadAllLines(filePath);
                var entries = lines
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#') && l.Contains('='))
                    .Select(l =>
                    {
                        var eqIdx = l.IndexOf('=');
                        var key = l[..eqIdx].Trim();
                        var value = l[(eqIdx + 1)..].Trim();
                        return new
                        {
                            key,
                            maskedValue = reveal ? value : MaskValue(value),
                            hasValue = !string.IsNullOrEmpty(value),
                            isPlaceholder = IsPlaceholder(value),
                        };
                    })
                    .ToList();

                return new
                {
                    fileName,
                    category = CategoryMap.GetValueOrDefault(fileName, TitleCase(fileName.Replace(".secret", ""))),
                    isInfra = InfraFiles.Contains(fileName),
                    entries,
                };
            })
            .ToList();

        logger.LogInformation("Admin {UserId} viewed secrets ({FileCount} files)", session!.User!.Id, files.Count);
        return Results.Ok(new { secretsDirectory = secretsDir, files });
    }

    private static async Task<IResult> HandleUpdateSecrets(
        HttpContext context, IConfiguration config, ILogger<Program> logger, CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireSuperAdminSession();
        if (!authorized) return error!;

        var secretsDir = GetSecretsDirectory(config);
        if (secretsDir == null)
            return Results.Problem("SECRETS_DIRECTORY not configured", statusCode: 500);

        var body = await context.Request.ReadFromJsonAsync<UpdateSecretsRequest>(ct).ConfigureAwait(false);
        if (body?.Updates == null || body.Updates.Count == 0)
            return Results.BadRequest(new { error = "No updates provided" });

        var updatedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var update in body.Updates)
        {
            // Path traversal: canonicalize and verify stays inside secretsDir
            var filePath = Path.GetFullPath(Path.Combine(secretsDir, update.FileName));
            if (!filePath.StartsWith(secretsDir, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"Invalid fileName: {update.FileName}" });

            if (!System.Text.RegularExpressions.Regex.IsMatch(update.Key, @"^[A-Za-z][A-Za-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
                return Results.BadRequest(new { error = $"Invalid key format: {update.Key}" });

            if (!File.Exists(filePath))
                return Results.BadRequest(new { error = $"File not found: {update.FileName}" });

            // Read original text to preserve line endings
            var originalText = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            var lineEnding = originalText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = originalText.Split(lineEnding).ToList();

            var found = false;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWith('#') || !lines[i].Contains('=', StringComparison.Ordinal)) continue;
                var eqIdx = lines[i].IndexOf('=', StringComparison.Ordinal);
                var lineKey = lines[i][..eqIdx].Trim();
                if (string.Equals(lineKey, update.Key, StringComparison.Ordinal))
                {
                    lines[i] = $"{update.Key}={update.Value}";
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                lines.Add($"{update.Key}={update.Value}");
            }

            await File.WriteAllTextAsync(filePath, string.Join(lineEnding, lines), ct).ConfigureAwait(false);
            updatedFiles.Add(update.FileName);

            logger.LogInformation("Admin {UserId} updated secret {Key} in {File}",
                session!.User!.Id, update.Key, update.FileName);
        }

        return Results.Ok(new { updatedFiles = updatedFiles.ToList(), updatedKeys = body.Updates.Count });
    }

    private static IResult HandleRestart(
        HttpContext context, IHostApplicationLifetime lifetime, ILogger<Program> logger)
    {
        var (authorized, session, error) = context.RequireSuperAdminSession();
        if (!authorized) return error!;

        logger.LogWarning("Admin {UserId} initiated API restart via secrets management", session!.User!.Id);

        _ = Task.Run(async () =>
        {
            await Task.Delay(2000).ConfigureAwait(false);
            lifetime.StopApplication();
        });

        return Results.Accepted(value: new
        {
            message = "API restart initiated. Service will be back in ~10 seconds.",
            restartedAt = DateTime.UtcNow,
        });
    }

    private static string? GetSecretsDirectory(IConfiguration config)
    {
        var dir = config["SECRETS_MANAGEMENT_DIR"]
            ?? Environment.GetEnvironmentVariable("SECRETS_MANAGEMENT_DIR")
            ?? config["SECRETS_DIRECTORY"]
            ?? Environment.GetEnvironmentVariable("SECRETS_DIRECTORY");
        if (string.IsNullOrEmpty(dir)) return null;

        if (!Path.IsPathRooted(dir))
            dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dir));

        return Directory.Exists(dir) ? dir : null;
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= 4) return "****";
        if (value.Length <= 10) return $"{value[..2]}****{value[^2..]}";
        return $"{value[..6]}****{value[^4..]}";
    }

    private static bool IsPlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
            || value.Contains("change_me", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("your_", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "changeme", StringComparison.OrdinalIgnoreCase);
    }

    private static string TitleCase(string s) =>
        string.IsNullOrEmpty(s) ? s :
        string.Join(' ', s.Split(TitleCaseSeparators, StringSplitOptions.RemoveEmptyEntries).Select(w =>
            w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w));

    private sealed record UpdateSecretsRequest(List<SecretUpdate> Updates);
    private sealed record SecretUpdate(string FileName, string Key, string Value);
}
