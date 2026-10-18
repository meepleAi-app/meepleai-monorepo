namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il gateway non ha risposto a un giro del ciclo: tetto, quota o provider. Ferma il ciclo; il codice
/// è quello del gateway (per esempio <c>AI_PROVIDER_UNAVAILABLE</c> o il codice del tetto giornaliero).
/// </summary>
#pragma warning disable S3871 // Exceptions should be public — internal here is intentional (BC-private contract)
internal sealed class AssistantGatewayException(string errorCode)
    : Exception($"The LLM gateway refused or failed a round trip ({errorCode}).")
{
    public string ErrorCode { get; } = errorCode;
}
#pragma warning restore S3871
