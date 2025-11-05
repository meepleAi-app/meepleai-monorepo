using System;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

public class AuthServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;

    public AuthServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    private MeepleAiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new MeepleAiDbContext(options);
    }

    [Fact]
    public async Task RegisterLoginLogout_RoundTrip_Succeeds()
    {
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var mockPasswordHashing = new Mock<IPasswordHashingService>();
        var authService = new AuthService(dbContext, mockPasswordHashing.Object, sessionCache: null, timeProvider: timeProvider);

        var register = await authService.RegisterAsync(new RegisterCommand(
            Email: "user@example.com",
            Password: "Password!1",
            DisplayName: "Test User",
            Role: "Admin",
            IpAddress: "127.0.0.1",
            UserAgent: "unit-tests"));

        register.User.Id.Should().NotBeNullOrWhiteSpace();
        register.User.Email.Should().Be("user@example.com");
        register.User.DisplayName.Should().Be("Test User");
        register.User.Role.Should().Be("Admin");
        register.SessionToken.Should().NotBeNullOrWhiteSpace();

        var active = await authService.ValidateSessionAsync(register.SessionToken);
        active.Should().NotBeNull();
        active!.User.Should().Be(register.User);

        await authService.LogoutAsync(register.SessionToken);
        var afterLogout = await authService.ValidateSessionAsync(register.SessionToken);
        afterLogout.Should().BeNull();

        var login = await authService.LoginAsync(new LoginCommand(
            Email: "user@example.com",
            Password: "Password!1",
            IpAddress: null,
            UserAgent: null));

        login.Should().NotBeNull();
        login!.User.Should().Be(register.User);
        login.SessionToken.Should().NotBe(register.SessionToken);

        var failedLogin = await authService.LoginAsync(new LoginCommand(
            Email: "user@example.com",
            Password: "wrong",
            IpAddress: null,
            UserAgent: null));
        failedLogin.Should().BeNull();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Editor")]
    public async Task RegisterAsync_RejectsElevatedRolesAfterBootstrap(string requestedRole)
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var mockPasswordHashing = new Mock<IPasswordHashingService>();
        var authService = new AuthService(dbContext, mockPasswordHashing.Object, sessionCache: null, timeProvider: timeProvider);

        await authService.RegisterAsync(new RegisterCommand(
            Email: "bootstrap@example.com",
            Password: "Password!1",
            DisplayName: "Bootstrap Admin",
            Role: "Admin",
            IpAddress: "127.0.0.1",
            UserAgent: "unit-tests"));

        var exception = await FluentActions.Invoking(() => authService.RegisterAsync(new RegisterCommand(
            Email: $"{requestedRole.ToLowerInvariant()}@example.com",
            Password: "Password!1",
            DisplayName: $"{requestedRole} User",
            Role: requestedRole,
            IpAddress: "127.0.0.1",
            UserAgent: "unit-tests")))
            .Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Be("Only administrators can assign elevated roles.");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}