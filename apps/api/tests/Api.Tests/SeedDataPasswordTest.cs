using System.Security.Cryptography;
using Api.Infrastructure;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;
using Api.Tests.Fixtures;

namespace Api.Tests;

/// <summary>
/// Test to verify and generate seed data password hashes
/// </summary>
[Collection("Postgres Integration Tests")]
public class SeedDataPasswordTest : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly WebApplicationFactoryFixture _factory;
    private readonly ITestOutputHelper _output;

    public SeedDataPasswordTest(PostgresCollectionFixture postgresFixture, WebApplicationFactoryFixture factory, ITestOutputHelper output)
    {
        factory.PostgresConnectionString = postgresFixture.ConnectionString;
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task SeedDataUsersCanLogin()
    {
        // Arrange
        var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        // Act - Try to login with seed users
        var adminLogin = await authService.LoginAsync(new Api.Models.LoginCommand(
            Email: "admin@meepleai.dev",
            Password: "Demo123!",
            IpAddress: null,
            UserAgent: null
        ));

        var editorLogin = await authService.LoginAsync(new Api.Models.LoginCommand(
            Email: "editor@meepleai.dev",
            Password: "Demo123!",
            IpAddress: null,
            UserAgent: null
        ));

        var userLogin = await authService.LoginAsync(new Api.Models.LoginCommand(
            Email: "user@meepleai.dev",
            Password: "Demo123!",
            IpAddress: null,
            UserAgent: null
        ));

        // Assert
        adminLogin.Should().NotBeNull();
        adminLogin.User.Email.Should().Be("admin@meepleai.dev");

        editorLogin.Should().NotBeNull();
        editorLogin.User.Email.Should().Be("editor@meepleai.dev");

        userLogin.Should().NotBeNull();
        userLogin.User.Email.Should().Be("user@meepleai.dev");

        _output.WriteLine("✓ All seed users can login successfully");
    }
}
