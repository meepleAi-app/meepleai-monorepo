using Api.Models;

namespace Api.Services;

public class RuleSpecService
{
    // TODO: integra parser PDF (Tabula/Camelot via sidecar) e normalizzazione in RuleSpec
    public Task<RuleSpec> GetOrCreateDemoAsync(string gameId)
    {
        var spec = new RuleSpec(
            gameId,
            "v0-demo",
            DateTime.UtcNow,
            new List<RuleAtom>{
                new("r1","Two players.", "Basics","1","1"),
                new("r2","White moves first.", "Basics","1","2")
            }
        );
        return Task.FromResult(spec);
    }
}
