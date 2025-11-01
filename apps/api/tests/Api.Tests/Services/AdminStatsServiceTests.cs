using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// ADMIN-02: Unit tests for AdminStatsService
/// Tests aggregation queries, caching, export functionality with in-memory SQLite
/// </summary>
public class AdminStatsServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly Mock<ILogger<AdminStatsService>> _loggerMock;
    private readonly AdminStatsService _service;
    private readonly ServiceProvider _serviceProvider;

    #pragma warning disable EXTEXP0018 // HybridCache is experimental
    public AdminStatsServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Setup in-memory SQLite
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Setup HybridCache with service provider for in-memory caching
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddHybridCache();
        _serviceProvider = services.BuildServiceProvider();
        _cache = _serviceProvider.GetRequiredService<HybridCache>();

        _loggerMock = new Mock<ILogger<AdminStatsService>>();
        _service = new AdminStatsService(_dbContext, _cache, _loggerMock.Object);
    }
    #pragma warning restore EXTEXP0018

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsCorrectMetrics()
    {
        // Arrange
        await SeedTestDataAsync();
        var queryParams = new AnalyticsQueryParams(Days: 30);

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(3);
        result.Metrics.ActiveSessions.Should().Be(2); // Only non-revoked sessions
        result.Metrics.TotalPdfDocuments.Should().Be(5);
        result.Metrics.TotalChatMessages.Should().Be(10);
        result.Metrics.TotalRagRequests > 0.Should().BeTrue();
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ActiveSessions_OnlyCountsNonRevoked()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = "user1",
            Email = "test@test.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = now
        };
        _dbContext.Users.Add(user);

        // Add 2 active sessions and 1 revoked
        _dbContext.UserSessions.AddRange(
            new UserSessionEntity
            {
                Id = "sess1",
                UserId = "user1",
                User = user,
                TokenHash = "hash1",
                CreatedAt = now,
                ExpiresAt = now.AddDays(1),
                RevokedAt = null
            },
            new UserSessionEntity
            {
                Id = "sess2",
                UserId = "user1",
                User = user,
                TokenHash = "hash2",
                CreatedAt = now,
                ExpiresAt = now.AddDays(1),
                RevokedAt = null
            },
            new UserSessionEntity
            {
                Id = "sess3",
                UserId = "user1",
                User = user,
                TokenHash = "hash3",
                CreatedAt = now,
                ExpiresAt = now.AddDays(1),
                RevokedAt = now.AddHours(-1) // Revoked
            }
        );
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(Days: 30);

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert
        result.Metrics.ActiveSessions.Should().Be(2);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_TimeSeriesTrends_FillsMissingDates()
    {
        // Arrange
        var now = DateTime.UtcNow.Date;
        var user1 = new UserEntity
        {
            Id = "user1",
            Email = "test1@test.com",
            DisplayName = "User 1",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = now.AddDays(-5)
        };
        var user2 = new UserEntity
        {
            Id = "user2",
            Email = "test2@test.com",
            DisplayName = "User 2",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = now.AddDays(-3)
        };
        _dbContext.Users.AddRange(user1, user2);
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-7),
            ToDate: now,
            Days: 7
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert
        result.UserTrend.Count.Should().Be(8); // 7 days + today (8 days total)

        // Check that missing dates have zero counts
        var dayMinus6 = result.UserTrend.FirstOrDefault(d => d.Date.Date == now.AddDays(-6).Date);
        dayMinus6.Should().NotBeNull();
        dayMinus6.Count.Should().Be(0);

        // Check that dates with data have correct counts
        var dayMinus5 = result.UserTrend.FirstOrDefault(d => d.Date.Date == now.AddDays(-5).Date);
        dayMinus5.Should().NotBeNull();
        dayMinus5.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_GameFilter_FiltersCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Add user first (required for PDF upload)
        var user = new UserEntity
        {
            Id = "user1",
            Email = "user@test.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = now
        };
        _dbContext.Users.Add(user);

        var game1 = new GameEntity { Id = "game1", Name = "Chess", CreatedAt = now };
        var game2 = new GameEntity { Id = "game2", Name = "Checkers", CreatedAt = now };
        _dbContext.Games.AddRange(game1, game2);

        // Add PDFs for different games
        _dbContext.PdfDocuments.AddRange(
            new PdfDocumentEntity
            {
                Id = "pdf1",
                GameId = "game1",
                FileName = "chess.pdf",
                FilePath = "/path/chess.pdf",
                FileSizeBytes = 1000,
                PageCount = 10,
                UploadedByUserId = "user1",
                UploadedAt = now
            },
            new PdfDocumentEntity
            {
                Id = "pdf2",
                GameId = "game2",
                FileName = "checkers.pdf",
                FilePath = "/path/checkers.pdf",
                FileSizeBytes = 2000,
                PageCount = 20,
                UploadedByUserId = "user1",
                UploadedAt = now
            }
        );
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(Days: 30, GameId: "game1");

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert
        var todayPdfs = result.PdfUploadTrend.FirstOrDefault(d => d.Date.Date == now.Date);
        todayPdfs.Should().NotBeNull();
        todayPdfs.Count.Should().Be(1); // Only game1 PDF
    }

    [Fact]
    public async Task GetDashboardStatsAsync_CachesResults()
    {
        // Arrange
        await SeedTestDataAsync();
        var queryParams = new AnalyticsQueryParams(Days: 30);

        // Act - First call (cache miss)
        var result1 = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Add more data after first call
        _dbContext.Users.Add(new UserEntity
        {
            Id = "user-new",
            Email = "new@test.com",
            DisplayName = "New User",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act - Second call (should return cached data)
        var result2 = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert - Should return same count (cached)
        result2.Metrics.TotalUsers.Should().Be(result1.Metrics.TotalUsers);
        result2.Metrics.TotalUsers.Should().Be(3); // Still 3, not 4
    }

    [Fact]
    public async Task ExportDashboardDataAsync_CSV_GeneratesCorrectFormat()
    {
        // Arrange
        await SeedTestDataAsync();
        var request = new ExportDataRequest(Format: "csv", FromDate: DateTime.UtcNow.AddDays(-7), ToDate: DateTime.UtcNow);

        // Act
        var result = await _service.ExportDashboardDataAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        Value", result.Should().Contain("Metric);
        ", result.Should().Contain("Total Users);
        ", result.Should().Contain("Active Sessions);
        Count,Average", result.Should().Contain("User Registrations - Date);
    }

    [Fact]
    public async Task ExportDashboardDataAsync_JSON_GeneratesCorrectFormat()
    {
        // Arrange
        await SeedTestDataAsync();
        var request = new ExportDataRequest(Format: "json", FromDate: DateTime.UtcNow.AddDays(-7), ToDate: DateTime.UtcNow);

        // Act
        var result = await _service.ExportDashboardDataAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("\"metrics\"");
        result.Should().Contain("\"userTrend\"");
        result.Should().Contain("\"totalUsers\"");
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<DashboardStatsDto>(result);
        deserialized.Should().NotBeNull();
    }

    [Fact]
    public async Task ExportDashboardDataAsync_UnsupportedFormat_ThrowsArgumentException()
    {
        // Arrange
        var request = new ExportDataRequest(Format: "xml", FromDate: DateTime.UtcNow.AddDays(-7), ToDate: DateTime.UtcNow);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.ExportDashboardDataAsync(request, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetDashboardStatsAsync_AverageConfidenceScore_CalculatesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Id = "req1",
                Endpoint = "qa",
                CreatedAt = now,
                TokenCount = 100,
                Confidence = 0.8,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Id = "req2",
                Endpoint = "qa",
                CreatedAt = now,
                TokenCount = 150,
                Confidence = 0.9,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Id = "req3",
                Endpoint = "qa",
                CreatedAt = now,
                TokenCount = 200,
                Confidence = null, // No confidence
                Status = "Success"
            }
        );
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(Days: 30);

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert
        result.Metrics.AverageConfidenceScore, 2.Should().Be(0.85); // (0.8 + 0.9) / 2 = 0.85
    }

    [Fact]
    public async Task GetDashboardStatsAsync_TotalTokensUsed_SumsCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Id = "req1",
                Endpoint = "qa",
                CreatedAt = now,
                TokenCount = 100,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Id = "req2",
                Endpoint = "explain",
                CreatedAt = now,
                TokenCount = 250,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Id = "req3",
                Endpoint = "setup",
                CreatedAt = now,
                TokenCount = 150,
                Status = "Success"
            }
        );
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(Days: 30);

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert
        result.Metrics.TotalTokensUsed.Should().Be(500); // 100 + 250 + 150
    }

    [Fact]
    public async Task GetDashboardStatsAsync_RoleFilter_FiltersCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _dbContext.Users.AddRange(
            new UserEntity
            {
                Id = "admin1",
                Email = "admin1@test.com",
                DisplayName = "Admin User",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                CreatedAt = now.AddDays(-5)
            },
            new UserEntity
            {
                Id = "editor1",
                Email = "editor1@test.com",
                DisplayName = "Editor User",
                PasswordHash = "hash",
                Role = UserRole.Editor,
                CreatedAt = now.AddDays(-3)
            },
            new UserEntity
            {
                Id = "user1",
                Email = "user1@test.com",
                DisplayName = "Regular User",
                PasswordHash = "hash",
                Role = UserRole.User,
                CreatedAt = now.AddDays(-1)
            }
        );
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-7),
            ToDate: now,
            Days: 7,
            RoleFilter: "Admin"
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert
        var userTrendWithData = result.UserTrend.Where(d => d.Count > 0).ToList();
        userTrendWithData.Should().ContainSingle(); // Only 1 admin user registered
        userTrendWithData[0].Count.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_EmptyDatabase_ReturnsZeroMetrics()
    {
        // Arrange
        var queryParams = new AnalyticsQueryParams(Days: 30);

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(0);
        result.Metrics.ActiveSessions.Should().Be(0);
        result.Metrics.TotalPdfDocuments.Should().Be(0);
        result.Metrics.TotalChatMessages.Should().Be(0);
        result.Metrics.AverageConfidenceScore.Should().Be(0.0);

        // UserTrend should have 31 days (30 days + today) with zero counts
        result.UserTrend.Count.Should().Be(31);
        Assert.All(result.UserTrend, point => Assert.Equal(0, point.Count));
    }

    /// <summary>
    /// Seed database with test data for most tests
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        var now = DateTime.UtcNow;

        // Add users
        _dbContext.Users.AddRange(
            new UserEntity
            {
                Id = "user1",
                Email = "user1@test.com",
                DisplayName = "User 1",
                PasswordHash = "hash",
                Role = UserRole.User,
                CreatedAt = now.AddDays(-10)
            },
            new UserEntity
            {
                Id = "user2",
                Email = "user2@test.com",
                DisplayName = "User 2",
                PasswordHash = "hash",
                Role = UserRole.Editor,
                CreatedAt = now.AddDays(-5)
            },
            new UserEntity
            {
                Id = "admin",
                Email = "admin@test.com",
                DisplayName = "Admin",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                CreatedAt = now.AddDays(-20)
            }
        );

        // Save users first to get them loaded for sessions
        await _dbContext.SaveChangesAsync();

        // Get user references
        var user1Ref = await _dbContext.Users.FindAsync("user1");
        var user2Ref = await _dbContext.Users.FindAsync("user2");
        var adminRef = await _dbContext.Users.FindAsync("admin");

        // Add sessions with User navigation property
        _dbContext.UserSessions.AddRange(
            new UserSessionEntity
            {
                Id = "sess1",
                UserId = "user1",
                User = user1Ref!,
                TokenHash = "hash1",
                CreatedAt = now,
                ExpiresAt = now.AddDays(1),
                RevokedAt = null
            },
            new UserSessionEntity
            {
                Id = "sess2",
                UserId = "user2",
                User = user2Ref!,
                TokenHash = "hash2",
                CreatedAt = now,
                ExpiresAt = now.AddDays(1),
                RevokedAt = null
            },
            new UserSessionEntity
            {
                Id = "sess3",
                UserId = "admin",
                User = adminRef!,
                TokenHash = "hash3",
                CreatedAt = now.AddDays(-10),
                ExpiresAt = now.AddDays(-9),
                RevokedAt = null // Expired
            }
        );

        // Add games
        var game = new GameEntity { Id = "game1", Name = "Chess", CreatedAt = now };
        _dbContext.Games.Add(game);

        // Add PDFs
        _dbContext.PdfDocuments.AddRange(
            Enumerable.Range(1, 5).Select(i => new PdfDocumentEntity
            {
                Id = $"pdf{i}",
                GameId = "game1",
                FileName = $"chess-{i}.pdf",
                FilePath = $"/path/chess-{i}.pdf",
                FileSizeBytes = 1000 * i,
                PageCount = 10 * i,
                UploadedByUserId = "user1",
                UploadedAt = now.AddDays(-i)
            })
        );

        // Add agent for chat
        var agent = new AgentEntity
        {
            Id = "agent1",
            GameId = "game1",
            Name = "QA Agent",
            Kind = "qa"
        };
        _dbContext.Agents.Add(agent);

        // Add chat messages
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user1",
            GameId = "game1",
            AgentId = "agent1",
            StartedAt = now
        };
        _dbContext.Chats.Add(chat);

        _dbContext.ChatLogs.AddRange(
            Enumerable.Range(1, 10).Select(i => new ChatLogEntity
            {
                Id = Guid.NewGuid(),
                ChatId = chat.Id,
                UserId = "user1",
                Level = "info",
                Message = $"Message {i}",
                SequenceNumber = i,
                CreatedAt = now.AddMinutes(-i)
            })
        );

        // Add AI request logs
        _dbContext.AiRequestLogs.AddRange(
            Enumerable.Range(1, 15).Select(i => new AiRequestLogEntity
            {
                Id = $"req{i}",
                UserId = "user1",
                GameId = "game1",
                Endpoint = "qa",
                Query = $"Question {i}",
                TokenCount = 100 + (i * 10),
                PromptTokens = 50 + (i * 5),
                CompletionTokens = 50 + (i * 5),
                Confidence = 0.7 + (i * 0.01),
                Status = "Success",
                CreatedAt = now.AddHours(-i)
            })
        );

        await _dbContext.SaveChangesAsync();
    }
}
