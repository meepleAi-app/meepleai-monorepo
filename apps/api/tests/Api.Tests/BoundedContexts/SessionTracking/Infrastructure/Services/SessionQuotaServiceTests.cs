using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;
using UserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Issue #3873 — le due guardie di <see cref="SessionQuotaService"/> (righe 51 e 92) esentano dalla
/// quota con <c>userRole.IsAdmin() || userRole.IsEditor()</c>, e quei due predicati sono confronti
/// ESATTI. Il quinto ruolo, <c>superadmin</c>, non corrisponde a nessuno dei due: chi sta sopra
/// l'admin finisce soggetto alla quota del suo tier, cioe' con MENO privilegi dell'admin.
///
/// I due <c>[Theory]</c> qui sotto fissano la tabella completa dei cinque ruoli:
/// esenti = {superadmin, admin, editor} · soggetti a quota = {creator, user}.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionQuotaServiceTests
{
    /// <summary>Limite di default del tier free (<c>DefaultLimits.FreeMaxSessions</c>).</summary>
    private const int FreeTierLimit = 3;

    /// <summary>Sessioni attive simulate: sopra il limite free, cosi' un ruolo non esente viene negato.</summary>
    private const int ActiveSessionCount = 5;

    private readonly Mock<ISessionRepository> _sessionRepositoryMock = new();
    private readonly Mock<IConfigurationService> _configServiceMock = new();
    private readonly SessionQuotaService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public SessionQuotaServiceTests()
    {
        _service = new SessionQuotaService(
            _sessionRepositoryMock.Object,
            _configServiceMock.Object,
            NullLogger<SessionQuotaService>.Instance);

        _sessionRepositoryMock
            .Setup(r => r.GetActiveByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateActiveSessions(ActiveSessionCount));

        // Nessun override in configurazione: il servizio ricade sui limiti di default.
        _configServiceMock
            .Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null);
    }

    private IEnumerable<Session> CreateActiveSessions(int count) =>
        Enumerable
            .Range(0, count)
            .Select(_ => Session.Create(_userId, Guid.NewGuid(), SessionType.GameSpecific))
            .ToList();

    #region CheckQuotaAsync — tabella dei cinque ruoli

    [Theory]
    [InlineData("superadmin")]
    [InlineData("admin")]
    [InlineData("editor")]
    public async Task CheckQuotaAsync_ExemptRole_ReturnsUnlimitedWithoutCountingSessions(string roleValue)
    {
        // Arrange
        var userRole = AuthRole.Parse(roleValue);

        // Act
        var result = await _service.CheckQuotaAsync(_userId, UserTier.Free, userRole);

        // Assert
        result.IsAllowed.Should().BeTrue(
            "il ruolo {0} e' esente dalla quota di sessioni concorrenti", roleValue);
        result.DenialReason.Should().BeNull();
        result.MaxAllowed.Should().Be(int.MaxValue);

        // L'esenzione precede il conteggio: il repository non va nemmeno interrogato.
        _sessionRepositoryMock.Verify(
            r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("creator")]
    [InlineData("user")]
    public async Task CheckQuotaAsync_QuotaSubjectRole_AtLimit_ReturnsDenied(string roleValue)
    {
        // Arrange
        var userRole = AuthRole.Parse(roleValue);

        // Act
        var result = await _service.CheckQuotaAsync(_userId, UserTier.Free, userRole);

        // Assert
        result.IsAllowed.Should().BeFalse(
            "il ruolo {0} resta soggetto al limite del tier", roleValue);
        result.DenialReason.Should().NotBeNull();
        result.DenialReason.Should().Contain($"{FreeTierLimit} sessions");
        result.DenialReason.Should().Contain("free tier");
        result.CurrentCount.Should().Be(ActiveSessionCount);
        result.MaxAllowed.Should().Be(FreeTierLimit);
    }

    #endregion

    #region GetQuotaInfoAsync — tabella dei cinque ruoli

    [Theory]
    [InlineData("superadmin")]
    [InlineData("admin")]
    [InlineData("editor")]
    public async Task GetQuotaInfoAsync_ExemptRole_ReturnsUnlimitedWithActualCount(string roleValue)
    {
        // Arrange
        var userRole = AuthRole.Parse(roleValue);

        // Act
        var info = await _service.GetQuotaInfoAsync(_userId, UserTier.Free, userRole);

        // Assert
        info.IsUnlimited.Should().BeTrue(
            "il ruolo {0} ha quota illimitata", roleValue);
        info.MaxSessions.Should().Be(int.MaxValue);
        info.RemainingSlots.Should().Be(int.MaxValue);

        // Il conteggio reale resta esposto anche per gli esenti (analytics).
        info.ActiveSessions.Should().Be(ActiveSessionCount);
    }

    [Theory]
    [InlineData("creator")]
    [InlineData("user")]
    public async Task GetQuotaInfoAsync_QuotaSubjectRole_ReturnsTierLimit(string roleValue)
    {
        // Arrange
        var userRole = AuthRole.Parse(roleValue);

        // Act
        var info = await _service.GetQuotaInfoAsync(_userId, UserTier.Free, userRole);

        // Assert
        info.IsUnlimited.Should().BeFalse(
            "il ruolo {0} resta soggetto al limite del tier", roleValue);
        info.MaxSessions.Should().Be(FreeTierLimit);
        info.ActiveSessions.Should().Be(ActiveSessionCount);
        info.RemainingSlots.Should().Be(0, "Math.Max(0, 3 - 5) = 0");
    }

    #endregion
}
