namespace Api.Models;

public record QaRequest(string tenantId, string gameId, string query);
public record QaResponse(string answer, IReadOnlyList<Snippet> snippets);
public record Snippet(string text, string source, int page, int line);

public record IngestPdfResponse(string jobId);
public record SeedRequest(string tenantId, string gameId);
