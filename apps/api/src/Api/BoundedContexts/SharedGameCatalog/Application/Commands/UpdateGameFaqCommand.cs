using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update an existing FAQ for a shared game.
/// Updates the question, answer, and display order.
/// </summary>
internal record UpdateGameFaqCommand(
    Guid FaqId,
    string Question,
    string Answer,
    int Order
) : ICommand<Unit>;
