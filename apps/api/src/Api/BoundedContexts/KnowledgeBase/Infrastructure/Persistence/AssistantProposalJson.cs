using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Forma jsonb di <c>payload</c>, <c>expected</c> e <c>confirmed_payload</c> in
/// <c>knowledge_base.assistant_proposals</c> (spec 2026-10-11 §5.2). Nomi in camelCase, espliciti: è un
/// contratto persistito.
/// </summary>
internal static class AssistantProposalJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Serialize(AssistantProposalPayload payload) => JsonSerializer.Serialize(payload, Options);

    public static string Serialize(AssistantProposalExpectation expected) => JsonSerializer.Serialize(expected, Options);

    public static string Serialize(AssistantProposalConfirmation confirmed) => JsonSerializer.Serialize(confirmed, Options);

    public static AssistantProposalPayload DeserializePayload(string json) =>
        JsonSerializer.Deserialize<AssistantProposalPayload>(json, Options)
        ?? throw new JsonException("Assistant proposal payload is null.");

    public static AssistantProposalExpectation DeserializeExpectation(string json) =>
        JsonSerializer.Deserialize<AssistantProposalExpectation>(json, Options)
        ?? throw new JsonException("Assistant proposal expectation is null.");

    public static AssistantProposalConfirmation DeserializeConfirmation(string json) =>
        JsonSerializer.Deserialize<AssistantProposalConfirmation>(json, Options)
        ?? throw new JsonException("Assistant proposal confirmation is null.");
}
