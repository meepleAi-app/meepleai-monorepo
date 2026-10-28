using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Batch command (#4503): reject the mechanic analyses left in <c>Draft</c> past the configured age, so a pipeline that
/// died with the process stops holding its (game, PDF, prompt) slot. Invoked by
/// <c>MechanicAnalysisStaleDraftExpirationJob</c> (not an HTTP endpoint).
/// </summary>
internal sealed record ExpireStaleMechanicAnalysisDraftsCommand : ICommand<ExpireStaleDraftsResult>;

/// <summary>
/// Outcome of one expiry run. <paramref name="Found"/> is how many stale drafts the query returned (at most one batch);
/// <paramref name="Expired"/> how many of them were actually rejected. The difference is drafts that finished in the
/// meantime or lost a write race: not failures.
/// </summary>
internal sealed record ExpireStaleDraftsResult(int Found, int Expired);
