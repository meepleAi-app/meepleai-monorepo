using System;
using System.Collections.Generic;
using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public class RuleSpecServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecService _service;
    private readonly Mock<IAiResponseCacheService> _cacheMock = new();

    public RuleSpecServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _cacheMock
            .Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheMock
            .Setup(x => x.InvalidateEndpointAsync(It.IsAny<string>(), It.IsAny<AiCacheEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new RuleSpecService(_dbContext, _cacheMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetOrCreateDemoAsync_WhenNoData_CreatesDemoSpec()
    {
        var gameId = "chess";

        var result = await _service.GetOrCreateDemoAsync(gameId);

        Assert.Equal(gameId, result.gameId);
        Assert.Equal("v0-demo", result.version);
        Assert.Equal(2, result.rules.Count);
        Assert.Collection(
            result.rules,
            atom =>
            {
                Assert.Equal("r1", atom.id);
                Assert.Equal("Two players.", atom.text);
                Assert.Equal("Basics", atom.section);
                Assert.Equal("1", atom.page);
                Assert.Equal("1", atom.line);
            },
            atom =>
            {
                Assert.Equal("r2", atom.id);
                Assert.Equal("White moves first.", atom.text);
                Assert.Equal("Basics", atom.section);
                Assert.Equal("1", atom.page);
                Assert.Equal("2", atom.line);
            });

        var savedGame = await _dbContext.Games.FirstOrDefaultAsync(g => g.Id == gameId);
        Assert.NotNull(savedGame);
        var savedSpec = await _dbContext.RuleSpecs.Include(s => s.Atoms).SingleAsync();
        Assert.Equal(gameId, savedSpec.GameId);
        Assert.Equal("v0-demo", savedSpec.Version);
        Assert.Equal(2, savedSpec.Atoms.Count);
    }

    [Fact]
    public async Task GetOrCreateDemoAsync_WhenSpecExists_ReturnsLatestSpec()
    {
        var game = new GameEntity
        {
            Id = "existing-game",
            Name = "Existing Game",
            CreatedAt = DateTime.UtcNow
        };
        var spec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        spec.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = spec,
            Key = "a1",
            Text = "Existing rule.",
            Section = "Intro",
            SortOrder = 1
        });

        _dbContext.Games.Add(game);
        _dbContext.RuleSpecs.Add(spec);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetOrCreateDemoAsync(game.Id);

        Assert.Equal("v1", result.version);
        Assert.Single(result.rules);
        Assert.Equal("Existing rule.", result.rules[0].text);
        Assert.Equal(1, await _dbContext.RuleSpecs.CountAsync());
    }

    [Fact]
    public async Task UpdateRuleSpecAsync_CreatesNewVersionWithAtoms()
    {
        var game = new GameEntity
        {
            Id = "game-1",
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "author-1",
            Email = "author@example.com",
            DisplayName = "Author",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var model = new RuleSpec(
            game.Id,
            "v2",
            DateTime.UtcNow,
            new[]
            {
                new RuleAtom("a1", "First rule", "Setup", "1", "1"),
                new RuleAtom("a2", "Second rule", "Setup", "1", "2")
            });

        var result = await _service.UpdateRuleSpecAsync(game.Id, model, user.Id);

        Assert.Equal(game.Id, result.gameId);
        Assert.Equal("v2", result.version);
        Assert.Equal(2, result.rules.Count);

        var entity = await _dbContext.RuleSpecs.Include(r => r.Atoms)
            .SingleAsync(r => r.GameId == game.Id && r.Version == "v2");
        Assert.Equal(2, entity.Atoms.Count);
        Assert.Contains(entity.Atoms, a => a.Key == "a1" && a.Text == "First rule");
        Assert.Contains(entity.Atoms, a => a.Key == "a2" && a.Text == "Second rule");

        _cacheMock.Verify(x => x.InvalidateGameAsync(game.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(user.Id, entity.CreatedByUserId);

        var history = await _service.GetVersionHistoryAsync(game.Id);
        Assert.Equal("Author", history.Versions.First().CreatedBy);
    }

    [Fact]
    public async Task UpdateRuleSpecAsync_WhenVersionMissing_GeneratesSequentialVersionAndTracksAuthor()
    {
        var game = new GameEntity
        {
            Id = "game-2",
            Name = "Sequential Game",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "author-2",
            Email = "author2@example.com",
            DisplayName = "Second Author",
            PasswordHash = "hash",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var existingSpec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedByUserId = user.Id
        };
        existingSpec.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = existingSpec,
            Key = "ex1",
            Text = "Existing",
            Section = "Intro",
            SortOrder = 1
        });

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.RuleSpecs.Add(existingSpec);
        await _dbContext.SaveChangesAsync();

        var model = new RuleSpec(
            game.Id,
            string.Empty,
            DateTime.UtcNow,
            new[]
            {
                new RuleAtom("b1", "Generated", "Setup", "1", "1"),
            });

        var result = await _service.UpdateRuleSpecAsync(game.Id, model, user.Id);

        Assert.Equal(game.Id, result.gameId);
        Assert.Equal("v2", result.version);

        var saved = await _dbContext.RuleSpecs
            .SingleAsync(r => r.GameId == game.Id && r.Version == "v2");
        Assert.Equal(user.Id, saved.CreatedByUserId);

        var history = await _service.GetVersionHistoryAsync(game.Id);
        Assert.Equal(2, history.TotalVersions);
        Assert.Equal("Second Author", history.Versions.First().CreatedBy);
        Assert.Equal("v2", history.Versions.First().Version);
    }

    [Fact]
    public async Task UpdateRuleSpecAsync_WhenVersionAlreadyExists_Throws()
    {
        var game = new GameEntity
        {
            Id = "game-duplicate",
            Name = "Duplicate Game",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "author-dup",
            Email = "dup@example.com",
            DisplayName = "Duplicate Author",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        var spec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            CreatedByUserId = user.Id
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.RuleSpecs.Add(spec);
        await _dbContext.SaveChangesAsync();

        var model = new RuleSpec(
            game.Id,
            "v1",
            DateTime.UtcNow,
            new[]
            {
                new RuleAtom("dup", "Duplicate", "Intro", "1", "1"),
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateRuleSpecAsync(game.Id, model, user.Id));
    }

    [Fact]
    public async Task GetVersionHistoryAsync_ReturnsVersionsOrderedByDate()
    {
        var game = new GameEntity
        {
            Id = "history-game",
            Name = "History Game",
            CreatedAt = DateTime.UtcNow
        };
        var user = new UserEntity
        {
            Id = "user-1",
            Email = "user@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        var older = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedByUserId = user.Id,
            CreatedBy = user
        };
        older.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = older,
            Key = "r1",
            Text = "Old rule",
            Section = "Basics",
            SortOrder = 1
        });

        var newer = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v2",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = user.Id,
            CreatedBy = user
        };
        newer.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = newer,
            Key = "r2",
            Text = "New rule",
            Section = "Basics",
            SortOrder = 1
        });

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.RuleSpecs.AddRange(older, newer);
        await _dbContext.SaveChangesAsync();

        var history = await _service.GetVersionHistoryAsync(game.Id);

        Assert.Equal(game.Id, history.GameId);
        Assert.Equal(2, history.TotalVersions);
        Assert.Collection(
            history.Versions,
            version =>
            {
                Assert.Equal("v2", version.Version);
                Assert.Equal(1, version.RuleCount);
                Assert.Equal("Test User", version.CreatedBy);
            },
            version =>
            {
                Assert.Equal("v1", version.Version);
                Assert.Equal(1, version.RuleCount);
                Assert.Equal("Test User", version.CreatedBy);
            });
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdfAsync_WithAtomicRules_ParsesStructuredRules()
    {
        var game = new GameEntity
        {
            Id = "game-pdf",
            Name = "PDF Game",
            CreatedAt = DateTime.UtcNow
        };
        var user = new UserEntity
        {
            Id = "user-atom",
            Email = "atom@example.com",
    public async Task GenerateRuleSpecFromPdfAsync_UsesAtomicRulesWhenPresent()
    {
        var user = new UserEntity
        {
            Id = "user-atomic",
            Email = "atomic@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        var processedAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-1",
            GameId = game.Id,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1234,
            UploadedByUserId = user.Id,
            UploadedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessingStatus = "completed",
            ProcessedAt = processedAt,
            AtomicRules = JsonSerializer.Serialize(new[]
            {
                "[Table on page 3] Setup: Place pieces; Count: 16",
                "Victory condition: Highest score wins"
            }),
            ExtractedText = "Fallback paragraph should not be used"
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GenerateRuleSpecFromPdfAsync(pdf.Id);

        Assert.Equal(game.Id, result.gameId);
        Assert.Equal($"pdf-{processedAt:yyyyMMddHHmmss}", result.version);
        Assert.Equal(2, result.rules.Count);
        Assert.Equal("atom-1", result.rules[0].id);
        Assert.Equal("Table", result.rules[0].section);
        Assert.Equal("3", result.rules[0].page);
        Assert.Equal("Setup: Place pieces; Count: 16", result.rules[0].text);
        Assert.Equal("Victory condition: Highest score wins", result.rules[1].text);
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdfAsync_WhenAtomicRulesMissing_UsesExtractedText()
    {
        var game = new GameEntity
        {
            Id = "game-text",
            Name = "Text Game",
            CreatedAt = DateTime.UtcNow
        };
        var game = new GameEntity
        {
            Id = "game-atomic",
            Name = "Atomic Game",
            CreatedAt = DateTime.UtcNow
        };

        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-atomic",
            GameId = game.Id,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = user.Id,
            UploadedAt = DateTime.UtcNow,
            AtomicRules = JsonSerializer.Serialize(new[]
            {
                "[Table on page 3] Setup: Each player draws five cards"
            })
        };

        _dbContext.Users.Add(user);
        _dbContext.Games.Add(game);
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var spec = await _service.GenerateRuleSpecFromPdfAsync(pdf.Id);

        Assert.Equal(game.Id, spec.gameId);
        Assert.StartsWith("ingest-", spec.version, StringComparison.Ordinal);
        Assert.Single(spec.rules);
        Assert.Equal("Setup: Each player draws five cards", spec.rules[0].text);
        Assert.Equal("3", spec.rules[0].page);
        Assert.Equal("r1", spec.rules[0].id);
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdfAsync_FallsBackToExtractedText()
    {
        var user = new UserEntity
        {
            Id = "user-text",
            Email = "text@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        var game = new GameEntity
        {
            Id = "game-text",
            Name = "Text Game",
            CreatedAt = DateTime.UtcNow
        };

        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-2",
            GameId = game.Id,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 4321,
            UploadedByUserId = user.Id,
            UploadedAt = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStatus = "completed",
            ExtractedText = "Rule one describes setup.\n\nRule two explains gameplay.\nRule three covers scoring."
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GenerateRuleSpecFromPdfAsync(pdf.Id);

        Assert.Equal(game.Id, result.gameId);
        Assert.True(result.rules.Count >= 2);
        Assert.Contains(result.rules, atom => atom.text.Contains("Rule one", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.rules, atom => atom.text.Contains("Rule two", StringComparison.OrdinalIgnoreCase));
        Assert.All(result.rules, atom => Assert.Null(atom.section));
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdfAsync_WhenPdfMissing_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GenerateRuleSpecFromPdfAsync("missing"));
            Id = "pdf-text",
            GameId = game.Id,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = user.Id,
            UploadedAt = DateTime.UtcNow,
            ExtractedText = "Rule one applies.\nRule two applies."
        };

        _dbContext.Users.Add(user);
        _dbContext.Games.Add(game);
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var spec = await _service.GenerateRuleSpecFromPdfAsync(pdf.Id);

        Assert.Equal(2, spec.rules.Count);
        Assert.Equal("Rule one applies.", spec.rules[0].text);
        Assert.Equal("Rule two applies.", spec.rules[1].text);
        Assert.All(spec.rules, atom => Assert.Null(atom.page));
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdfAsync_ThrowsWhenNoPdf()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GenerateRuleSpecFromPdfAsync("missing"));
    }
}
