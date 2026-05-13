using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to reset all transactional data owned by the smoke-aaron test persona.
/// Used by the Bruno smoke collection's pre-request script so that each smoke
/// run starts from a deterministic empty state — enabling the tolerant→strict
/// assertion migration tracked in #943.
///
/// Security model (triple-gate, enforced in the handler):
///   1. <c>TestEndpoints:Enabled == true</c> (config flag, default false)
///   2. <c>IWebHostEnvironment.IsProduction() == false</c>
///   3. Target user is hardcoded to <c>smoke-aaron</c> UUID — no parameter accepted
///
/// All 3 must be true. Any one missing → 403.
///
/// Issue #943 (EPIC #906 follow-up).
/// </summary>
internal record ResetSmokeAaronCommand : IRequest<ResetSmokeAaronResult>;

/// <summary>
/// Counts returned by <see cref="ResetSmokeAaronCommand"/> for audit + idempotency observability.
/// All values are post-delete; a value of 0 means nothing was found to delete.
/// </summary>
public record ResetSmokeAaronResult(
    int PrivateGames,
    int Agents,
    int ChatThreads,
    int PdfDocuments,
    int KbReindexJobs);
