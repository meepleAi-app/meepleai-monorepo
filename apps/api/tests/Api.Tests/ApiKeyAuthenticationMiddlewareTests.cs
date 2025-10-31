using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware;
using Api.Services;
using Api.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

public class ApiKeyAuthenticationMiddlewareTests
{
    private readonly ITestOutputHelper _output;

    [Fact]
    public async Task InvokeAsync_WithValidApiKey_LogsSanitizedPath()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance);

        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "sanitized@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(user.Id, "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        var logger = new TestLogger<ApiKeyAuthenticationMiddleware>();
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new ApiKeyAuthenticationMiddleware(next, logger);

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Request.Method = "GET";
        context.Request.Path = "/api/data\r\nfetch";
        context.Request.Headers["X-API-Key"] = plaintextKey;

        // Act
        await middleware.InvokeAsync(context, service);

        // Assert
        var entry = Assert.Single(logger.Entries.Where(e => e.LogLevel == LogLevel.Information));
        Assert.Equal("/api/datafetch", entry.GetStateValue("Path"));
    }

    private static MeepleAiDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        return new MeepleAiDbContext(options);
    }
}
