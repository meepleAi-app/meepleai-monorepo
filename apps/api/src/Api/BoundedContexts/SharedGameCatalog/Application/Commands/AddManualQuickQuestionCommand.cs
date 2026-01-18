using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to manually add a quick question to a game.
/// </summary>
/// <param name="SharedGameId">The ID of the game.</param>
/// <param name="Text">The question text (max 200 characters).</param>
/// <param name="Emoji">The emoji icon (1-2 characters).</param>
/// <param name="Category">The question category.</param>
/// <param name="DisplayOrder">The display order (0-based).</param>
internal record AddManualQuickQuestionCommand(
    Guid SharedGameId,
    string Text,
    string Emoji,
    QuestionCategory Category,
    int DisplayOrder
) : ICommand<Guid>;