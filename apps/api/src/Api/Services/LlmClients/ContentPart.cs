namespace Api.Services.LlmClients;

/// <summary>
/// Base type for multimodal content parts in LLM messages.
/// Supports text and image content for vision-capable models.
/// </summary>
#pragma warning disable S2094 // Abstract base record for sealed hierarchy (TextContentPart, ImageContentPart)
internal abstract record ContentPart;
#pragma warning restore S2094

/// <summary>
/// Text content part for LLM messages.
/// </summary>
internal record TextContentPart(string Text) : ContentPart;

/// <summary>
/// Image content part with base64-encoded data for vision-capable LLM models.
/// </summary>
internal record ImageContentPart(string Base64Data, string MediaType) : ContentPart
{
    public string ToDataUri() => $"data:{MediaType};base64,{Base64Data}";
}

/// <summary>
/// Multimodal LLM message supporting mixed text and image content.
/// </summary>
internal record LlmMessage(string Role, IReadOnlyList<ContentPart> Content)
{
    /// <summary>
    /// Create a text-only message (convenience factory).
    /// </summary>
    public static LlmMessage FromText(string role, string text) =>
        new(role, [new TextContentPart(text)]);

    /// <summary>
    /// Whether this message contains any image content parts.
    /// </summary>
    public bool HasImages => Content.Any(c => c is ImageContentPart);
}
