using Api.Models;

namespace Api.Services;

public class RagService
{
    // TODO: integra Qdrant + OpenRouter; per ora ritorna snippet demo
    public Task<QaResponse> AskAsync(string tenantId, string gameId, string query)
    {
        var answer = query.ToLower().Contains("how many players") ? "Two players." : "Not Specified";
        var snippets = new List<Snippet>{
            new("Two players.","RuleSpec:Basics",1,1)
        };
        return Task.FromResult(new QaResponse(answer, snippets));
    }
}
