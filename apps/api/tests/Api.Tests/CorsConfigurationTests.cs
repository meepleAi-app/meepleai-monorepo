using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public class CorsConfigurationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly WebApplicationFactoryFixture _factory;

    public CorsConfigurationTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CustomOriginsAreLoadedFromTopLevelAllowedOriginsSection()
    {
        const string origin = "https://prod.example.com";

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Staging");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AllowedOrigins:0"] = origin
                });
            });
        });

        using var scope = factory.Services.CreateScope();
        var corsPolicyProvider = scope.ServiceProvider.GetRequiredService<ICorsPolicyProvider>();

        var policy = await corsPolicyProvider.GetPolicyAsync(new DefaultHttpContext(), "web");

        Assert.NotNull(policy);
        Assert.Contains(origin, policy!.Origins);
    }
}
