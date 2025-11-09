using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit;
using Api.Tests.Fixtures;

namespace Api.Tests;

[Collection("Postgres Integration Tests")]
public class CorsConfigurationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly ITestOutputHelper _output;

    private readonly WebApplicationFactoryFixture _factory;

    public CorsConfigurationTests(PostgresCollectionFixture postgresFixture, WebApplicationFactoryFixture factory, ITestOutputHelper output)
    {
        _output = output;
        factory.PostgresConnectionString = postgresFixture.ConnectionString;
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

        policy.Should().NotBeNull();
        policy!.Origins.Should().Contain(origin);
    }
}
