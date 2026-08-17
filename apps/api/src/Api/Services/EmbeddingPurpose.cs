namespace Api.Services;

/// <summary>
/// Which side of a retrieval pair a text belongs to, for models trained with an
/// asymmetric instruction prefix (issue #3737).
/// </summary>
/// <remarks>
/// <para>
/// The <c>intfloat/multilingual-e5-*</c> family is trained so that the question and the
/// indexed text are encoded with <b>different</b> prefixes — <c>"query: "</c> and
/// <c>"passage: "</c>. One embedding endpoint serves both sides here, so the caller has to
/// declare which one it is: nothing downstream can infer it from the text.
/// </para>
/// <para>
/// Before #3737 the embedding service prepended <c>"passage: "</c> unconditionally. That was
/// right for the indexing path and wrong for every search query, and the cost was measured on
/// the real corpus (56.367 chunk, 127 manuali): the best chunk of the manual named by the
/// canonical query <c>catan-setup</c> sat at cosine rank <b>10</b> instead of <b>1</b>.
/// </para>
/// <para>
/// <see cref="Passage"/> is the default everywhere it is not passed, because it is what the
/// service already did — so the chunks already indexed stay valid and no re-embedding of the
/// corpus is required by this change.
/// </para>
/// </remarks>
internal enum EmbeddingPurpose
{
    /// <summary>
    /// Indexed text (a document chunk). Encoded with the <c>"passage: "</c> prefix.
    /// The pre-#3737 behaviour, and the default.
    /// </summary>
    Passage = 0,

    /// <summary>
    /// A retrieval question. Encoded with the <c>"query: "</c> prefix.
    /// </summary>
    Query = 1
}

/// <summary>
/// Wire-format helpers for <see cref="EmbeddingPurpose"/>.
/// </summary>
internal static class EmbeddingPurposeExtensions
{
    /// <summary>
    /// The token the embedding service expects in the request body. Kept explicit rather than
    /// relying on <c>ToString().ToLowerInvariant()</c> so a rename of the enum member cannot
    /// silently change the wire contract.
    /// </summary>
    public static string ToWireValue(this EmbeddingPurpose purpose) => purpose switch
    {
        EmbeddingPurpose.Query => "query",
        EmbeddingPurpose.Passage => "passage",
        _ => "passage"
    };
}
