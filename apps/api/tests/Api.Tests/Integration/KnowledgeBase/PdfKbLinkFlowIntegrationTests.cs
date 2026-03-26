using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for the PDF-KB link flow.
/// Tests ChatThread KB selection persistence round-trip and DTO serialization.
/// Feature: Upload PDF → Detect Existing KB → Link → Chat with KB Selection
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PdfKbLinkFlowIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IChatThreadRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("10000000-0000-0000-0000-000000000099");
    private static readonly Guid TestGameId = new("20000000-0000-0000-0000-000000000099");

    public PdfKbLinkFlowIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pdfkblink_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IChatThreadRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        // Seed required parent entities for FK constraints
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = TestUserId,
            Email = "test-pdfkb@example.com",
            PasswordHash = "test-hash",
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.Games.Add(new Api.Infrastructure.Entities.GameEntity
        {
            Id = TestGameId,
            Name = "Test Game for KB Link",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Helper Methods

    private async Task EnsureUserExistsAsync(Guid userId)
    {
        using var scope = _serviceProvider!.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existingUser = await userRepo.GetByIdAsync(userId, TestCancellationToken);
        if (existingUser == null)
        {
            var user = new User(
                id: userId,
                email: new Email($"user{userId:N}@test.com"),
                displayName: "Test User",
                passwordHash: PasswordHash.Create("TestPassword123!"),
                role: Role.User,
                tier: UserTier.Free);
            await userRepo.AddAsync(user, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
        }
    }

    #endregion

    #region ChatThread KB Selection Persistence

    [Fact]
    public async Task ChatThread_WithSelectedKbs_ShouldPersistAndRetrieve()
    {
        // Arrange
        await EnsureUserExistsAsync(TestUserId);

        var threadId = Guid.NewGuid();
        var kbIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var thread = new ChatThread(id: threadId, userId: TestUserId, gameId: TestGameId, title: "KB Selection Test");
        thread.SetSelectedKnowledgeBases(kbIds);

        // Act — persist
        await _repository!.AddAsync(thread, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act — retrieve from fresh context
        using var scope = _serviceProvider!.CreateScope();
        var freshRepo = scope.ServiceProvider.GetRequiredService<IChatThreadRepository>();
        var retrieved = await freshRepo.GetByIdAsync(threadId, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.SelectedKnowledgeBaseIdsJson.Should().NotBeNullOrEmpty();

        var retrievedKbIds = retrieved.GetSelectedKnowledgeBaseIds();
        retrievedKbIds.Should().NotBeNull();
        retrievedKbIds.Should().HaveCount(3);
        retrievedKbIds.Should().BeEquivalentTo(kbIds);
    }

    [Fact]
    public async Task ChatThread_WithoutSelectedKbs_ShouldPersistNullAndRetrieveNull()
    {
        // Arrange
        await EnsureUserExistsAsync(TestUserId);

        var threadId = Guid.NewGuid();
        var thread = new ChatThread(id: threadId, userId: TestUserId, gameId: TestGameId, title: "No KB Selection");

        // Act — persist
        await _repository!.AddAsync(thread, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act — retrieve
        using var scope = _serviceProvider!.CreateScope();
        var freshRepo = scope.ServiceProvider.GetRequiredService<IChatThreadRepository>();
        var retrieved = await freshRepo.GetByIdAsync(threadId, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.SelectedKnowledgeBaseIdsJson.Should().BeNull();
        retrieved.GetSelectedKnowledgeBaseIds().Should().BeNull();
    }

    [Fact]
    public async Task ChatThread_UpdateSelectedKbs_ShouldPersistNewSelection()
    {
        // Arrange
        await EnsureUserExistsAsync(TestUserId);

        var threadId = Guid.NewGuid();
        var initialKbIds = new List<Guid> { Guid.NewGuid() };
        var thread = new ChatThread(id: threadId, userId: TestUserId, gameId: TestGameId, title: "Update KB Selection");
        thread.SetSelectedKnowledgeBases(initialKbIds);

        await _repository!.AddAsync(thread, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act — update selection
        var updatedKbIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var retrievedForUpdate = await _repository.GetByIdAsync(threadId, TestCancellationToken);
        retrievedForUpdate!.SetSelectedKnowledgeBases(updatedKbIds);
        await _repository.UpdateAsync(retrievedForUpdate, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act — verify
        using var scope = _serviceProvider!.CreateScope();
        var freshRepo = scope.ServiceProvider.GetRequiredService<IChatThreadRepository>();
        var final = await freshRepo.GetByIdAsync(threadId, TestCancellationToken);

        // Assert
        final.Should().NotBeNull();
        var finalKbIds = final!.GetSelectedKnowledgeBaseIds();
        finalKbIds.Should().HaveCount(2);
        finalKbIds.Should().BeEquivalentTo(updatedKbIds);
    }

    [Fact]
    public async Task ChatThread_ClearSelectedKbs_ShouldPersistNull()
    {
        // Arrange
        await EnsureUserExistsAsync(TestUserId);

        var threadId = Guid.NewGuid();
        var kbIds = new List<Guid> { Guid.NewGuid() };
        var thread = new ChatThread(id: threadId, userId: TestUserId, gameId: TestGameId, title: "Clear KB Selection");
        thread.SetSelectedKnowledgeBases(kbIds);

        await _repository!.AddAsync(thread, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act — clear selection
        var retrieved = await _repository.GetByIdAsync(threadId, TestCancellationToken);
        retrieved!.SetSelectedKnowledgeBases(null);
        await _repository.UpdateAsync(retrieved, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Verify
        using var scope = _serviceProvider!.CreateScope();
        var freshRepo = scope.ServiceProvider.GetRequiredService<IChatThreadRepository>();
        var final = await freshRepo.GetByIdAsync(threadId, TestCancellationToken);

        // Assert
        final.Should().NotBeNull();
        final!.SelectedKnowledgeBaseIdsJson.Should().BeNull();
        final.GetSelectedKnowledgeBaseIds().Should().BeNull();
    }

    #endregion

    #region DTO Serialization Round-Trips

    [Fact]
    public void ExistingKbInfoDto_JsonRoundTrip_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "shared",
            FileName: "catan-rules.pdf",
            ProcessingState: "Ready",
            TotalChunks: 108,
            OriginalGameName: "Catan",
            SharedGameId: Guid.NewGuid());

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ExistingKbInfoDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.PdfDocumentId.Should().Be(original.PdfDocumentId);
        deserialized.Source.Should().Be("shared");
        deserialized.FileName.Should().Be("catan-rules.pdf");
        deserialized.ProcessingState.Should().Be("Ready");
        deserialized.TotalChunks.Should().Be(108);
        deserialized.OriginalGameName.Should().Be("Catan");
        deserialized.SharedGameId.Should().Be(original.SharedGameId);
    }

    [Fact]
    public void ExistingKbInfoDto_WithNullOptionals_ShouldRoundTrip()
    {
        // Arrange
        var original = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "user",
            FileName: "my-rules.pdf",
            ProcessingState: "Embedding",
            TotalChunks: null,
            OriginalGameName: null,
            SharedGameId: null);

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ExistingKbInfoDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Source.Should().Be("user");
        deserialized.TotalChunks.Should().BeNull();
        deserialized.OriginalGameName.Should().BeNull();
        deserialized.SharedGameId.Should().BeNull();
    }

    [Fact]
    public void LinkKbResultDto_JsonRoundTrip_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new LinkKbResultDto(
            VectorDocumentId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            Status: "linked");

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<LinkKbResultDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.VectorDocumentId.Should().Be(original.VectorDocumentId);
        deserialized.GameId.Should().Be(original.GameId);
        deserialized.PdfDocumentId.Should().Be(original.PdfDocumentId);
        deserialized.Status.Should().Be("linked");
    }

    [Fact]
    public void PdfUploadResult_WithExistingKb_ShouldRoundTrip()
    {
        // Arrange
        var kbInfo = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "shared",
            FileName: "test.pdf",
            ProcessingState: "Ready",
            TotalChunks: 42,
            OriginalGameName: "Test Game",
            SharedGameId: Guid.NewGuid());

        var original = new PdfUploadResult(true, "Existing KB found", null, kbInfo);

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<PdfUploadResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Success.Should().BeTrue();
        deserialized.ExistingKb.Should().NotBeNull();
        deserialized.ExistingKb!.Source.Should().Be("shared");
        deserialized.ExistingKb.TotalChunks.Should().Be(42);
    }

    #endregion
}
