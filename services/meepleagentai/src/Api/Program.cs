using Api.Models;
using Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RuleSpecService>();
builder.Services.AddSingleton<RagService>();

var app = builder.Build();

app.MapGet("/", () => Results.Json(new { ok = true, name = "MeepleAgentAI" }));

app.MapPost("/agents/qa", async (QaRequest req, RagService rag) =>
{
    if (string.IsNullOrWhiteSpace(req.tenantId) || string.IsNullOrWhiteSpace(req.gameId))
        return Results.BadRequest(new { error = "tenantId and gameId are required" });

    var resp = await rag.AskAsync(req.tenantId, req.gameId, req.query);
    return Results.Json(resp);
});

app.MapPost("/ingest/pdf", () => Results.Json(new IngestPdfResponse(Guid.NewGuid().ToString("N"))));

app.MapPost("/admin/seed", async (RuleSpecService rules) =>
{
    var spec = await rules.GetOrCreateDemoAsync("demo-chess");
    return Results.Json(new { ok = true, spec });
});

app.Run();
