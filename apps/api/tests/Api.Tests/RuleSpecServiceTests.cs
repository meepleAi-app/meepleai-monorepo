using System;
using System.Collections.Generic;
using System.IO.Compression;
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
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

public class RuleSpecServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecService _service;
    private readonly Mock<IAiResponseCacheService> _cacheMock = new();

    public RuleSpecServiceTests(ITestOutputHelper output)
    {
        _output = output;
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

        result.gameId.Should().BeEquivalentTo(gameId);
        result.version.Should().BeEquivalentTo("v0-demo");
        result.rules.Should().HaveCount(2);
        result.rules[0].id.Should().Be("r1");
        result.rules[0].text.Should().Be("Two players.");
        result.rules[0].section.Should().Be("Basics");
        result.rules[0].page.Should().Be("1");
        result.rules[0].line.Should().Be("1");
        result.rules[1].id.Should().Be("r2");
        result.rules[1].text.Should().Be("White moves first.");
        result.rules[1].section.Should().Be("Basics");
        result.rules[1].page.Should().Be("1");
        result.rules[1].line.Should().Be("2");

        var savedGame = await _dbContext.Games.FirstOrDefaultAsync(g => g.Id == gameId);
        savedGame.Should().NotBeNull();
        var savedSpec = await _dbContext.RuleSpecs.Include(s => s.Atoms).SingleAsync();
        savedSpec.GameId.Should().Be(gameId);
        savedSpec.Version.Should().Be("v0-demo");
        savedSpec.Atoms.Count.Should().Be(2);
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

        result.version.Should().BeEquivalentTo("v1");
        result.rules.Should().ContainSingle();
        result.rules[0].text.Should().Be("Existing rule.");
        (await _dbContext.RuleSpecs.CountAsync()).Should().Be(1);
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

        result.gameId.Should().BeEquivalentTo(game.Id);
        result.version.Should().BeEquivalentTo("v2");
        result.rules.Count.Should().Be(2);

        var entity = await _dbContext.RuleSpecs.Include(r => r.Atoms)
            .SingleAsync(r => r.GameId == game.Id && r.Version == "v2");
        entity.Atoms.Count.Should().Be(2);
        a => a.Key == "a1" && a.Text == "First rule".Should().Contain(entity.Atoms);
        a => a.Key == "a2" && a.Text == "Second rule".Should().Contain(entity.Atoms);

        _cacheMock.Verify(x => x.InvalidateGameAsync(game.Id, It.IsAny<CancellationToken>()), Times.Once);
        entity.CreatedByUserId.Should().Be(user.Id);

        var history = await _service.GetVersionHistoryAsync(game.Id);
        history.Versions.First().CreatedBy.Should().Be("Author");
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

        result.gameId.Should().BeEquivalentTo(game.Id);
        result.version.Should().BeEquivalentTo("v2");

        var saved = await _dbContext.RuleSpecs
            .SingleAsync(r => r.GameId == game.Id && r.Version == "v2");
        saved.CreatedByUserId.Should().Be(user.Id);

        var history = await _service.GetVersionHistoryAsync(game.Id);
        history.TotalVersions.Should().Be(2);
        history.Versions.First().CreatedBy.Should().Be("Second Author");
        history.Versions.First().Version.Should().Be("v2");
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

        var act = async () => await _service.UpdateRuleSpecAsync(game.Id, model, user.Id);
        await act.Should().ThrowAsync<InvalidOperationException>();
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

        history.GameId.Should().Be(game.Id);
        history.TotalVersions.Should().Be(2);
        history.Versions.Should().HaveCount(2);
        history.Versions[0].Version.Should().Be("v2");
        history.Versions[0].RuleCount.Should().Be(1);
        history.Versions[0].CreatedBy.Should().Be("Test User");
        history.Versions[1].Version.Should().Be("v1");
        history.Versions[1].RuleCount.Should().Be(1);
        history.Versions[1].CreatedBy.Should().Be("Test User");
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
            DisplayName = "Atomic User",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-structured",
            GameId = game.Id,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1234,
            UploadedByUserId = user.Id,
            UploadedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessingStatus = "completed",
            ProcessedAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
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

        result.gameId.Should().BeEquivalentTo(game.Id);
        result.version.Should().StartWith("ingest-");
        result.rules.Count.Should().Be(2);
        result.rules[0].id.Should().Be("r1");
        result.rules[0].text.Should().Be("Setup: Place pieces; Count: 16");
        result.rules[0].page.Should().Be("3");
        result.rules[1].id.Should().Be("r2");
        result.rules[1].text.Should().Be("Victory condition: Highest score wins");
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdfAsync_UsesAtomicRulesWhenPresent()
    {
        var game = new GameEntity
        {
            Id = "game-atomic",
            Name = "Atomic Game",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "user-atomic",
            Email = "atomic@example.com",
            DisplayName = "Atomic Tester",
            PasswordHash = "hash",
            Role = UserRole.Admin,
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
            ProcessingStatus = "completed",
            AtomicRules = JsonSerializer.Serialize(new[]
            {
                "[Table on page 3] Setup: Each player draws five cards"
            }),
            ExtractedText = "This paragraph should be ignored when atomic rules are available"
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var spec = await _service.GenerateRuleSpecFromPdfAsync(pdf.Id);

        spec.gameId.Should().Be(game.Id);
        spec.version.Should().StartWith("ingest-");
        spec.rules.Should().ContainSingle();
        spec.rules[0].id.Should().Be("r1");
        spec.rules[0].text.Should().Be("Setup: Each player draws five cards");
        spec.rules[0].page.Should().Be("3");
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

        var user = new UserEntity
        {
            Id = "user-text",
            Email = "text@example.com",
            DisplayName = "Text Tester",
            PasswordHash = "hash",
            Role = UserRole.Editor,
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
            ProcessingStatus = "completed",
            ExtractedText = "Rule one applies.\nRule two applies."
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var spec = await _service.GenerateRuleSpecFromPdfAsync(pdf.Id);

        spec.gameId.Should().Be(game.Id);
        spec.version.Should().StartWith("ingest-");
        spec.rules.Count.Should().Be(2);
        spec.rules[0].text.Should().Be("Rule one applies.");
        spec.rules[1].text.Should().Be("Rule two applies.");
        spec.rules.Should().OnlyContain(atom => atom.page == null);
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdfAsync_FallsBackToExtractedText()
    {
        var game = new GameEntity
        {
            Id = "game-fallback",
            Name = "Fallback Game",
            CreatedAt = DateTime.UtcNow
        };

        var user = new UserEntity
        {
            Id = "user-fallback",
            Email = "fallback@example.com",
            DisplayName = "Fallback Tester",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        var pdf = new PdfDocumentEntity
        {
            Id = "pdf-fallback",
            GameId = game.Id,
            FileName = "fallback.pdf",
            FilePath = "/tmp/fallback.pdf",
            FileSizeBytes = 4321,
            UploadedByUserId = user.Id,
            UploadedAt = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStatus = "completed",
            AtomicRules = "not-valid-json",
            ExtractedText = "Rule one describes setup.\n\nRule two explains gameplay.\nRule three covers scoring."
        };

        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GenerateRuleSpecFromPdfAsync(pdf.Id);

        result.gameId.Should().BeEquivalentTo(game.Id);
        (result.rules.Count >= 2).Should().BeTrue();
        result.rules.Should().Contain(atom => atom.text.Contains("Rule one"));
        result.rules.Should().Contain(atom => atom.text.Contains("Rule two"));
        result.rules.Should().OnlyContain(atom => atom.section == null);
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdfAsync_WhenPdfMissing_Throws()
    {
        var act = async () => await _service.GenerateRuleSpecFromPdfAsync("missing");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // CreateZipArchiveAsync Tests

    [Fact]
    public async Task CreateZipArchiveAsync_WithSingleGameId_CreatesValidZip()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };
        var spec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };
        spec.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = spec,
            Key = "r1",
            Text = "Two players",
            Section = "Basics",
            PageNumber = 1,
            LineNumber = 1,
            SortOrder = 1
        });

        _dbContext.Games.Add(game);
        _dbContext.RuleSpecs.Add(spec);
        await _dbContext.SaveChangesAsync();

        // Act
        var zipBytes = await _service.CreateZipArchiveAsync(new List<string> { "chess" });

        // Assert
        zipBytes.Should().NotBeNull();
        (zipBytes.Length > 0).Should().BeTrue();

        // Verify ZIP structure
        using var memoryStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        archive.Entries.Should().ContainSingle();
        var entry = archive.Entries[0];
        entry.Name.Should().Be("chess_v1.json");

        // Verify JSON content
        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        var json = await reader.ReadToEndAsync();
        var ruleSpec = JsonSerializer.Deserialize<RuleSpec>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        ruleSpec.Should().NotBeNull();
        ruleSpec.gameId.Should().Be("chess");
        ruleSpec.version.Should().Be("v1");
        ruleSpec.rules.Should().ContainSingle();
        ruleSpec.rules[0].text.Should().Be("Two players");
    }

    [Fact]
    public async Task CreateZipArchiveAsync_WithMultipleGameIds_CreatesZipWithAllGames()
    {
        // Arrange
        var game1 = new GameEntity { Id = "chess", Name = "Chess", CreatedAt = DateTime.UtcNow };
        var game2 = new GameEntity { Id = "checkers", Name = "Checkers", CreatedAt = DateTime.UtcNow };

        var spec1 = new RuleSpecEntity
        {
            GameId = game1.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };
        spec1.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = spec1,
            Key = "r1",
            Text = "Chess rule",
            SortOrder = 1
        });

        var spec2 = new RuleSpecEntity
        {
            GameId = game2.Id,
            Version = "v2",
            CreatedAt = DateTime.UtcNow
        };
        spec2.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = spec2,
            Key = "r1",
            Text = "Checkers rule",
            SortOrder = 1
        });

        _dbContext.Games.AddRange(game1, game2);
        _dbContext.RuleSpecs.AddRange(spec1, spec2);
        await _dbContext.SaveChangesAsync();

        // Act
        var zipBytes = await _service.CreateZipArchiveAsync(new List<string> { "chess", "checkers" });

        // Assert
        using var memoryStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        archive.Entries.Count.Should().Be(2);
        e => e.Name == "chess_v1.json".Should().Contain(archive.Entries);
        e => e.Name == "checkers_v2.json".Should().Contain(archive.Entries);
    }

    [Fact]
    public async Task CreateZipArchiveAsync_WithEmptyGameIds_ThrowsArgumentException()
    {
        var act = async () => await _service.CreateZipArchiveAsync(new List<string>());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateZipArchiveAsync_WithNullGameIds_ThrowsArgumentException()
    {
        var act = async () => await _service.CreateZipArchiveAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateZipArchiveAsync_WithTooManyGameIds_ThrowsArgumentException()
    {
        var tooManyIds = Enumerable.Range(1, 101).Select(i => $"game{i}").ToList();

        var act = async () => await _service.CreateZipArchiveAsync(tooManyIds);
        var ex = await act.Should().ThrowAsync<ArgumentException>();

        ex.Which.Message.Should().Contain("100");
    }

    [Fact]
    public async Task CreateZipArchiveAsync_WithNonExistentGameIds_ThrowsInvalidOperationException()
    {
        var act = async () => await _service.CreateZipArchiveAsync(new List<string> { "non-existent-game" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateZipArchiveAsync_WithDuplicateGameIds_CreatesZipWithSingleEntry()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };
        var spec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };
        spec.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = spec,
            Key = "r1",
            Text = "Rule",
            SortOrder = 1
        });

        _dbContext.Games.Add(game);
        _dbContext.RuleSpecs.Add(spec);
        await _dbContext.SaveChangesAsync();

        // Act - Pass same game ID multiple times
        var zipBytes = await _service.CreateZipArchiveAsync(new List<string> { "chess", "chess", "chess" });

        // Assert - Should deduplicate to single entry
        using var memoryStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        archive.Entries.Should().ContainSingle();
        archive.Entries[0].Name.Should().Be("chess_v1.json");
    }

    [Fact]
    public async Task CreateZipArchiveAsync_SanitizesInvalidFileNames()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = "game/with:invalid*chars",
            Name = "Invalid Name",
            CreatedAt = DateTime.UtcNow
        };
        var spec = new RuleSpecEntity
        {
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow
        };
        spec.Atoms.Add(new RuleAtomEntity
        {
            RuleSpec = spec,
            Key = "r1",
            Text = "Rule",
            SortOrder = 1
        });

        _dbContext.Games.Add(game);
        _dbContext.RuleSpecs.Add(spec);
        await _dbContext.SaveChangesAsync();

        // Act
        var zipBytes = await _service.CreateZipArchiveAsync(new List<string> { "game/with:invalid*chars" });

        // Assert
        using var memoryStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        archive.Entries.Should().ContainSingle();
        var entry = archive.Entries[0];

        // Filename should not contain invalid characters
        entry.Name.Should().NotContain("/");
        entry.Name.Should().NotContain(":");
        entry.Name.Should().NotContain("*");
    }
}
