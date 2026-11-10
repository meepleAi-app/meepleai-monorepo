using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// #4613: <see cref="OAuthAccount.Reconstitute"/> rimette in piedi un collegamento salvato, senza passare dal
/// costruttore di un collegamento nuovo.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4613")]
public sealed class OAuthAccountReconstituteTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static OAuthAccount Reconstitute(string provider = "github") =>
        OAuthAccount.Reconstitute(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            provider: provider,
            providerUserId: "gh-1",
            accessTokenEncrypted: "access",
            refreshTokenEncrypted: "refresh",
            tokenExpiresAt: CreatedAt.AddHours(1),
            createdAt: CreatedAt,
            updatedAt: CreatedAt.AddMinutes(30));

    [Fact]
    public void Reconstitute_KeepsEveryValueAsStored()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var account = OAuthAccount.Reconstitute(
            id, userId, "discord", "d-9", "access", null, null, CreatedAt, CreatedAt.AddMinutes(30));

        account.Id.Should().Be(id);
        account.UserId.Should().Be(userId);
        account.Provider.Should().Be("discord");
        account.ProviderUserId.Should().Be("d-9");
        account.AccessTokenEncrypted.Should().Be("access");
        account.RefreshTokenEncrypted.Should().BeNull();
        account.TokenExpiresAt.Should().BeNull();
        account.CreatedAt.Should().Be(CreatedAt, "the constructor would have said now");
        account.UpdatedAt.Should().Be(CreatedAt.AddMinutes(30), "the constructor would have said now");
    }

    [Fact]
    public void Reconstitute_RaisesNoEvent()
    {
        Reconstitute().DomainEvents.Should().BeEmpty("an account that is read was linked long ago");
    }

    [Fact]
    public void Reconstitute_DoesNotValidateWhatWasAlreadyStored()
    {
        // The constructor refuses a provider that is not on the list. A link written while the provider was on
        // the list has to stay readable: reading is not the place to re-judge it.
        var act = () => Reconstitute(provider: "twitter");

        act.Should().NotThrow();
    }

    [Fact]
    public void Reconstitute_ThenARealChange_RaisesOnlyThatEvent()
    {
        var account = Reconstitute();

        account.UpdateTokens("access-2", "refresh-2", CreatedAt.AddDays(1));

        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<OAuthTokensRefreshedEvent>();
    }

    [Fact]
    public void TheConstructor_StillRaisesTheLinkedEvent()
    {
        var account = new OAuthAccount(Guid.NewGuid(), Guid.NewGuid(), "google", "g-1", "access");

        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<OAuthAccountLinkedEvent>();
    }
}
