using System;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class AuthServiceTests
{
    [Fact]
    public async Task RegisterLoginLogout_RoundTrip_Succeeds()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new AuthService(dbContext, timeProvider);

        var register = await authService.RegisterAsync(new RegisterCommand(
            "user@example.com",
            "Password!1",
            "Test User",
            "Admin",
            "127.0.0.1",
            "unit-tests"));

        Assert.False(string.IsNullOrWhiteSpace(register.User.id));
        Assert.Equal("user@example.com", register.User.email);
        Assert.Equal("Test User", register.User.displayName);
        Assert.Equal("Admin", register.User.role);
        Assert.False(string.IsNullOrWhiteSpace(register.SessionToken));

        var active = await authService.ValidateSessionAsync(register.SessionToken);
        Assert.NotNull(active);
        Assert.Equal(register.User, active!.User);

        await authService.LogoutAsync(register.SessionToken);
        var afterLogout = await authService.ValidateSessionAsync(register.SessionToken);
        Assert.Null(afterLogout);

        var login = await authService.LoginAsync(new LoginCommand(
            "user@example.com",
            "Password!1",
            null,
            null));

        Assert.NotNull(login);
        Assert.Equal(register.User, login!.User);
        Assert.NotEqual(register.SessionToken, login.SessionToken);

        var failedLogin = await authService.LoginAsync(new LoginCommand(
            "user@example.com",
            "wrong",
            null,
            null));
        Assert.Null(failedLogin);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Editor")]
    public async Task RegisterAsync_RejectsElevatedRolesAfterBootstrap(string requestedRole)
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new AuthService(dbContext, timeProvider);

        await authService.RegisterAsync(new RegisterCommand(
            "bootstrap@example.com",
            "Password!1",
            "Bootstrap Admin",
            "Admin",
            "127.0.0.1",
            "unit-tests"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => authService.RegisterAsync(new RegisterCommand(
            $"{requestedRole.ToLowerInvariant()}@example.com",
            "Password!1",
            $"{requestedRole} User",
            requestedRole,
            "127.0.0.1",
            "unit-tests")));

        Assert.Equal("Only administrators can assign elevated roles.", exception.Message);
    }

    private static MeepleAiDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        return new MeepleAiDbContext(options);
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
