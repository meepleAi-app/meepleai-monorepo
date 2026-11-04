using Microsoft.Extensions.AI;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// ADR-097: la catena di un turno dell'assistente — invocazione degli strumenti, telemetria, gateway.
/// Una catena per turno, perché porta modello, versione del profilo e contatore del turno.
/// </summary>
internal interface IAssistantChatClientFactory
{
    IChatClient Create(AssistantModelSettings settings, ChatTurnMeter meter);
}
