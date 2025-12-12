using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for CreateDocumentCollectionCommandHandler.
/// Issue #2051: Multi-document collection creation with validation
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class CreateDocumentCollectionHandlerIntegrationTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IMediator? _mediator;
    private IDocumentCollectionRepository? _repository;
    private IUnitOfWork? _unitOfWork;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;
    private static readonly Guid TestUserId = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestGameId = new("20000000-0000-0000-0000-000000000001");
    private static readonly Guid TestPdfId1 = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TestPdfId2 = new("30000000-0000-0000-0000-000000000002");
    private static readonly Guid TestPdfId5 = new("30000000-0000-0000-0000-000000000005");
    private static readonly Guid TestPdfId6 = new("30000000-0000-0000-0000-000000000006");

    public async ValueTask InitializeAsync()
    {
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "create_collection_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);

        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={postgresPort};Database=create_collection_test;Username=postgres;Password=postgres;";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IDocumentCollectionRepository, DocumentCollectionRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(CreateDocumentCollectionCommandHandler).Assembly));

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = serviceProvider.GetRequiredService<IMediator>();
        _repository = serviceProvider.GetRequiredService<IDocumentCollectionRepository>();
        _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Handle_CreateEmptyCollection_Succeeds()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateDocumentCollectionCommand(
            gameId, TestUserId, "Basic Collection", "Test", new List<InitialDocumentRequest>());

        // Act
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().Be(gameId);
        result.DocumentCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CreateWithOneDocument_PersistsCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateDocumentCollectionCommand(
            gameId, TestUserId, "Single Doc", null,
            new List<InitialDocumentRequest> { new(TestPdfId1, "base", 0) });

        // Act
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert
        result.DocumentCount.Should().Be(1);
        result.Documents[0].PdfDocumentId.Should().Be(TestPdfId1);

        // Verify persistence
        var persisted = await _repository!.FindByGameIdAsync(gameId, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.DocumentCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CreateWithMaxDocuments_Accepts5()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var docs = new List<InitialDocumentRequest>();
        var pdfIds = new[] { TestPdfId1, TestPdfId2, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        for (int i = 0; i < pdfIds.Length; i++)
        {
            if (i > 1)
            {
                var pdf = new PdfDocumentEntity
                {
                    Id = pdfIds[i],
                    GameId = gameId,
                    FileName = $"doc{i}.pdf",
                    FilePath = $"/test/doc{i}.pdf",
                    FileSizeBytes = 5000,
                    PageCount = 10,
                    ProcessingStatus = "completed",
                    UploadedAt = DateTime.UtcNow,
                    UploadedByUserId = TestUserId
                };
                _dbContext!.PdfDocuments.Add(pdf);
            }
            docs.Add(new(pdfIds[i], "base", i));
        }
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var command = new CreateDocumentCollectionCommand(gameId, TestUserId, "Full", null, docs);

        // Act
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert
        result.DocumentCount.Should().Be(5);
        result.IsFull.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ExistingCollectionForGame_ThrowsDomainException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var cmd1 = new CreateDocumentCollectionCommand(
            gameId, TestUserId, "First", null, new List<InitialDocumentRequest>());
        await _mediator!.Send(cmd1, TestCancellationToken);

        var cmd2 = new CreateDocumentCollectionCommand(
            gameId, TestUserId, "Second", null, new List<InitialDocumentRequest>());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _mediator.Send(cmd2, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_TooManyDocuments_ThrowsDomainException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var docs = new List<InitialDocumentRequest>();
        for (int i = 0; i < 6; i++)
        {
            var id = Guid.NewGuid();
            if (i < 2)
                id = i == 0 ? TestPdfId1 : TestPdfId2;
            else
            {
                var pdf = new PdfDocumentEntity
                {
                    Id = id,
                    GameId = gameId,
                    FileName = $"d{i}.pdf",
                    FilePath = $"/d{i}.pdf",
                    FileSizeBytes = 5000,
                    PageCount = 10,
                    ProcessingStatus = "completed",
                    UploadedAt = DateTime.UtcNow,
                    UploadedByUserId = TestUserId
                };
                _dbContext!.PdfDocuments.Add(pdf);
            }
            docs.Add(new(id, "base", i));
        }
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var command = new CreateDocumentCollectionCommand(gameId, TestUserId, "TooMany", null, docs);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _mediator!.Send(command, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_DuplicateDocuments_ThrowsDomainException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateDocumentCollectionCommand(
            gameId, TestUserId, "Dup", null,
            new List<InitialDocumentRequest>
            {
                new(TestPdfId1, "base", 0),
                new(TestPdfId1, "expansion", 1)
            });

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _mediator!.Send(command, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_PdfNotFound_ThrowsDomainException()
    {
        // Arrange
        var command = new CreateDocumentCollectionCommand(
            Guid.NewGuid(), TestUserId, "Missing", null,
            new List<InitialDocumentRequest> { new(Guid.NewGuid(), "base", 0) });

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _mediator!.Send(command, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_PdfBelongsToDifferentGame_ThrowsDomainException()
    {
        // Arrange
        var differentGameId = Guid.NewGuid();
        var pdfForOtherGame = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = differentGameId,
            FileName = "other.pdf",
            FilePath = "/other.pdf",
            FileSizeBytes = 5000,
            PageCount = 10,
            ProcessingStatus = "completed",
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = TestUserId
        };
        _dbContext!.PdfDocuments.Add(pdfForOtherGame);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new CreateDocumentCollectionCommand(
            TestGameId, TestUserId, "Wrong", null,
            new List<InitialDocumentRequest> { new(pdfForOtherGame.Id, "base", 0) });

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _mediator!.Send(command, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_RollsBackOnFailure()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateDocumentCollectionCommand(
            gameId, TestUserId, "Rollback", null,
            new List<InitialDocumentRequest> { new(Guid.NewGuid(), "base", 0) });

        // Act
        try { await _mediator!.Send(command, TestCancellationToken); }
        catch (DomainException) { }

        // Assert - No collection created
        var result = await _repository!.FindByGameIdAsync(gameId, TestCancellationToken);
        result.Should().BeNull();
    }

    private async Task SeedTestDataAsync()
    {
        var testUser = new UserEntity
        {
            Id = TestUserId,
            Email = "createcollection@test.com",
            DisplayName = "Test User",
            Role = "User"
        };
        _dbContext!.Users.Add(testUser);

        var testGame = new GameEntity
        {
            Id = TestGameId,
            Name = "Test Game"
        };
        _dbContext.Games.Add(testGame);

        foreach (var pdfId in new[] { TestPdfId1, TestPdfId2, TestPdfId5, TestPdfId6 })
        {
            var pdf = new PdfDocumentEntity
            {
                Id = pdfId,
                GameId = TestGameId,
                FileName = $"test-{pdfId}.pdf",
                FilePath = $"/test/{pdfId}.pdf",
                FileSizeBytes = 5000,
                PageCount = 10,
                ProcessingStatus = "completed",
                UploadedAt = DateTime.UtcNow,
                UploadedByUserId = TestUserId
            };
            _dbContext.PdfDocuments.Add(pdf);
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }
}
