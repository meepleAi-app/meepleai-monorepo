using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for the public get-by-share-token query path.
/// Covers generate → read via token, revoke → token no longer found, bogus token.
/// Issue #2437-2.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2437")]
public sealed class PlayRecordShareTokenTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider? _serviceProvider;
    private readonly TestTimeProvider _timeProvider = new();

    public PlayRecordShareTokenTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized.");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"playrecord_sharetoken_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString);
        services.AddScoped<IPlayRecordRepository, PlayRecordRepository>();
        services.AddScoped<PlayRecordPermissionChecker>();
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<IGameCoreDataProvider, GameCoreDataProvider>();
        services.AddSingleton<TimeProvider>(_timeProvider);
        // IBlobStorageService stub — local storage returns null (raw path pass-through)
        var blobMock = new Mock<Api.Services.Pdf.IBlobStorageService>();
        blobMock.Setup(b => b.GetPresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<Api.Services.Pdf.BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((string?)null);
        services.AddScoped(_ => blobMock.Object);

        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    /// <summary>
    /// Generate a share token → GetPlayRecordByShareTokenQuery returns a DTO with the matching Id.
    /// </summary>
    [Fact(DisplayName = "GetByShareToken_AfterGenerate_ReturnsDto")]
    public async Task GetByShareToken_AfterGenerate_ReturnsDto()
    {
        // Arrange
        var userId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(userId);

        var generateResult = await SendInScopeAsync(
            new GeneratePlayRecordShareTokenCommand(recordId, userId));

        generateResult.ShareToken.Should().NotBeNullOrEmpty();

        // Act
        var dto = await SendInScopeAsync(
            new GetPlayRecordByShareTokenQuery(generateResult.ShareToken));

        // Assert
        dto.Id.Should().Be(recordId);
        dto.ShareToken.Should().Be(generateResult.ShareToken,
            "the mapper should expose the token when IsShared=true");
    }

    /// <summary>
    /// After revoking, the same token must not be findable — query throws NotFoundException.
    /// </summary>
    [Fact(DisplayName = "GetByShareToken_AfterRevoke_ThrowsNotFoundException")]
    public async Task GetByShareToken_AfterRevoke_ThrowsNotFoundException()
    {
        // Arrange
        var userId = await SeedTestUserAsync();
        var recordId = await CreateTestRecordAsync(userId);

        var generateResult = await SendInScopeAsync(
            new GeneratePlayRecordShareTokenCommand(recordId, userId));

        await SendInScopeAsync(
            new RevokePlayRecordShareTokenCommand(recordId, userId));

        // Act
        Func<Task> act = async () =>
            await SendInScopeAsync(new GetPlayRecordByShareTokenQuery(generateResult.ShareToken));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>(
            "IsShared=false after revoke — token must not be accessible");
    }

    /// <summary>
    /// A token that was never issued throws NotFoundException.
    /// </summary>
    [Fact(DisplayName = "GetByShareToken_BogusToken_ThrowsNotFoundException")]
    public async Task GetByShareToken_BogusToken_ThrowsNotFoundException()
    {
        // Act
        Func<Task> act = async () =>
            await SendInScopeAsync(new GetPlayRecordByShareTokenQuery("this-token-does-not-exist"));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> SeedTestUserAsync()
    {
        var id = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext(_connectionString);
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"sharetoken-{id:N}@meepleai.test",
            DisplayName = "ShareToken Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    private async Task<Guid> CreateTestRecordAsync(Guid creatorUserId)
    {
        var gameId = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext(_connectionString);
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "ShareToken Test Game",
            YearPublished = 2024,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);

        var command = new CreatePlayRecordCommand(
            creatorUserId,
            gameId,
            "ShareToken Test Game",
            _timeProvider.UtcNow.AddHours(-1),
            PlayRecordVisibility.Private);

        return await SendInScopeAsync(command);
    }

    private async Task<TResult> SendInScopeAsync<TResult>(IRequest<TResult> request)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request, TestCancellationToken);
    }

    private async Task SendInScopeAsync(IRequest request)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(request, TestCancellationToken);
    }
}
