using System;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public class RuleSpecServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecService _service;

    public RuleSpecServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _service = new RuleSpecService(_dbContext);
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

        _dbContext.Games.Add(game);
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

        var result = await _service.UpdateRuleSpecAsync(game.Id, model);

        Assert.Equal(game.Id, result.gameId);
        Assert.Equal("v2", result.version);
        Assert.Equal(2, result.rules.Count);

        var entity = await _dbContext.RuleSpecs.Include(r => r.Atoms)
            .SingleAsync(r => r.GameId == game.Id && r.Version == "v2");
        Assert.Equal(2, entity.Atoms.Count);
        Assert.Contains(entity.Atoms, a => a.Key == "a1" && a.Text == "First rule");
        Assert.Contains(entity.Atoms, a => a.Key == "a2" && a.Text == "Second rule");
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
