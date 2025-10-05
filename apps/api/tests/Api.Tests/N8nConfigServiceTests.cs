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

public class N8nConfigServiceTests
{
    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static N8nConfigService CreateService(MeepleAiDbContext dbContext, Mock<IHttpClientFactory>? httpClientFactoryMock = null)
    {
        httpClientFactoryMock ??= new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        var configurationMock = new Mock<IConfiguration>();
        configurationMock
            .Setup(c => c[It.Is<string>(key => key == "N8N_ENCRYPTION_KEY")])
            .Returns("test-encryption-key");

        var loggerMock = new Mock<ILogger<N8nConfigService>>();

        return new N8nConfigService(dbContext, httpClientFactoryMock.Object, configurationMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task CreateConfigAsync_PersistsConfigWithTrimmedValues()
    {
        await using var dbContext = CreateInMemoryContext();
        var service = CreateService(dbContext);

        var result = await service.CreateConfigAsync(
            "user-1",
            new CreateN8nConfigRequest("Config One", "https://example.com/", "secret", "https://webhook.test/"),
            CancellationToken.None);

        Assert.Equal("Config One", result.Name);
        Assert.Equal("https://example.com", result.BaseUrl);
        Assert.Equal("https://webhook.test", result.WebhookUrl);

        var entity = await dbContext.N8nConfigs.FirstAsync();
        Assert.Equal("user-1", entity.CreatedByUserId);
        Assert.True(entity.IsActive);
        Assert.NotEqual("secret", entity.ApiKeyEncrypted);
    }

    [Fact]
    public async Task UpdateConfigAsync_ModifiesFields()
    {
        await using var dbContext = CreateInMemoryContext();
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

        Assert.Equal("Updated", updated.Name);
        Assert.Equal("https://updated.com", updated.BaseUrl);
        Assert.Equal("https://hook/new", updated.WebhookUrl);
        Assert.False(updated.IsActive);

        var refreshed = await dbContext.N8nConfigs.FirstAsync();
        Assert.Equal("Updated", refreshed.Name);
        Assert.Equal("https://updated.com", refreshed.BaseUrl);
        Assert.Equal("https://hook/new", refreshed.WebhookUrl);
        Assert.False(refreshed.IsActive);
        Assert.True(refreshed.UpdatedAt > previousUpdatedAt);
        Assert.NotEqual(previousApiKey, refreshed.ApiKeyEncrypted);
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenNameConflicts_Throws()
    {
        await using var dbContext = CreateInMemoryContext();
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
        var service = CreateService(dbContext);

        var created = await service.CreateConfigAsync(
            "user",
            new CreateN8nConfigRequest("DeleteMe", "https://delete.com", "key", null),
            CancellationToken.None);

        var deleted = await service.DeleteConfigAsync(created.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(dbContext.N8nConfigs);
    }

    [Fact]
    public async Task DeleteConfigAsync_WhenMissing_ReturnsFalse()
    {
        await using var dbContext = CreateInMemoryContext();
        var service = CreateService(dbContext);

        var deleted = await service.DeleteConfigAsync("missing", CancellationToken.None);

        Assert.False(deleted);
    }
}
