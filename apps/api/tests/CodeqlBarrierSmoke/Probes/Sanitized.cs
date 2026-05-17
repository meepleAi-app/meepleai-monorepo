// =====================================================================
// SYNTHETIC barrier verification probe — POSITIVE side.
//
// Purpose:
//   Validate that the CodeQL MaD sanitizer pack
//   (.github/codeql/meepleai/sanitizers-extensions/) recognises canonical
//   sanitizer composition as a barrier for `cs/exposure-of-sensitive-information`.
//
// Expected outcome when the monthly `barrier-verification.yml` workflow runs:
//   ZERO alerts produced from this file. A non-zero count means the barrier
//   registration has silently regressed.
//
// DO NOT delete this file. DO NOT remove the sanitizer composition.
// If a refactor needs to touch this code, update
// `docs/for-developers/specs/2026-05-17-codeql-barrier-probes.md` first.
//
// References:
//   - ADR-058 §Acceptance bullet 5
//   - Issue #1196
// =====================================================================

using Api.Helpers;
using Api.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace Api.Tests.CodeqlBarrierSmoke.Probes;

public static class Sanitized
{
    public static void Probe(ILogger logger, string email)
    {
        // BOTH masking (file-content-store barrier — PII confidentiality)
        // AND log-forging stripping (log-injection barrier — CRLF safety).
        // CodeQL barrierModel registrations must recognise this composition
        // as safe for `cs/exposure-of-sensitive-information`.
        var safe = LogSanitizer.Sanitize(DataMasking.MaskEmail(email));
        logger.LogInformation("user email (masked): {Email}", safe);
    }
}
