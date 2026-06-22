using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Round-trips <see cref="TranslationSource"/> against the kebab-case string
/// representation persisted in <c>shared_game_translations.source</c>.
/// Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// Lives in the Application layer (not Infrastructure) per plan review
/// finding I4 — the validator and the resolver both need to translate the
/// enum without taking a hard dependency on a persistence-layer type.
/// </remarks>
internal static class TranslationSourceMapper
{
    public const string ManualString = "manual";
    public const string AutoOpenRouterString = "auto-openrouter";
    public const string CommunityString = "community";

    public static string ToPersistedString(TranslationSource source) => source switch
    {
        TranslationSource.Manual => ManualString,
        TranslationSource.AutoOpenRouter => AutoOpenRouterString,
        TranslationSource.Community => CommunityString,
        _ => throw new ArgumentOutOfRangeException(
            nameof(source), source, $"Unknown TranslationSource: {source}")
    };

    public static TranslationSource FromPersistedString(string source)
    {
        if (TryFromPersistedString(source, out var result))
        {
            return result;
        }

        throw new ArgumentOutOfRangeException(
            nameof(source), source, $"Unknown source: {source}");
    }

    public static bool TryFromPersistedString(string source, out TranslationSource result)
    {
        switch (source)
        {
            case ManualString:
                result = TranslationSource.Manual;
                return true;
            case AutoOpenRouterString:
                result = TranslationSource.AutoOpenRouter;
                return true;
            case CommunityString:
                result = TranslationSource.Community;
                return true;
            default:
                result = default;
                return false;
        }
    }
}
