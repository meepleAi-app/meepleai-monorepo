using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// EDIT-06: Tests for version timeline functionality
/// </summary>
public class RuleSpecServiceTimelineTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly MeepleAiDbContext _context;
    private readonly Mock<IAiResponseCacheService> _cacheMock;
    private readonly RuleSpecService _service;

    public RuleSpecServiceTimelineTests(ITestOutputHelper output)
    {
        _output = output;
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new MeepleAiDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        _cacheMock = new Mock<IAiResponseCacheService>();
        _service = new RuleSpecService(_context, _cacheMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create test game
        var game = new GameEntity
        {
            Id = "test-game",
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        // Create test users
        var user1 = new UserEntity
        {
            Id = "user1",
            Email = "user1@test.com",
            DisplayName = "User One",
            PasswordHash = "hash1",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var user2 = new UserEntity
        {
            Id = "user2",
            Email = "user2@test.com",
            DisplayName = "User Two",
            PasswordHash = "hash2",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(user1, user2);

        // Create version chain: v1 -> v2 -> v3
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var v1 = new RuleSpecEntity
        {
            Id = Guid.NewGuid(),
            GameId = "test-game",
            Version = "v1",
            CreatedAt = baseTime,
            CreatedByUserId = "user1",
            ParentVersionId = null
        };
        v1.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = v1,
            Key = "r1",
            Text = "Rule 1",
            Section = "Setup",
            SortOrder = 1
        });

        var v2 = new RuleSpecEntity
        {
            Id = Guid.NewGuid(),
            GameId = "test-game",
            Version = "v2",
            CreatedAt = baseTime.AddDays(1),
            CreatedByUserId = "user2",
            ParentVersionId = v1.Id
        };
        v2.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = v2,
            Key = "r1",
            Text = "Rule 1 updated",
            Section = "Setup",
            SortOrder = 1
        });
        v2.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = v2,
            Key = "r2",
            Text = "Rule 2",
            Section = "Gameplay",
            SortOrder = 2
        });

        var v3 = new RuleSpecEntity
        {
            Id = Guid.NewGuid(),
            GameId = "test-game",
            Version = "v3",
            CreatedAt = baseTime.AddDays(2),
            CreatedByUserId = "user1",
            ParentVersionId = v2.Id,
            MergedFromVersionIds = $"{v1.Id},{v2.Id}"
        };
        v3.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = v3,
            Key = "r1",
            Text = "Rule 1 final",
            Section = "Setup",
            SortOrder = 1
        });

        _context.RuleSpecs.AddRange(v1, v2, v3);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetVersionTimelineAsync_ReturnsAllVersions()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game");

        // Assert
        result.Should().NotBeNull();
        Assert.Equal("test-game", result.GameId);
        Assert.Equal(3, result.TotalVersions);
        Assert.Equal(3, result.Versions.Count);
    }

    [Fact]
    public async Task GetVersionTimelineAsync_VersionsOrderedByCreatedAt()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game");

        // Assert
        Assert.Equal("v1", result.Versions[0].Version);
        Assert.Equal("v2", result.Versions[1].Version);
        Assert.Equal("v3", result.Versions[2].Version);
    }

    [Fact]
    public async Task GetVersionTimelineAsync_MarksLatestVersionAsCurrent()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game");

        // Assert
        var v3 = result.Versions.First(v => v.Version == "v3");
        Assert.True(v3.IsCurrentVersion);

        var v1 = result.Versions.First(v => v.Version == "v1");
        Assert.False(v1.IsCurrentVersion);
    }

    [Fact]
    public async Task GetVersionTimelineAsync_PopulatesParentVersions()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game");

        // Assert
        var v1 = result.Versions.First(v => v.Version == "v1");
        v1.ParentVersionId.Should().BeNull();
        v1.ParentVersion.Should().BeNull();

        var v2 = result.Versions.First(v => v.Version == "v2");
        v2.ParentVersionId.Should().NotBeNull();
        Assert.Equal("v1", v2.ParentVersion);

        var v3 = result.Versions.First(v => v.Version == "v3");
        v3.ParentVersionId.Should().NotBeNull();
        Assert.Equal("v2", v3.ParentVersion);
    }

    [Fact]
    public async Task GetVersionTimelineAsync_PopulatesMergedFromVersions()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game");

        // Assert
        var v3 = result.Versions.First(v => v.Version == "v3");
        Assert.Equal(2, v3.MergedFromVersionIds.Count);
        Assert.Equal(2, v3.MergedFromVersions.Count);
        Assert.Contains("v1", v3.MergedFromVersions);
        Assert.Contains("v2", v3.MergedFromVersions);
    }

    [Fact]
    public async Task GetVersionTimelineAsync_ExtractsAuthors()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game");

        // Assert
        Assert.Equal(2, result.Authors.Count);
        Assert.Contains("User One", result.Authors);
        Assert.Contains("User Two", result.Authors);
    }

    [Fact]
    public async Task GetVersionTimelineAsync_FiltersByStartDate()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var filters = new VersionTimelineFilters
        {
            StartDate = baseTime.AddDays(1)
        };

        // Act
        var result = await _service.GetVersionTimelineAsync("test-game", filters);

        // Assert
        Assert.Equal(2, result.TotalVersions);
        Assert.DoesNotContain(result.Versions, v => v.Version == "v1");
        Assert.Contains(result.Versions, v => v.Version == "v2");
        Assert.Contains(result.Versions, v => v.Version == "v3");
    }

    [Fact]
    public async Task GetVersionTimelineAsync_FiltersByEndDate()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var filters = new VersionTimelineFilters
        {
            EndDate = baseTime.AddDays(1)
        };

        // Act
        var result = await _service.GetVersionTimelineAsync("test-game", filters);

        // Assert
        Assert.Equal(2, result.TotalVersions);
        Assert.Contains(result.Versions, v => v.Version == "v1");
        Assert.Contains(result.Versions, v => v.Version == "v2");
        Assert.DoesNotContain(result.Versions, v => v.Version == "v3");
    }

    [Fact]
    public async Task GetVersionTimelineAsync_FiltersByAuthorDisplayName()
    {
        // Arrange
        var filters = new VersionTimelineFilters
        {
            Author = "User One"
        };

        // Act
        var result = await _service.GetVersionTimelineAsync("test-game", filters);

        // Assert
        Assert.Equal(2, result.TotalVersions);
        Assert.Contains(result.Versions, v => v.Version == "v1");
        Assert.DoesNotContain(result.Versions, v => v.Version == "v2");
        Assert.Contains(result.Versions, v => v.Version == "v3");
    }

    [Fact]
    public async Task GetVersionTimelineAsync_FiltersByAuthorEmail()
    {
        // Arrange
        var filters = new VersionTimelineFilters
        {
            Author = "user2@test.com"
        };

        // Act
        var result = await _service.GetVersionTimelineAsync("test-game", filters);

        // Assert
        Assert.Equal(1, result.TotalVersions);
        Assert.Contains(result.Versions, v => v.Version == "v2");
    }

    [Fact]
    public async Task GetVersionTimelineAsync_FiltersBySearchQuery()
    {
        // Arrange
        var filters = new VersionTimelineFilters
        {
            SearchQuery = "v2"
        };

        // Act
        var result = await _service.GetVersionTimelineAsync("test-game", filters);

        // Assert
        Assert.Equal(1, result.TotalVersions);
        Assert.Contains(result.Versions, v => v.Version == "v2");
    }

    [Fact]
    public async Task GetVersionTimelineAsync_AppliesMultipleFilters()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var filters = new VersionTimelineFilters
        {
            StartDate = baseTime,
            EndDate = baseTime.AddDays(10),
            Author = "User One"
        };

        // Act
        var result = await _service.GetVersionTimelineAsync("test-game", filters);

        // Assert
        Assert.Equal(2, result.TotalVersions);
        Assert.Contains(result.Versions, v => v.Version == "v1");
        Assert.Contains(result.Versions, v => v.Version == "v3");
    }

    [Fact]
    public async Task GetVersionTimelineAsync_ReturnsEmptyForNonExistentGame()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("non-existent-game");

        // Assert
        result.Should().NotBeNull();
        Assert.Equal("non-existent-game", result.GameId);
        Assert.Equal(0, result.TotalVersions);
        result.Versions.Should().BeEmpty();
        result.Authors.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVersionTimelineAsync_HandlesNullFilters()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game", null);

        // Assert
        result.Should().NotBeNull();
        Assert.Equal(3, result.TotalVersions);
    }

    [Fact]
    public async Task GetVersionTimelineAsync_PopulatesChangeCount()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game");

        // Assert
        var v1 = result.Versions.First(v => v.Version == "v1");
        Assert.Equal(1, v1.ChangeCount);

        var v2 = result.Versions.First(v => v.Version == "v2");
        Assert.Equal(2, v2.ChangeCount);

        var v3 = result.Versions.First(v => v.Version == "v3");
        Assert.Equal(1, v3.ChangeCount);
    }

    [Fact]
    public async Task GetVersionTimelineAsync_PopulatesMetadata()
    {
        // Act
        var result = await _service.GetVersionTimelineAsync("test-game");

        // Assert
        foreach (var version in result.Versions)
        {
            Assert.NotEqual(Guid.Empty, version.Id);
            version.Version.Should().NotBeEmpty();
            version.Title.Should().NotBeEmpty();
            version.Description.Should().NotBeEmpty();
            version.Author.Should().NotBeEmpty();
            Assert.NotEqual(DateTime.MinValue, version.CreatedAt);
        }
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}
