namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Il regolamento finto dello spike: i passaggi dello scenario, senza citazioni (l'esecutore non giudica il testo).</summary>
internal sealed class ScenarioRuleSource(AssistantScenario scenario) : IAssistantRuleSource
{
    public Task<AssistantRuleSearch> SearchAsync(string question, CancellationToken cancellationToken) =>
        Task.FromResult(new AssistantRuleSearch(
            scenario.Passages.Select(p => new AssistantRulePassage(p.Text, p.Page)).ToList(), []));
}
