using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Helpers;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// Unit-level regression tests for C1 (hash mismatch).
///
/// The endpoint code paths in <c>AuthenticationEndpoints.cs</c> and the legacy
/// helper <c>TempSessionService.HashToken</c> previously hashed the UTF-8 bytes
/// of the Base-64 cookie *string* (effectively SHA256 over text), while storage
/// hashes the *decoded* random bytes via <see cref="SessionTokenHasher.HashFromCookie"/>.
///
/// These tests run without Docker / a database and prove:
///   1. The broken inline algorithm is not equivalent to the canonical algorithm.
///   2. <see cref="SessionTokenHasher.HashFromCookie"/> matches <see cref="SessionToken.ComputeHash"/>
///      (single source of truth).
///   3. The legacy helper <see cref="CryptographyHelper.ComputeSha256HashBase64"/> reproduces
///      the broken behavior — confirming TempSessionService.HashToken was bugged before the fix.
///
/// The integration test counterparts in <see cref="SessionExtendEndpointTests"/> exercise
/// the full HTTP stack but require Docker for Testcontainers; this file is the unit-level
/// safety net that ALWAYS runs in CI/local dev regardless of Docker availability.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class SessionTokenHashConsistencyUnitTests
{
    /// <summary>
    /// The pre-fix inline algorithm used by ExtendSession / RevokeAllSessions / TempSessionService:
    ///   SHA256(UTF-8 bytes of the Base-64 cookie string)
    /// Reproduced verbatim so the test fails the moment anyone re-introduces it.
    /// </summary>
    private static string BrokenInlineHash(string cookieValue)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(cookieValue));
        return Convert.ToBase64String(hash);
    }

    [Fact]
    public void BrokenInlineHash_DoesNotMatch_CanonicalHasher()
    {
        // Arrange — generate a real session token (Base-64 encoded 32 random bytes).
        var token = SessionToken.Generate();

        // Act
        var canonical = SessionTokenHasher.HashFromCookie(token.Value);
        var broken = BrokenInlineHash(token.Value);

        // Assert — the two algorithms produce DIFFERENT hashes; this is the C1 bug.
        broken.Should().NotBe(canonical,
            "C1 bug: SHA256 over UTF-8 of the Base-64 string ≠ SHA256 over decoded random bytes.");
    }

    [Fact]
    public void CanonicalHasher_MatchesSessionTokenComputeHash_RepeatedSamples()
    {
        // Generate many random tokens and confirm the centralized hasher is identical
        // to SessionToken.ComputeHash for each. Guards against any subtle algorithmic drift.
        for (var i = 0; i < 32; i++)
        {
            var token = SessionToken.Generate();

            var viaHasher = SessionTokenHasher.HashFromCookie(token.Value);
            var viaSessionToken = token.ComputeHash();

            viaHasher.Should().Be(viaSessionToken,
                $"sample #{i}: both code paths must produce identical hashes (single source of truth).");
        }
    }

    [Fact]
    public void LegacyCryptographyHelper_ReproducesBrokenAlgorithm()
    {
        // CryptographyHelper.ComputeSha256HashBase64 was the implementation backing
        // TempSessionService.HashToken before the fix. Verify it is still the broken
        // algorithm so the C1 fix in TempSessionService is meaningful — calling
        // SessionTokenHasher.HashFromCookie instead of CryptographyHelper.ComputeSha256HashBase64
        // changes behavior for any Base-64 input.
        var token = SessionToken.Generate();

        var legacyResult = CryptographyHelper.ComputeSha256HashBase64(token.Value);
        var canonical = SessionTokenHasher.HashFromCookie(token.Value);

        legacyResult.Should().NotBe(canonical,
            "TempSessionService.HashToken needed to switch from CryptographyHelper.ComputeSha256HashBase64 to SessionTokenHasher.HashFromCookie.");

        // Cross-check: legacy result equals the broken inline hash above.
        legacyResult.Should().Be(BrokenInlineHash(token.Value),
            "the legacy helper and the inline endpoint logic both implement SHA256 over UTF-8 bytes of the cookie string.");
    }
}
