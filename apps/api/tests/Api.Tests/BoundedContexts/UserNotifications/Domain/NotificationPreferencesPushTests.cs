using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain;

[Trait("Category", TestCategories.Unit)]
public class NotificationPreferencesPushTests
{
    [Fact]
    public void UpdatePushSubscription_SetsFields()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());

        prefs.UpdatePushSubscription("https://push.example.com/endpoint", "p256dh", "auth");

        Assert.Equal("https://push.example.com/endpoint", prefs.PushEndpoint);
        Assert.Equal("p256dh", prefs.PushP256dhKey);
        Assert.Equal("auth", prefs.PushAuthKey);
        Assert.True(prefs.HasPushSubscription);
    }

    [Fact]
    public void ClearPushSubscription_ClearsAllFields()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());
        prefs.UpdatePushSubscription("https://push.example.com/endpoint", "p256dh", "auth");

        prefs.ClearPushSubscription();

        Assert.Null(prefs.PushEndpoint);
        Assert.Null(prefs.PushP256dhKey);
        Assert.Null(prefs.PushAuthKey);
        Assert.False(prefs.HasPushSubscription);
    }

    [Fact]
    public void HasPushSubscription_ReturnsFalse_WhenEndpointIsEmpty()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());

        Assert.False(prefs.HasPushSubscription);
    }

    [Fact]
    public void HasPushSubscription_ReturnsFalse_WhenEndpointIsEmptyString()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());
        prefs.UpdatePushSubscription("", "p256dh", "auth");

        Assert.False(prefs.HasPushSubscription);
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

        Assert.Equal(id, prefs.Id);
        Assert.Equal(userId, prefs.UserId);
        Assert.Equal("https://push.example.com/endpoint", prefs.PushEndpoint);
        Assert.Equal("p256dh", prefs.PushP256dhKey);
        Assert.Equal("auth", prefs.PushAuthKey);
        Assert.True(prefs.HasPushSubscription);
    }

    [Fact]
    public void Reconstitute_WithoutPushSubscription_DefaultsToNull()
    {
        var prefs = NotificationPreferences.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(),
            true, true, false,
            true, true, false,
            true, true, true);

        Assert.Null(prefs.PushEndpoint);
        Assert.Null(prefs.PushP256dhKey);
        Assert.Null(prefs.PushAuthKey);
        Assert.False(prefs.HasPushSubscription);
    }
}
