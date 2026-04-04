using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain;

[Trait("Category", TestCategories.Unit)]
public class NotificationPreferencesPushTests
{
    [Fact]
    public void UpdatePushSubscription_SetsFields()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());

        prefs.UpdatePushSubscription("https://push.example.com/endpoint", "p256dh", "auth");

        prefs.PushEndpoint.Should().Be("https://push.example.com/endpoint");
        prefs.PushP256dhKey.Should().Be("p256dh");
        prefs.PushAuthKey.Should().Be("auth");
        prefs.HasPushSubscription.Should().BeTrue();
    }

    [Fact]
    public void ClearPushSubscription_ClearsAllFields()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());
        prefs.UpdatePushSubscription("https://push.example.com/endpoint", "p256dh", "auth");

        prefs.ClearPushSubscription();

        prefs.PushEndpoint.Should().BeNull();
        prefs.PushP256dhKey.Should().BeNull();
        prefs.PushAuthKey.Should().BeNull();
        prefs.HasPushSubscription.Should().BeFalse();
    }

    [Fact]
    public void HasPushSubscription_ReturnsFalse_WhenEndpointIsEmpty()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());

        prefs.HasPushSubscription.Should().BeFalse();
    }

    [Fact]
    public void HasPushSubscription_ReturnsFalse_WhenEndpointIsEmptyString()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());
        prefs.UpdatePushSubscription("", "p256dh", "auth");

        prefs.HasPushSubscription.Should().BeFalse();
    }

    [Fact]
    public void Reconstitute_WithPushSubscription_RestoresFields()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var prefs = NotificationPreferences.Reconstitute(
            id, userId,
            true, true, false,
            true, true, false,
            true, true, true,
            "https://push.example.com/endpoint", "p256dh", "auth");

        prefs.Id.Should().Be(id);
        prefs.UserId.Should().Be(userId);
        prefs.PushEndpoint.Should().Be("https://push.example.com/endpoint");
        prefs.PushP256dhKey.Should().Be("p256dh");
        prefs.PushAuthKey.Should().Be("auth");
        prefs.HasPushSubscription.Should().BeTrue();
    }

    [Fact]
    public void Reconstitute_WithoutPushSubscription_DefaultsToNull()
    {
        var prefs = NotificationPreferences.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(),
            true, true, false,
            true, true, false,
            true, true, true);

        prefs.PushEndpoint.Should().BeNull();
        prefs.PushP256dhKey.Should().BeNull();
        prefs.PushAuthKey.Should().BeNull();
        prefs.HasPushSubscription.Should().BeFalse();
    }
}
