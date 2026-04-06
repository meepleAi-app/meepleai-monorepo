using Api.BoundedContexts.UserNotifications.Infrastructure.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SlackHttpClientRegistrationTests
{
    [Fact]
    public void AddUserNotificationsContext_SlackApiClient_HasTenSecondTimeout()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://meepleai.app"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddUserNotificationsContext(config);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("SlackApi");

        client.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }
}
