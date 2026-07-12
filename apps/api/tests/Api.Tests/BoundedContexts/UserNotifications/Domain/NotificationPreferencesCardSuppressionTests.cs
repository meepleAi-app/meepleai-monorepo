using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationPreferencesCardSuppressionTests
{
    [Fact]
    public void New_DefaultsEmailOnCardSuppressed_False()
    {
        new NotificationPreferences(Guid.NewGuid()).EmailOnCardSuppressed.Should().BeFalse();
    }

    [Fact]
    public void UpdateCardSuppressionEmailPreference_SetsFlag()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());

        prefs.UpdateCardSuppressionEmailPreference(true);
        prefs.EmailOnCardSuppressed.Should().BeTrue();

        prefs.UpdateCardSuppressionEmailPreference(false);
        prefs.EmailOnCardSuppressed.Should().BeFalse();
    }
}
