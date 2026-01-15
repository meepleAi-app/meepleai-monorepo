using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to delete a quick question.
/// </summary>
/// <param name="QuestionId">The ID of the question to delete.</param>
internal record DeleteQuickQuestionCommand(Guid QuestionId) : ICommand<Unit>;