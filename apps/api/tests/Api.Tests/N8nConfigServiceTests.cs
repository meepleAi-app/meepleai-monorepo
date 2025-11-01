using System.Net.Http;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

public class N8nConfigServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;

    public N8nConfigServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    private MeepleAiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task SeedUserAsync(MeepleAiDbContext dbContext, string userId)
    {
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"{userId}@example.com",
            PasswordHash = "hashed-password",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static N8nConfigService CreateService(
        MeepleAiDbContext dbContext,
        Mock<IHttpClientFactory>? httpClientFactoryMock = null,
        string? encryptionKey = "test-encryption-key")
    {
        httpClientFactoryMock ??= new Mock<IHttpClientFactory>();
        // Create a mock handler to avoid CA2000 error
        var mockHandler = new Mock<HttpMessageHandler>();
        var mockHttpClient = new HttpClient(mockHandler.Object);
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(mockHttpClient);

        var configurationMock = new Mock<IConfiguration>();
        configurationMock
            .Setup(c => c[It.Is<string>(key => key == "N8N_ENCRYPTION_KEY")])
            .Returns(encryptionKey);

        var loggerMock = new Mock<ILogger<N8nConfigService>>();

        return new N8nConfigService(dbContext, httpClientFactoryMock.Object, configurationMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task CreateConfigAsync_WhenEncryptionKeyMissing_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        await SeedUserAsync(dbContext, "user");

        var service = CreateService(dbContext, encryptionKey: null);

        var act = () => service.CreateConfigAsync(
            "user",
            new CreateN8nConfigRequest("NoKey", "https://example.com", "secret", null),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task CreateConfigAsync_WhenEncryptionKeyIsPlaceholder_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        await SeedUserAsync(dbContext, "user");

        const string placeholder = "changeme-replace-with-32-byte-key-in-production";
        var service = CreateService(dbContext, encryptionKey: placeholder);

        var act = () => service.CreateConfigAsync(
            "user",
            new CreateN8nConfigRequest("Placeholder", "https://example.com", "secret", null),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task CreateConfigAsync_PersistsConfigWithTrimmedValues()
    {
        await using var dbContext = CreateInMemoryContext();
        await SeedUserAsync(dbContext, "user-1");
        var service = CreateService(dbContext);

        var result = await service.CreateConfigAsync(
            "user-1",
            new CreateN8nConfigRequest("Config One", "https://example.com/", "secret", "https://webhook.test/"),
            CancellationToken.None);

        result.Name.Should().Be("Config One");
        result.BaseUrl.Should().Be("https://example.com");
        result.WebhookUrl.Should().Be("https://webhook.test");

        var entity = await dbContext.N8nConfigs.FirstAsync();
        entity.CreatedByUserId.Should().Be("user-1");
        entity.IsActive.Should().BeTrue();
        entity.ApiKeyEncrypted.Should().NotBe("secret");
    }

    [Fact]
    public async Task UpdateConfigAsync_ModifiesFields()
    {
        await using var dbContext = CreateInMemoryContext();
        await SeedUserAsync(dbContext, "creator");
        var service = CreateService(dbContext);

        var created = await service.CreateConfigAsync(
            "creator",
            new CreateN8nConfigRequest("Original", "https://origin.com", "initial", "https://hook/"),
            CancellationToken.None);

        var entity = await dbContext.N8nConfigs.FirstAsync();
        var previousUpdatedAt = entity.UpdatedAt;
        var previousApiKey = entity.ApiKeyEncrypted;

        var updated = await service.UpdateConfigAsync(
            created.Id,
            new UpdateN8nConfigRequest("Updated", "https://updated.com/", "new-secret", "https://hook/new/", false),
            CancellationToken.None);

        updated.Name.Should().Be("Updated");
        updated.BaseUrl.Should().Be("https://updated.com");
        updated.WebhookUrl.Should().Be("https://hook/new");
        updated.IsActive.Should().BeFalse();

        var refreshed = await dbContext.N8nConfigs.FirstAsync();
        refreshed.Name.Should().Be("Updated");
        refreshed.BaseUrl.Should().Be("https://updated.com");
        refreshed.WebhookUrl.Should().Be("https://hook/new");
        refreshed.IsActive.Should().BeFalse();
        refreshed.UpdatedAt > previousUpdatedAt.Should().BeTrue();
        refreshed.ApiKeyEncrypted.Should().NotBe(previousApiKey);
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenNameConflicts_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
        await SeedUserAsync(dbContext, "user");
        var service = CreateService(dbContext);

        var first = await service.CreateConfigAsync(
            "user",
            new CreateN8nConfigRequest("First", "https://one.com", "key1", null),
            CancellationToken.None);

        await service.CreateConfigAsync(
            "user",
            new CreateN8nConfigRequest("Second", "https://two.com", "key2", null),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateConfigAsync(
            first.Id,
            new UpdateN8nConfigRequest("Second", null, null, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task DeleteConfigAsync_RemovesEntity()
    {
        await using var dbContext = CreateInMemoryContext();
        await SeedUserAsync(dbContext, "user");
        var service = CreateService(dbContext);

        var created = await service.CreateConfigAsync(
            "user",
            new CreateN8nConfigRequest("DeleteMe", "https://delete.com", "key", null),
            CancellationToken.None);

        var deleted = await service.DeleteConfigAsync(created.Id, CancellationToken.None);

        deleted.Should().BeTrue();
        dbContext.N8nConfigs.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteConfigAsync_WhenMissing_ReturnsFalse()
    {
        await using var dbContext = CreateInMemoryContext();
        await SeedUserAsync(dbContext, "user");
        var service = CreateService(dbContext);

        var deleted = await service.DeleteConfigAsync("missing", CancellationToken.None);

        deleted.Should().BeFalse();
    }
}