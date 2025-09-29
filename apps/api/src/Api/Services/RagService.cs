using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class RagService
{
    private readonly MeepleAiDbContext _dbContext;

    public RagService(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // TODO: integra Qdrant + OpenRouter; per ora ritorna snippet demo
    public async Task<QaResponse> AskAsync(string tenantId, string gameId, string query, CancellationToken cancellationToken = default)
    {
        var ruleSpec = await _dbContext.RuleSpecs
            .AsNoTracking()
            .Include(r => r.Atoms)
            .Where(r => r.TenantId == tenantId && r.GameId == gameId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (ruleSpec is null || ruleSpec.Atoms.Count == 0)
        {
            return new QaResponse("Not Specified", Array.Empty<Snippet>());
        }

        var atoms = ruleSpec.Atoms
            .OrderBy(a => a.SortOrder)
            .ToList();

        var match = atoms.FirstOrDefault(a =>
            !string.IsNullOrWhiteSpace(query) &&
            a.Text.Contains(query, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            match = atoms.First();
        }

        var snippet = new Snippet(
            match.Text,
            $"RuleSpec:{ruleSpec.Version}:{match.Section ?? "General"}",
            match.PageNumber ?? 0,
            match.LineNumber ?? 0);

        return new QaResponse(match.Text, new[] { snippet });
    }
}
