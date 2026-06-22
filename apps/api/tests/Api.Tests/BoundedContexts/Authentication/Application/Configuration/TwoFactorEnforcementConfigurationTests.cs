using Api.BoundedContexts.Authentication.Application.Configuration;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Configuration;

/// <summary>
/// Unit tests for the SP5 S3 T3 typed config-flag wrapper. Verifies fail-closed semantics
/// and the contract that <c>StrictModeDefault</c> is FALSE (shadow mode) at deploy time —
/// the cutover from shadow to strict is an explicit ops decision via admin toggle, not a
/// code change. See <c>audits/2026-05-26-s3-three-amigos-kickoff.md</c> §D-S3-1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TwoFactorEnforcementConfigurationTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly TwoFactorEnforcementConfiguration _sut;

    public TwoFactorEnforcementConfigurationTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _sut = new TwoFactorEnforcementConfiguration(_mockConfigService.Object);
    }

    [Fact]
    public void StrictModeDefault_IsFalse_AtDeployTime()
    {
        // Cutover contract: strict mode is OFF by default. Flipping it on is an explicit ops
        // decision (admin toggle) — separates code merge from behavior change and prevents the
        // mass-lockout scenario at deploy time when all existing sessions have LastTotpVerifiedAt
        // = null. See D-S3-1.
        TwoFactorConfigurationKeys.StrictModeDefault.Should().BeFalse(
            "default must be shadow mode; ops flips strict via admin toggle post-sweep");
    }

    [Fact]
    public void StrictModeKey_FollowsColonNamespaceConvention()
    {
        // Pattern consistent with Registration:PublicEnabled etc — the dynamic config store uses
        // ":" as the namespace separator.
        TwoFactorConfigurationKeys.StrictMode.Should().Be("TwoFactor:StrictMode");
    }

    [Fact]
    public async Task GetStrictModeAsync_WhenConfigStoreReturnsTrue_ReturnsTrue()
    {
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(TwoFactorConfigurationKeys.StrictMode, It.IsAny<bool?>(), It.IsAny<string?>()))
            .ReturnsAsync(true);

        var result = await _sut.GetStrictModeAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetStrictModeAsync_WhenConfigStoreReturnsNull_ReturnsDefaultFalse()
    {
        // Key absent from the DB — store returns null; provider applies the StrictModeDefault.
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(TwoFactorConfigurationKeys.StrictMode, It.IsAny<bool?>(), It.IsAny<string?>()))
            .ReturnsAsync((bool?)null);

        var result = await _sut.GetStrictModeAsync();

        result.Should().BeFalse("default at deploy is shadow mode");
    }

    [Fact]
    public async Task GetStrictModeAsync_WhenConfigStoreThrows_FailsClosedToShadow()
    {
        // Fail-closed: an unreachable config store must NOT silently flip behavior to strict.
        // Strict mode is opt-in by ops via admin toggle; any read error => shadow.
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("config store unreachable"));

        var result = await _sut.GetStrictModeAsync();

        result.Should().BeFalse("fail-closed: any error => shadow mode (the safe default)");
    }
}
