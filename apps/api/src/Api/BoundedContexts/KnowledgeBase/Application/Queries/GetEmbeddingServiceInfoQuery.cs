using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #4446: what the admin pages show about the embedding service. The endpoint used to build this
/// inline, with the dimension, the input limit and the batch size written as literals: every admin
/// page showed a dimension the service did not use.
/// </summary>
internal sealed record GetEmbeddingServiceInfoQuery : IQuery<EmbeddingServiceInfoDto>;

/// <summary>
/// <paramref name="Status"/> is the service's own status from <c>/health</c>, <c>unhealthy</c> when
/// <c>/health</c> answers with an error, <c>unavailable</c> when the service does not answer.
/// <paramref name="Model"/>, <paramref name="Device"/> and <paramref name="SupportedLanguages"/> come
/// from <c>/health</c>: null or empty when the service is not healthy.
/// <paramref name="Dimension"/> is the API's (<c>IEmbeddingService.GetEmbeddingDimensions()</c>), the
/// one the vector schema is checked against, so it is known even when the service is down.
/// <paramref name="MaxInputChars"/> is the longest text the service embeds, from its <c>/config</c>:
/// null when the service does not answer it.
/// </summary>
internal sealed record EmbeddingServiceInfoDto(
    string Status,
    string? Model,
    string? Device,
    IReadOnlyList<string> SupportedLanguages,
    int Dimension,
    int? MaxInputChars);
