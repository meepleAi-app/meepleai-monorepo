using System.Security.Cryptography;
using Api.Infrastructure;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Test to verify and generate seed data password hashes
/// </summary>
[Collection("IntegrationTests")]
public class SeedDataPasswordTest : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly WebApplicationFactoryFixture _factory;
    private readonly ITestOutputHelper _output;

    public SeedDataPasswordTest(WebApplicationFactoryFixture factory, ITestOutputHelper output)
    {
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
        Assert.NotNull(adminLogin);
        Assert.Equal("admin@meepleai.dev", adminLogin.User.Email);

        Assert.NotNull(editorLogin);
        Assert.Equal("editor@meepleai.dev", editorLogin.User.Email);

        Assert.NotNull(userLogin);
        Assert.Equal("user@meepleai.dev", userLogin.User.Email);

        _output.WriteLine("✓ All seed users can login successfully");
    }
}
