using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update an existing FAQ for a shared game.
/// Updates the question, answer, and display order.
/// </summary>
/// <remarks>#4557: <c>SharedGameId</c> è il gioco del segmento <c>{id}</c> della rotta; una FAQ di un
/// altro gioco risponde 404.</remarks>
internal record UpdateGameFaqCommand(
    Guid SharedGameId,
    Guid FaqId,
    string Question,
    string Answer,
    int Order
) : ICommand<Unit>;
