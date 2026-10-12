using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Services;
using Microsoft.Extensions.AI;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// ADR-097: <c>FunctionInvokingChatClient → OpenTelemetryChatClient → GatewayChatClient</c>. Il primo
/// <c>Use</c> è il più esterno: l'invocazione degli strumenti richiama la telemetria e il gateway a
/// ogni giro. Nessuna cache di risposte (spec §4.1). Il <c>FunctionInvoker</c> conta le chiamate e
/// traduce gli argomenti non validi in un errore tipizzato per il modello.
/// </summary>
internal sealed class AssistantChatClientFactory : IAssistantChatClientFactory
{
    public const string TelemetrySourceName = "MeepleAI.SessionAssistant";

    private readonly ILlmService _gateway;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _services;

    public AssistantChatClientFactory(ILlmService gateway, ILoggerFactory loggerFactory, IServiceProvider services)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IChatClient Create(AssistantModelSettings settings, ChatTurnMeter meter) =>
        new ChatClientBuilder(new GatewayChatClient(_gateway, settings, meter))
            .UseFunctionInvocation(_loggerFactory, client =>
            {
                client.MaximumIterationsPerRequest = settings.MaxToolIterations;
                client.IncludeDetailedErrors = false;
                client.FunctionInvoker = (context, cancellationToken) => InvokeToolAsync(context, meter, cancellationToken);
            })
            .UseOpenTelemetry(_loggerFactory, TelemetrySourceName)
            .Build(_services);

    /// <summary>
    /// Il binding di M.E.AI lancia <see cref="ArgumentException"/> quando manca un parametro obbligatorio
    /// o un valore non si converte, prima che lo strumento parta. Il modello riceve un errore che può
    /// correggere, e il turno lo conta.
    /// </summary>
    private static async ValueTask<object?> InvokeToolAsync(
        FunctionInvocationContext context, ChatTurnMeter meter, CancellationToken cancellationToken)
    {
        meter.RecordToolCall(context.Function.Name);
        try
        {
            return await context.Function.InvokeAsync(context.Arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or FormatException or InvalidCastException)
        {
            meter.RecordInvalidArguments();
            return new AssistantToolError(
                "argomenti_non_validi",
                "Gli argomenti non sono validi: controlla nomi e tipi dei parametri dello strumento e riprova.");
        }
    }
}
