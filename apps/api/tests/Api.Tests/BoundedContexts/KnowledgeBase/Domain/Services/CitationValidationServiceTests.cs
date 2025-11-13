using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for CitationValidationService
/// ISSUE-971: BGAI-029 - Citation validation (verify source references)
/// </summary>
public class CitationValidationServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly CitationValidationService _service;
    private readonly Guid _gameId;
    private readonly Guid _pdf1Id;
    private readonly Guid _pdf2Id;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CitationValidationServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"CitationValidationTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(options);

        var mockLogger = new Mock<ILogger<CitationValidationService>>();
        _service = new CitationValidationService(_dbContext, mockLogger.Object);

        // Seed test data
        _gameId = Guid.NewGuid();
        _pdf1Id = Guid.NewGuid();
        _pdf2Id = Guid.NewGuid();

        SeedTestData();
    }

    private void SeedTestData()
    {
        _dbContext.PdfDocuments.AddRange(
            new PdfDocumentEntity
            {
                Id = _pdf1Id,
                GameId = _gameId,
                FileName = "test-rules.pdf",
                FilePath = "/test/rules.pdf",
                FileSizeBytes = 1000,
                PageCount = 10,
                UploadedByUserId = Guid.NewGuid(),
                UploadedAt = DateTime.UtcNow
            },
            new PdfDocumentEntity
            {
                Id = _pdf2Id,
                GameId = _gameId,
                FileName = "test-expansion.pdf",
                FilePath = "/test/expansion.pdf",
                FileSizeBytes = 2000,
                PageCount = 5,
                UploadedByUserId = Guid.NewGuid(),
                UploadedAt = DateTime.UtcNow
            });

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    [Fact]
    public async Task Test01_ValidateCitations_AllValid_ReturnsValid()
    {
        // Arrange
        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{_pdf1Id}", page: 1, line: 0, score: 0.9f),
            new Snippet("text2", $"PDF:{_pdf1Id}", page: 5, line: 0, score: 0.8f),
            new Snippet("text3", $"PDF:{_pdf2Id}", page: 3, line: 0, score: 0.7f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(3, result.TotalCitations);
        Assert.Equal(3, result.ValidCitations);
        Assert.Equal(0, result.InvalidCitations);
        Assert.Empty(result.Errors);
        Assert.Equal(1.0, result.ValidationAccuracy);
        Assert.Contains("All 3 citations valid", result.Message);
    }

    [Fact]
    public async Task Test02_ValidateCitations_Empty_ReturnsValid()
    {
        // Arrange
        var snippets = new List<Snippet>();

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.TotalCitations);
        Assert.Equal(0, result.ValidCitations);
        Assert.Empty(result.Errors);
        Assert.Equal("No citations to validate", result.Message);
    }

    [Fact]
    public async Task Test03_ValidateCitations_DocumentNotFound_ReturnsInvalid()
    {
        // Arrange
        var nonExistentPdfId = Guid.NewGuid();
        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{nonExistentPdfId}", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(1, result.TotalCitations);
        Assert.Equal(0, result.ValidCitations);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.DocumentNotFound, result.Errors[0].ErrorType);
        Assert.Contains("not found", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Test04_ValidateCitations_InvalidPageNumber_ReturnsInvalid()
    {
        // Arrange - PDF1 has 10 pages, try page 15
        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{_pdf1Id}", page: 15, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(1, result.TotalCitations);
        Assert.Equal(0, result.ValidCitations);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.InvalidPageNumber, result.Errors[0].ErrorType);
        Assert.Contains("Invalid page number", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Test05_ValidateCitations_PageZero_ReturnsInvalid()
    {
        // Arrange - Page 0 is invalid (pages start at 1)
        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{_pdf1Id}", page: 0, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.InvalidPageNumber, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task Test06_ValidateCitations_MalformedSource_ReturnsInvalid()
    {
        // Arrange - Invalid source format (not "PDF:guid")
        var snippets = new List<Snippet>
        {
            new Snippet("text", "INVALID_FORMAT", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
        Assert.Contains("Invalid source format", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Test07_ValidateCitations_NullSource_ReturnsInvalid()
    {
        // Arrange
        var snippets = new List<Snippet>
        {
            new Snippet("text", source: null!, page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task Test08_ValidateCitations_MixedValidInvalid_ReturnsPartiallyValid()
    {
        // Arrange - 2 valid, 1 invalid
        var nonExistentPdfId = Guid.NewGuid();
        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{_pdf1Id}", page: 1, line: 0, score: 0.9f), // Valid
            new Snippet("text2", $"PDF:{nonExistentPdfId}", page: 1, line: 0, score: 0.8f), // Invalid
            new Snippet("text3", $"PDF:{_pdf2Id}", page: 2, line: 0, score: 0.7f)  // Valid
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid); // Not all valid
        Assert.Equal(3, result.TotalCitations);
        Assert.Equal(2, result.ValidCitations);
        Assert.Equal(1, result.InvalidCitations);
        Assert.Single(result.Errors);
        Assert.Equal(2.0 / 3.0, result.ValidationAccuracy, precision: 2);
        Assert.Contains("2/3 citations valid", result.Message);
    }

    [Fact]
    public async Task Test09_ValidateCitations_BoundaryPages_ValidatesCorrectly()
    {
        // Arrange - Test boundary pages (first and last)
        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{_pdf1Id}", page: 1, line: 0, score: 0.9f),   // First page (valid)
            new Snippet("text2", $"PDF:{_pdf1Id}", page: 10, line: 0, score: 0.8f),  // Last page (valid)
            new Snippet("text3", $"PDF:{_pdf2Id}", page: 1, line: 0, score: 0.7f),   // First page (valid)
            new Snippet("text4", $"PDF:{_pdf2Id}", page: 5, line: 0, score: 0.6f)    // Last page (valid)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(4, result.ValidCitations);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Test10_ValidateSingleCitation_Valid_ReturnsTrue()
    {
        // Arrange
        var snippet = new Snippet("text", $"PDF:{_pdf1Id}", page: 5, line: 0, score: 0.9f);

        // Act
        var result = await _service.ValidateSingleCitationAsync(snippet, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task Test11_ValidateSingleCitation_Invalid_ReturnsFalse()
    {
        // Arrange
        var nonExistentPdfId = Guid.NewGuid();
        var snippet = new Snippet("text", $"PDF:{nonExistentPdfId}", page: 1, line: 0, score: 0.9f);

        // Act
        var result = await _service.ValidateSingleCitationAsync(snippet, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Test12_ValidateCitations_InvalidGameId_ReturnsInvalid()
    {
        // Arrange
        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{_pdf1Id}", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, "invalid-game-id", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
        Assert.Contains("Invalid game ID", result.Errors[0].ErrorMessage);
    }
}
