using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Comprehensive integration tests for CreateRuleComment workflow (Issue #1691).
/// Tests the complete comment creation pipeline using Testcontainers for real infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Create comments with various configurations
/// 2. Validation Errors: Invalid input handling
/// 3. @Mention Extraction: User mention resolution
/// 4. Line Number Validation: Line number constraints
/// 5. Long Comments: Maximum length handling
/// 6. Concurrent Operations: Race condition handling
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for CreateRuleCommentCommandHandler
/// Execution Time Target: <60s
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class CreateRuleCommentIntegrationTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testUserId;
    private Guid _testGameId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Prefer external Postgres if provided
        var externalConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNSTRING");
        string connectionString;
        if (!string.IsNullOrWhiteSpace(externalConn))
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(externalConn)
            {
                Database = "create_comment_test",
                SslMode = Npgsql.SslMode.Disable,
                KeepAlive = 30,
                Pooling = false
            };
            connectionString = builder.ConnectionString;
        }
        else
        {
            // Start PostgreSQL container
            _postgresContainer = new ContainerBuilder()
                .WithImage("postgres:16-alpine")
                .WithEnvironment("POSTGRES_USER", "postgres")
                .WithEnvironment("POSTGRES_PASSWORD", "postgres")
                .WithEnvironment("POSTGRES_DB", "create_comment_test")
                .WithPortBinding(5432, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
                .Build();

            await _postgresContainer.StartAsync(TestCancellationToken);

            // Setup services
            var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
            connectionString = $"Host=localhost;Port={postgresPort};Database=create_comment_test;Username=postgres;Password=postgres;Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=30;Pooling=false;";
        }

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register domain event infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register MediatR
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(CreateRuleCommentCommandHandler).Assembly));

        // Register TimeProvider
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<CreateRuleCommentCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Retry to mitigate occasional network hiccups when starting fresh container
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

        // Seed test data
        await SeedTestDataAsync();
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

    private async Task SeedTestDataAsync()
    {
        // Seed user
        _testUserId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = _testUserId,
            Email = "test@meepleai.dev",
            DisplayName = "TestUser",
            Role = "Editor",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        // Seed mentioned user
        var mentionedUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "mentioned@meepleai.dev",
            DisplayName = "MentionedUser",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(mentionedUser);

        // Seed game
        _testGameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = _testGameId,
            Name = "Test Game for Comments",
            Publisher = "Test Publisher",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 60,
            MaxPlayTimeMinutes = 90,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task ResetDatabaseAsync()
    {
        _dbContext!.RuleSpecComments.RemoveRange(_dbContext.RuleSpecComments);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }
    [Fact]
    public async Task CreateComment_WithValidData_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            42,
            "This is a test comment",
            _testUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.CommentText.Should().Be("This is a test comment");
        result.LineNumber.Should().Be(42);
        result.UserId.Should().Be(_testUserId.ToString());
        result.GameId.Should().Be(_testGameId.ToString());

        // Verify database state
        var comment = await _dbContext!.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == result.Id, TestCancellationToken);
        comment.Should().NotBeNull();
        comment!.CommentText.Should().Be("This is a test comment");
    }

    [Fact]
    public async Task CreateComment_WithoutLineNumber_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            null,
            "General comment without line number",
            _testUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.LineNumber.Should().BeNull();
        result.CommentText.Should().Be("General comment without line number");
    }
    [Fact]
    public async Task CreateComment_WithEmptyText_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            null,
            "",
            _testUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public async Task CreateComment_WithWhitespaceText_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            null,
            "   ",
            _testUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public async Task CreateComment_WithNegativeLineNumber_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            -1,
            "Test comment",
            _testUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be positive*");
    }

    [Fact]
    public async Task CreateComment_WithZeroLineNumber_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            0,
            "Test comment",
            _testUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be positive*");
    }
    [Fact]
    public async Task CreateComment_WithValidMention_ExtractsMentionedUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            null,
            "Hey @MentionedUser, check this rule",
            _testUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.MentionedUserIds.Should().NotBeEmpty();

        var mentionedUser = await _dbContext!.Users
            .FirstOrDefaultAsync(u => u.DisplayName == "MentionedUser", TestCancellationToken);
        result.MentionedUserIds.Should().Contain(mentionedUser!.Id.ToString());
    }

    [Fact]
    public async Task CreateComment_WithNonExistentMention_IgnoresMention()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            null,
            "Hey @NonExistentUser, this won't resolve",
            _testUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.MentionedUserIds.Should().BeEmpty();
    }
    [Fact]
    public async Task CreateComment_WithMaxLength_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var longText = new string('a', 2000); // Max length
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            null,
            longText,
            _testUserId
        );

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.CommentText.Should().HaveLength(2000);
    }

    [Fact]
    public async Task CreateComment_ExceedingMaxLength_ThrowsInvalidOperation()
    {
        // Arrange
        await ResetDatabaseAsync();
        var handler = _serviceProvider!.GetRequiredService<CreateRuleCommentCommandHandler>();
        var tooLongText = new string('a', 2001); // Exceeds max
        var command = new CreateRuleCommentCommand(
            _testGameId.ToString(),
            "1.0.0",
            null,
            tooLongText,
            _testUserId
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum length*");
    }
}
