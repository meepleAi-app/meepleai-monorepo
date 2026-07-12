using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationTypeCardSuppressedTests
{
    [Fact]
    public void FromString_ParsesAdminMechanicCardSuppressed()
    {
        NotificationType.FromString("admin_mechanic_card_suppressed")
            .Should().Be(NotificationType.AdminMechanicCardSuppressed);
        NotificationType.AdminMechanicCardSuppressed.Value.Should().Be("admin_mechanic_card_suppressed");
    }
}
