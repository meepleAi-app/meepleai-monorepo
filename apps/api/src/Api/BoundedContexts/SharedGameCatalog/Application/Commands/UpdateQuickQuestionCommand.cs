using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update an existing quick question.
/// </summary>
/// <param name="QuestionId">The ID of the question to update.</param>
/// <param name="Text">The updated question text.</param>
/// <param name="Emoji">The updated emoji.</param>
/// <param name="Category">The updated category.</param>
/// <param name="DisplayOrder">The updated display order.</param>
internal record UpdateQuickQuestionCommand(
    Guid QuestionId,
    string Text,
    string Emoji,
    QuestionCategory Category,
    int DisplayOrder
) : ICommand<QuickQuestionDto>;