using Api.Infrastructure;
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    db.Database.Migrate();
}

app.MapGet("/", () => Results.Json(new { ok = true, name = "MeepleAgentAI" }));

app.MapPost("/agents/qa", async (QaRequest req, RagService rag, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.tenantId) || string.IsNullOrWhiteSpace(req.gameId))
        return Results.BadRequest(new { error = "tenantId and gameId are required" });

    var resp = await rag.AskAsync(req.tenantId, req.gameId, req.query, ct);
    return Results.Json(resp);
});

app.MapPost("/ingest/pdf", () => Results.Json(new IngestPdfResponse(Guid.NewGuid().ToString("N"))));

app.MapPost("/admin/seed", async (SeedRequest request, RuleSpecService rules, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.tenantId) || string.IsNullOrWhiteSpace(request.gameId))
    {
        return Results.BadRequest(new { error = "tenantId and gameId are required" });
    }

    var spec = await rules.GetOrCreateDemoAsync(request.tenantId, request.gameId, ct);
    return Results.Json(new { ok = true, spec });
});

app.Run();
