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

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new MeepleAiDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = new MeepleAiDbContext(options);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new AuthService(dbContext, timeProvider);

        var register = await authService.RegisterAsync(new RegisterCommand(
            "user@example.com",
            "Password!1",
            "Test User",
            "Admin",
            "127.0.0.1",
            "unit-tests"));

        Assert.Equal("Admin", register.User.role);
        Assert.False(string.IsNullOrWhiteSpace(register.SessionToken));

        var active = await authService.ValidateSessionAsync(register.SessionToken);
        Assert.NotNull(active);
        Assert.Equal(register.User.email, active!.User.email);

        await authService.LogoutAsync(register.SessionToken);
        var afterLogout = await authService.ValidateSessionAsync(register.SessionToken);
        Assert.Null(afterLogout);

        var login = await authService.LoginAsync(new LoginCommand(
            "user@example.com",
            "Password!1",
            null,
            null));

        Assert.NotNull(login);
        Assert.Equal(register.User.id, login!.User.id);
        Assert.NotEqual(register.SessionToken, login.SessionToken);

        var failedLogin = await authService.LoginAsync(new LoginCommand(
            "user@example.com",
            "wrong",
            null,
            null));
        Assert.Null(failedLogin);
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
