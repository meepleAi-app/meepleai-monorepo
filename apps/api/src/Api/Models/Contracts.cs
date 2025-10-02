namespace Api.Models;

public record QaRequest(string tenantId, string gameId, string query);
public record QaResponse(string answer, IReadOnlyList<Snippet> snippets);
public record Snippet(string text, string source, int page, int line);

public record IngestPdfResponse(string jobId);
public record SeedRequest(string tenantId, string gameId);

// AI-02: RAG Explain models
public record ExplainRequest(string tenantId, string gameId, string topic);
public record ExplainResponse(
    ExplainOutline outline,
    string script,
    IReadOnlyList<Snippet> citations,
    int estimatedReadingTimeMinutes
);
public record ExplainOutline(
    string mainTopic,
    IReadOnlyList<string> sections
);
