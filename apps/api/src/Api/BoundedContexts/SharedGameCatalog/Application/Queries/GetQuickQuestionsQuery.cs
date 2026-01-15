using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get quick questions for a game.
/// </summary>
/// <param name="SharedGameId">The ID of the game.</param>
/// <param name="ActiveOnly">Whether to return only active questions. Default is true.</param>
internal record GetQuickQuestionsQuery(Guid SharedGameId, bool ActiveOnly = true)
    : IQuery<IReadOnlyCollection<QuickQuestionDto>>;