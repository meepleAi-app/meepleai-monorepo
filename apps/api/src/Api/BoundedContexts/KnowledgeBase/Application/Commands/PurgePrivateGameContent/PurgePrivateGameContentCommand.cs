using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.PurgePrivateGameContent;

/// <summary>
/// #4244 (consegna C): toglie per sempre il contenuto di un gioco privato — le chat, i PDF, i
/// passaggi, il documento vettoriale, gli embedding.
/// </summary>
/// <remarks>
/// Lo invia la cancellazione del gioco (<c>DeletePrivateGameCommandHandler</c>, in UserLibrary),
/// che ha già verificato che chi cancella sia il proprietario. Questo comando NON controlla chi
/// lo chiede: non va esposto da una rotta.
/// </remarks>
internal sealed record PurgePrivateGameContentCommand(Guid PrivateGameId) : ICommand;
