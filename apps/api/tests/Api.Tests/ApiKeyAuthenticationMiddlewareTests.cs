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
using FluentAssertions;
using Xunit;

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
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance);

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "sanitized@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(user.Id.ToString(), "Test Key", "read");
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        var logger = new TestLogger<ApiKeyAuthenticationMiddleware>();
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new ApiKeyAuthenticationMiddleware(next, logger);

        using var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        context.Request.Method = "GET";
        context.Request.Path = "/api/data\r\nfetch";
        context.Request.Headers["X-API-Key"] = plaintextKey;

        // Act
        await middleware.InvokeAsync(context, service);

        // Assert
        var entry = logger.Entries.Where(e => e.LogLevel == LogLevel.Information).Should().ContainSingle().Subject;
        entry.GetStateValue("Path").Should().Be("/api/datafetch");
    }

    private static MeepleAiDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        return new MeepleAiDbContext(options);
    }
}
