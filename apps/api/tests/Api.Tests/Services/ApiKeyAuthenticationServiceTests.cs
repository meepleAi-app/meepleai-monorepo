using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using System.Threading;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services;

[Trait("Category", TestCategories.Unit)]

public class ApiKeyAuthenticationServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerateApiKeyAsync_InvalidUserId_Throws()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateApiKeyAsync("not-a-guid", "Test", new[] { "read" }, null, "live", TestCancellationToken));
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ReturnsValidResult()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var userId = Guid.NewGuid();
        context.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestCancellationToken);

        var (plaintext, entity) = await service.GenerateApiKeyAsync(userId.ToString(), "My Key", new[] { "read" }, null, "live", TestCancellationToken);
        context.ApiKeys.Add(entity);
        await context.SaveChangesAsync(TestCancellationToken);

        var result = await service.ValidateApiKeyAsync(plaintext, TestCancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal(userId.ToString(), result.UserId);
        Assert.Equal("user@example.com", result.UserEmail);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_InvalidFormat_ReturnsFalse()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);

        var success = await service.RevokeApiKeyAsync("not-a-guid", Guid.NewGuid().ToString(), TestCancellationToken);

        Assert.False(success);
    }

    private static ApiKeyAuthenticationService CreateService(MeepleAiDbContext context)
    {
        var hashing = new FakePasswordHashingService();
        return new ApiKeyAuthenticationService(
            context,
            hashing,
            NullLogger<ApiKeyAuthenticationService>.Instance,
            TimeProvider.System);
    }

    private static MeepleAiDbContext CreateDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }

    private sealed class FakePasswordHashingService : IPasswordHashingService
    {
        public string HashSecret(string secret) => $"HASH::{secret}";

        public string HashSecret(string secret, int iterations) => HashSecret(secret);

        public bool VerifySecret(string secret, string storedHash) => storedHash == HashSecret(secret);
    }
}