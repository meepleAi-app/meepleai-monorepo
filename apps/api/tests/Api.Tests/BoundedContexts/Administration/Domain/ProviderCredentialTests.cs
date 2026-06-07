using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials.Events;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class ProviderCredentialTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(FixedNow);

    [Fact]
    public void Create_FirstRotation_ProducesActiveCredential_NoPreviousId()
    {
        var providerName = ProviderName.Create("deepseek");
        var fingerprint = KeyFingerprint.FromPlaintext("sk-deepseek-abcd1234");
        var actorId = Guid.NewGuid();

        var credential = ProviderCredential.Create(
            provider: providerName,
            encryptedKey: "ciphertext-abc",
            fingerprint: fingerprint,
            rotatedByUserId: actorId,
            previousCredentialId: null,
            timeProvider: _timeProvider);

        credential.Id.Should().NotBe(Guid.Empty);
        credential.ProviderName.Should().Be(providerName);
        credential.EncryptedApiKey.Should().Be("ciphertext-abc");
        credential.Fingerprint.Should().Be(fingerprint);
        credential.IsActive.Should().BeTrue();
        credential.RotatedAt.Should().Be(FixedNow.UtcDateTime);
        credential.RotatedByUserId.Should().Be(actorId);
        credential.PreviousCredentialId.Should().BeNull();

        credential.DomainEvents.Should().ContainSingle(e => e is ProviderKeyRotatedEvent);
        var evt = credential.DomainEvents.OfType<ProviderKeyRotatedEvent>().Single();
        evt.ProviderName.Should().Be("deepseek");
        evt.NewFingerprint.Should().Be(fingerprint.Value);
        evt.PreviousFingerprint.Should().BeNull();
        evt.RotatedByUserId.Should().Be(actorId);
    }

    [Fact]
    public void Create_SubsequentRotation_ProducesActiveCredential_WithPreviousId()
    {
        var providerName = ProviderName.Create("openrouter");
        var fingerprint = KeyFingerprint.FromPlaintext("sk-or-v1-newkey123");
        var actorId = Guid.NewGuid();
        var previousId = Guid.NewGuid();

        var credential = ProviderCredential.Create(
            provider: providerName,
            encryptedKey: "ciphertext-new",
            fingerprint: fingerprint,
            rotatedByUserId: actorId,
            previousCredentialId: previousId,
            timeProvider: _timeProvider);

        credential.PreviousCredentialId.Should().Be(previousId);
        credential.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_EmptyRotatedByUserId_Throws()
    {
        var act = () => ProviderCredential.Create(
            ProviderName.Create("deepseek"),
            "cipher",
            KeyFingerprint.FromPlaintext("sk-de-abcd1234"),
            Guid.Empty,
            null,
            _timeProvider);

        act.Should().Throw<ArgumentException>().WithParameterName("rotatedByUserId");
    }

    [Fact]
    public void Create_EmptyCipherText_Throws()
    {
        var act = () => ProviderCredential.Create(
            ProviderName.Create("deepseek"),
            string.Empty,
            KeyFingerprint.FromPlaintext("sk-de-abcd1234"),
            Guid.NewGuid(),
            null,
            _timeProvider);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_FlipsIsActiveToFalse()
    {
        var credential = CreateSampleCredential();
        credential.IsActive.Should().BeTrue();

        credential.Deactivate(_timeProvider);

        credential.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_AlreadyInactive_Idempotent_DoesNotThrow()
    {
        var credential = CreateSampleCredential();
        credential.Deactivate(_timeProvider);

        var act = () => credential.Deactivate(_timeProvider);
        act.Should().NotThrow();
        credential.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var credential = CreateSampleCredential();
        credential.DomainEvents.Should().HaveCount(1);

        credential.ClearDomainEvents();

        credential.DomainEvents.Should().BeEmpty();
    }

    private ProviderCredential CreateSampleCredential() =>
        ProviderCredential.Create(
            ProviderName.Create("deepseek"),
            "cipher",
            KeyFingerprint.FromPlaintext("sk-de-abcd1234"),
            Guid.NewGuid(),
            null,
            _timeProvider);
}
