using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class SuggestPhaseTemplatesCommandHandler
    : IQueryHandler<SuggestPhaseTemplatesCommand, IReadOnlyList<PhaseTemplateSuggestionDto>>
{
    private readonly IPhaseRulesSearchService _search;
    private readonly ILlmService _llm;

    public SuggestPhaseTemplatesCommandHandler(
        IPhaseRulesSearchService search,
        ILlmService llm)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    private const string SystemPrompt = """
        You are a board game rules expert. Given excerpts from a game rulebook, suggest a list of game phases (rounds/turns).
        Return ONLY a JSON array with objects having fields: phaseName (string), phaseOrder (int, 1-based), description (string), rationale (string).
        Example: [{"phaseName":"Setup","phaseOrder":1,"description":"Prepare the board","rationale":"Rules state players must prepare before starting"}]
        """;

    public async Task<IReadOnlyList<PhaseTemplateSuggestionDto>> Handle(
        SuggestPhaseTemplatesCommand query, CancellationToken cancellationToken)
    {
        var chunks = await _search.SearchRulesChunksAsync(
            query.GameId, "phase turn round step action", topK: 10, cancellationToken).ConfigureAwait(false);

        if (chunks.Count == 0)
            return Array.Empty<PhaseTemplateSuggestionDto>();

        var rulebookExcerpts = string.Join("\n\n---\n\n", chunks);
        var userPrompt = $"Rulebook excerpts:\n\n{rulebookExcerpts}";

        var suggestions = await _llm.GenerateJsonAsync<List<PhaseTemplateSuggestionDto>>(
            SystemPrompt, userPrompt, RequestSource.Manual, cancellationToken).ConfigureAwait(false);

        return suggestions?.AsReadOnly() ?? (IReadOnlyList<PhaseTemplateSuggestionDto>)Array.Empty<PhaseTemplateSuggestionDto>();
    }
}
