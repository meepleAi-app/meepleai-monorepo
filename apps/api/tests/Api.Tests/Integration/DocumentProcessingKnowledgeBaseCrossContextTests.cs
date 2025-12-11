using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration;

/// <summary>
/// Cross-context integration tests: DocumentProcessing ↔ KnowledgeBase.
/// Tests PDF upload, processing, vector embedding, and RAG search integration.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class DocumentProcessingKnowledgeBaseCrossContextTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized");

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Always start isolated Postgres for this suite
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "doc_kb_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);
        var containerPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={containerPort};Database=doc_kb_test;Username=postgres;Password=postgres;";

        // Enforce SSL disabled and stable connection settings
        var enforcedBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };
        enforcedBuilder.Host = string.IsNullOrWhiteSpace(enforcedBuilder.Host) || enforcedBuilder.Host == "localhost"
            ? "127.0.0.1"
            : enforcedBuilder.Host;
        connectionString = enforcedBuilder.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IVectorDocumentRepository, VectorDocumentRepository>();
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain event infrastructure
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();

        // Register MediatR (required by MeepleAiDbContext for domain event dispatching)
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(Api.BoundedContexts.Authentication.Application.Commands.LoginCommandHandler).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await EnsureCreatedWithRetry(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }
    }

    private static async Task EnsureCreatedWithRetry(MeepleAiDbContext context)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await context.Database.EnsureCreatedAsync(TestCancellationToken);
                return;
            }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    private async Task ResetDatabaseAsync()
    {
        var tableNames = await _dbContext!.Database
            .SqlQueryRaw<string>(
                @"SELECT tablename
                  FROM pg_tables
                  WHERE schemaname = 'public'
                  AND tablename != '__EFMigrationsHistory'")
            .ToListAsync(TestCancellationToken);

        if (tableNames.Count > 0)
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                "SET session_replication_role = 'replica';",
                TestCancellationToken);

            try
            {
                foreach (var tableName in tableNames)
                {
#pragma warning disable EF1002
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        $"TRUNCATE TABLE \"{tableName}\" CASCADE;",
                        TestCancellationToken);
#pragma warning restore EF1002
                }
            }
            finally
            {
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "SET session_replication_role = 'origin';",
                    TestCancellationToken);
            }
        }
    }

    [Fact]
    public async Task PdfUpload_CreatesDocument_WithPendingProcessingStatus()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = ServiceProvider.GetRequiredService<IUserRepository>();
        var gameRepository = ServiceProvider.GetRequiredService<IGameRepository>();
        var pdfRepository = ServiceProvider.GetRequiredService<IPdfDocumentRepository>();

        var user = CreateTestUser("uploader@meepleai.dev", "Document Uploader");
        await userRepository.AddAsync(user, TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Gloomhaven"),
            playerCount: new PlayerCount(1, 4),
            playTime: new PlayTime(90, 150)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var pdfDocument = new PdfDocument(
            Guid.NewGuid(),
            game.Id,
            new FileName("gloomhaven-rules.pdf"),
            "/uploads/gloomhaven-rules.pdf",
            new FileSize(5 * 1024 * 1024),
            user.Id
        );

        await pdfRepository.AddAsync(pdfDocument, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var loadedDocument = await pdfRepository.GetByIdAsync(pdfDocument.Id, TestCancellationToken);
        loadedDocument.Should().NotBeNull();
        loadedDocument!.GameId.Should().Be(game.Id);
        loadedDocument.UploadedByUserId.Should().Be(user.Id);
        loadedDocument.ProcessingStatus.Should().Be("pending");
        loadedDocument.FileName.Value.Should().Be("gloomhaven-rules.pdf");
    }

    [Fact]
    public async Task PdfProcessingWorkflow_FromUploadToVectorEmbedding()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = ServiceProvider.GetRequiredService<IUserRepository>();
        var gameRepository = ServiceProvider.GetRequiredService<IGameRepository>();
        var pdfRepository = ServiceProvider.GetRequiredService<IPdfDocumentRepository>();
        var vectorRepository = ServiceProvider.GetRequiredService<IVectorDocumentRepository>();

        var user = CreateTestUser("processor@meepleai.dev", "PDF Processor");
        await userRepository.AddAsync(user, TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Scythe"),
            playerCount: new PlayerCount(1, 5),
            playTime: new PlayTime(90, 120)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Complete workflow
        var pdfDocument = new PdfDocument(
            Guid.NewGuid(),
            game.Id,
            new FileName("scythe-rules.pdf"),
            "/uploads/scythe-rules.pdf",
            new FileSize(4 * 1024 * 1024),
            user.Id
        );
        await pdfRepository.AddAsync(pdfDocument, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Reload to avoid tracking conflicts
        var reloadedPdf = await pdfRepository.GetByIdAsync(pdfDocument.Id, TestCancellationToken);
        reloadedPdf!.MarkAsProcessing();
        await pdfRepository.UpdateAsync(reloadedPdf, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var reloadedPdf2 = await pdfRepository.GetByIdAsync(pdfDocument.Id, TestCancellationToken);
        reloadedPdf2!.MarkAsCompleted(pageCount: 32);
        await pdfRepository.UpdateAsync(reloadedPdf2, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var vectorDoc = new VectorDocument(
            Guid.NewGuid(),
            game.Id,
            pdfDocument.Id,
            language: "en",
            totalChunks: 15
        );
        vectorDoc.UpdateMetadata("{\"page\": 12, \"source\": \"scythe-rules.pdf\", \"quality\": 0.95}");
        await vectorRepository.AddAsync(vectorDoc, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var loadedDocument = await pdfRepository.GetByIdAsync(pdfDocument.Id, TestCancellationToken);
        loadedDocument.Should().NotBeNull();
        loadedDocument!.ProcessingStatus.Should().Be("completed");
        loadedDocument.PageCount.Should().Be(32);
        loadedDocument.ProcessedAt.Should().NotBeNull();

        var loadedVectorDoc = await vectorRepository.GetByIdAsync(vectorDoc.Id, TestCancellationToken);
        loadedVectorDoc.Should().NotBeNull();
        loadedVectorDoc!.GameId.Should().Be(game.Id);
        loadedVectorDoc.PdfDocumentId.Should().Be(pdfDocument.Id);
        loadedVectorDoc.TotalChunks.Should().Be(15);
    }

    [Fact]
    public async Task VectorDocuments_EnableGameSpecificRAG_ForChatThreads()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = ServiceProvider.GetRequiredService<IUserRepository>();
        var gameRepository = ServiceProvider.GetRequiredService<IGameRepository>();
        var pdfRepository = ServiceProvider.GetRequiredService<IPdfDocumentRepository>();
        var vectorRepository = ServiceProvider.GetRequiredService<IVectorDocumentRepository>();
        var chatThreadRepository = ServiceProvider.GetRequiredService<IChatThreadRepository>();

        var user = CreateTestUser("raguser@meepleai.dev", "RAG User");
        await userRepository.AddAsync(user, TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Pandemic"),
            playerCount: new PlayerCount(2, 4),
            playTime: new PlayTime(45, 60)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);

        var pdfDocument = new PdfDocument(
            Guid.NewGuid(),
            game.Id,
            new FileName("pandemic-rules.pdf"),
            "/uploads/pandemic-rules.pdf",
            new FileSize(3 * 1024 * 1024),
            user.Id
        );
        pdfDocument.MarkAsProcessing();
        pdfDocument.MarkAsCompleted(24);
        await pdfRepository.AddAsync(pdfDocument, TestCancellationToken);

        var vectorDoc = new VectorDocument(
            Guid.NewGuid(),
            game.Id,
            pdfDocument.Id,
            language: "en",
            totalChunks: 12
        );
        await vectorRepository.AddAsync(vectorDoc, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Clear tracker to avoid conflicts
        _dbContext.ChangeTracker.Clear();

        // Reload and update metadata
        var loadedVectorDoc = await vectorRepository.GetByIdAsync(vectorDoc.Id, TestCancellationToken);
        loadedVectorDoc!.UpdateMetadata("{\"page\": 8, \"source\": \"pandemic-rules.pdf\", \"topic\": \"outbreak\"}");
        await vectorRepository.UpdateAsync(loadedVectorDoc, TestCancellationToken);
        // Note: UpdateAsync already calls SaveChangesAsync internally, removed duplicate

        // Act - Create chat thread using RAG context
        var chatThread = new ChatThread(
            Guid.NewGuid(),
            user.Id,
            game.Id,
            "Pandemic Outbreak Questions"
        );
        chatThread.AddUserMessage("What happens during an outbreak?");
        chatThread.AddAssistantMessage("Based on the rules (page 8): Outbreak occurs when city has 3+ disease cubes...");

        await chatThreadRepository.AddAsync(chatThread, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Clear tracker before final assertion to force fresh query
        _dbContext.ChangeTracker.Clear();

        // Assert
        var loadedThread = await chatThreadRepository.GetByIdAsync(chatThread.Id, TestCancellationToken);
        loadedThread.Should().NotBeNull();
        loadedThread!.GameId.Should().Be(game.Id);
        loadedThread.MessageCount.Should().Be(2);

        var gameVectors = await vectorRepository.GetByGameIdAsync(game.Id, TestCancellationToken);
        gameVectors.Should().ContainSingle();
        gameVectors[0].Metadata.Should().Contain("outbreak");
    }

    [Fact]
    public async Task MultipleUsers_CanUploadDocuments_ForSameGame()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = ServiceProvider.GetRequiredService<IUserRepository>();
        var gameRepository = ServiceProvider.GetRequiredService<IGameRepository>();
        var pdfRepository = ServiceProvider.GetRequiredService<IPdfDocumentRepository>();

        var user1 = CreateTestUser("user1@meepleai.dev", "User One");
        var user2 = CreateTestUser("user2@meepleai.dev", "User Two");
        await userRepository.AddAsync(user1, TestCancellationToken);
        await userRepository.AddAsync(user2, TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Terraforming Mars"),
            playerCount: new PlayerCount(1, 5),
            playTime: new PlayTime(120, 180)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Both users upload documents
        var pdf1 = new PdfDocument(
            Guid.NewGuid(),
            game.Id,
            new FileName("tm-base-rules.pdf"),
            "/uploads/tm-base.pdf",
            new FileSize(6 * 1024 * 1024),
            user1.Id
        );
        pdf1.MarkAsProcessing();
        pdf1.MarkAsCompleted(40);

        var pdf2 = new PdfDocument(
            Guid.NewGuid(),
            game.Id,
            new FileName("tm-expansions.pdf"),
            "/uploads/tm-exp.pdf",
            new FileSize(8 * 1024 * 1024),
            user2.Id
        );
        pdf2.MarkAsProcessing();
        pdf2.MarkAsCompleted(56);

        await pdfRepository.AddAsync(pdf1, TestCancellationToken);
        await pdfRepository.AddAsync(pdf2, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Assert - Verify both documents exist
        var user1Doc = await pdfRepository.GetByIdAsync(pdf1.Id, TestCancellationToken);
        user1Doc.Should().NotBeNull();
        user1Doc!.FileName.Value.Should().Be("tm-base-rules.pdf");
        user1Doc.PageCount.Should().Be(40);
        user1Doc.UploadedByUserId.Should().Be(user1.Id);

        var user2Doc = await pdfRepository.GetByIdAsync(pdf2.Id, TestCancellationToken);
        user2Doc.Should().NotBeNull();
        user2Doc!.FileName.Value.Should().Be("tm-expansions.pdf");
        user2Doc.PageCount.Should().Be(56);
        user2Doc.UploadedByUserId.Should().Be(user2.Id);
    }

    private static User CreateTestUser(string email, string displayName)
    {
        return new User(
            Guid.NewGuid(),
            new Email(email),
            displayName,
            PasswordHash.Create("SecurePass123!"),
            Role.User
        );
    }
}