using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;

/// <summary>
/// Handles GetUserGameKbStatusQuery.
/// Returns the per-game KB indexing status, coverage score, and suggested questions for end users.
/// KB-03: User-facing KB status endpoint.
/// </summary>
internal sealed class GetUserGameKbStatusQueryHandler
    : IQueryHandler<GetUserGameKbStatusQuery, UserGameKbStatusDto>
{
    private readonly IVectorDocumentRepository _vectorRepo;
    private readonly IConfigurationRepository _configRepo;

    public GetUserGameKbStatusQueryHandler(
        IVectorDocumentRepository vectorRepo,
        IConfigurationRepository configRepo)
    {
        _vectorRepo = vectorRepo ?? throw new ArgumentNullException(nameof(vectorRepo));
        _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
    }

    public async Task<UserGameKbStatusDto> Handle(
        GetUserGameKbStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var documents = await _vectorRepo
            .GetByGameIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        var isIndexed = documents.Count > 0;
        var documentCount = documents.Count;

        int coverageScore = 0;
        string coverageLevel = "None";
        List<string> suggestedQuestions = [];

        if (isIndexed)
        {
            // Read coverage score
            string coverageKey = $"KB:Coverage:{query.GameId}";
            var coverageConfig = await _configRepo
                .GetByKeyAsync(coverageKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (coverageConfig is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(coverageConfig.Value);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("score", out var scoreEl))
                        coverageScore = scoreEl.GetInt32();
                    if (root.TryGetProperty("level", out var levelEl))
                        coverageLevel = levelEl.GetString() ?? "None";
                }
                catch
                {
                    // Swallow JSON parsing errors — defaults already set
                }
            }

            // Read suggested questions
            string questionsKey = $"KB:SuggestedQuestions:{query.GameId}";
            var questionsConfig = await _configRepo
                .GetByKeyAsync(questionsKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (questionsConfig is not null)
            {
                try
                {
                    var questions = JsonSerializer.Deserialize<List<string>>(questionsConfig.Value);
                    if (questions is not null)
                        suggestedQuestions = questions;
                }
                catch
                {
                    // Swallow JSON parsing errors — defaults already set
                }
            }
        }

        return new UserGameKbStatusDto(
            GameId: query.GameId,
            IsIndexed: isIndexed,
            DocumentCount: documentCount,
            CoverageScore: coverageScore,
            CoverageLevel: coverageLevel,
            SuggestedQuestions: suggestedQuestions);
    }
}
