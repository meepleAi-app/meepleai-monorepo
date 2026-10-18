using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - the streaming command and its handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Spec 2026-10-11 §6.2: una domanda dell'host all'assistente in partita. È in streaming perché il
/// client vede cosa sta facendo l'assistente mentre il ciclo gira; la risposta resta bufferizzata (§6.1).
/// <c>UserRole</c> decide l'accesso alla KB del gioco in <c>cerca_regole</c> (#4411), come in StreamQa.
/// </summary>
internal sealed record AskSessionAssistantCommand(Guid LiveSessionId, Guid UserId, string? UserRole, string Question, string? Language)
    : IStreamingQuery<AssistantTurnEvent>;

internal sealed class AskSessionAssistantCommandHandler(AssistantTurnRunner runner)
    : IStreamingQueryHandler<AskSessionAssistantCommand, AssistantTurnEvent>
{
    public IAsyncEnumerable<AssistantTurnEvent> Handle(AskSessionAssistantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return StreamAsync(command, cancellationToken);
    }

    private async IAsyncEnumerable<AssistantTurnEvent> StreamAsync(
        AskSessionAssistantCommand command, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // I controlli vengono prima del primo evento: se falliscono, chi consuma lo stream riceve
        // l'eccezione al primo MoveNextAsync, e l'endpoint risponde con uno stato HTTP invece che con SSE.
        var turn = await runner.PrepareAsync(command, cancellationToken).ConfigureAwait(false);
        yield return AssistantTurnEvent.Status();

        // Il turno scrive i suoi eventi in un canale mentre gira: così uno «status» arriva al client
        // durante il ciclo, e non serve un yield dentro un try/catch (che C# non permette).
        var channel = Channel.CreateUnbounded<AssistantTurnEvent>(new UnboundedChannelOptions { SingleReader = true });
        var running = runner.RunAsync(turn, channel.Writer, cancellationToken);
        try
        {
            await foreach (var turnEvent in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return turnEvent;
            }
        }
        finally
        {
            // RunAsync non lancia se non per annullamento: qui si aspetta solo che abbia finito.
            await running.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
