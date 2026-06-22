// =====================================================================
// SYNTHETIC barrier verification probe — NEGATIVE side.
//
// Purpose:
//   Validate that the CodeQL rule `cs/exposure-of-sensitive-information`
//   is still alive and not globally disabled by drift in
//   `.github/codeql/codeql-config.yml` query-filters or paths-ignore.
//
// Expected outcome when the monthly `barrier-verification.yml` workflow runs:
//   AT LEAST ONE alert produced from this file. A zero count means the rule
//   has silently been disabled / suppressed / excluded.
//
// DO NOT "fix" this code. The lack of sanitizer is INTENTIONAL.
// DO NOT delete this file — the workflow's regression-guard step explicitly
// asserts its presence and fails loudly if missing.
//
// References:
//   - ADR-058 §Acceptance bullet 5
//   - Issue #1196
// =====================================================================

using Microsoft.Extensions.Logging;

namespace Api.Tests.CodeqlBarrierSmoke.Probes;

public static class Unsanitized
{
    public static void Probe(ILogger logger, string email)
    {
        // INTENTIONAL violation — raw user-controlled email flows directly
        // into a structured logger template without any sanitizer applied.
        // CodeQL MUST flag this as `cs/exposure-of-sensitive-information`.
        logger.LogInformation("user email (raw): {Email}", email);
    }
}
