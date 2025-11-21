using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;
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

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

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
        var snippets = TestDataFactory.CreateValidSnippets(_pdf1Id, count: 2)
            .Concat(TestDataFactory.CreateValidSnippets(_pdf2Id, count: 1).Select(s => new Snippet(s.text, s.source, page: 3, s.line, score: 0.7f)))
            .ToList();

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
        var snippets = TestDataFactory.CreateSnippetsWithInvalidPages(_pdf1Id).Take(1).ToList();

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

    // ========== Additional Comprehensive Tests (BGAI-031) ==========

    [Fact]
    public async Task Test13_ValidateCitations_NegativePageNumber_ReturnsInvalid()
    {
        // Arrange - Negative page number
        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{_pdf1Id}", page: -5, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.InvalidPageNumber, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task Test14_ValidateCitations_LargeCollection_ValidatesAll()
    {
        // Arrange - Large collection of valid citations
        var snippets = new List<Snippet>();
        for (int i = 1; i <= 100; i++)
        {
            var page = (i % 10) + 1; // Pages 1-10 for pdf1
            snippets.Add(new Snippet($"text{i}", $"PDF:{_pdf1Id}", page: page, line: 0, score: 0.9f));
        }

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(100, result.TotalCitations);
        Assert.Equal(100, result.ValidCitations);
        Assert.Equal(1.0, result.ValidationAccuracy);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Test15_ValidateCitations_ValidationAccuracy_CalculatesCorrectly()
    {
        // Arrange - 7 valid, 3 invalid = 70% accuracy
        var validPdfId = _pdf1Id;
        var invalidPdfId = Guid.NewGuid();
        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{validPdfId}", page: 1, line: 0, score: 0.9f),
            new Snippet("text2", $"PDF:{validPdfId}", page: 2, line: 0, score: 0.9f),
            new Snippet("text3", $"PDF:{validPdfId}", page: 3, line: 0, score: 0.9f),
            new Snippet("text4", $"PDF:{invalidPdfId}", page: 1, line: 0, score: 0.9f), // Invalid
            new Snippet("text5", $"PDF:{validPdfId}", page: 4, line: 0, score: 0.9f),
            new Snippet("text6", $"PDF:{invalidPdfId}", page: 1, line: 0, score: 0.9f), // Invalid
            new Snippet("text7", $"PDF:{validPdfId}", page: 5, line: 0, score: 0.9f),
            new Snippet("text8", $"PDF:{invalidPdfId}", page: 1, line: 0, score: 0.9f), // Invalid
            new Snippet("text9", $"PDF:{validPdfId}", page: 6, line: 0, score: 0.9f),
            new Snippet("text10", $"PDF:{validPdfId}", page: 7, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(10, result.TotalCitations);
        Assert.Equal(7, result.ValidCitations);
        Assert.Equal(3, result.InvalidCitations);
        Assert.Equal(0.7, result.ValidationAccuracy);
    }

    [Fact]
    public async Task Test16_ValidateCitations_DuplicateCitations_ValidatesEach()
    {
        // Arrange - Same citation repeated
        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{_pdf1Id}", page: 5, line: 0, score: 0.9f),
            new Snippet("text", $"PDF:{_pdf1Id}", page: 5, line: 0, score: 0.9f),
            new Snippet("text", $"PDF:{_pdf1Id}", page: 5, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(3, result.TotalCitations);
        Assert.Equal(3, result.ValidCitations);
    }

    [Fact]
    public async Task Test17_ValidateCitations_WhitespaceSource_ReturnsInvalid()
    {
        // Arrange - Whitespace-only source
        var snippets = new List<Snippet>
        {
            new Snippet("text", "   ", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task Test18_ValidateCitations_EmptyStringSource_ReturnsInvalid()
    {
        // Arrange - Empty string source
        var snippets = new List<Snippet>
        {
            new Snippet("text", "", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task Test19_ValidateCitations_InvalidGuidInSource_ReturnsInvalid()
    {
        // Arrange - Invalid GUID format
        var snippets = new List<Snippet>
        {
            new Snippet("text", "PDF:not-a-valid-guid", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.DocumentNotFound, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task Test20_ValidateCitations_MultiplePdfDocuments_ValidatesCrossDocument()
    {
        // Arrange - Citations from both PDFs
        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{_pdf1Id}", page: 1, line: 0, score: 0.9f),
            new Snippet("text2", $"PDF:{_pdf1Id}", page: 5, line: 0, score: 0.8f),
            new Snippet("text3", $"PDF:{_pdf2Id}", page: 1, line: 0, score: 0.7f),
            new Snippet("text4", $"PDF:{_pdf2Id}", page: 3, line: 0, score: 0.6f),
            new Snippet("text5", $"PDF:{_pdf1Id}", page: 10, line: 0, score: 0.5f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(5, result.TotalCitations);
        Assert.Equal(5, result.ValidCitations);
    }

    [Fact]
    public async Task Test21_ValidateCitations_SourceWithoutColon_ReturnsInvalid()
    {
        // Arrange - Source missing colon separator
        var snippets = new List<Snippet>
        {
            new Snippet("text", "PDF123456", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task Test22_ValidateCitations_SourceWithWrongPrefix_ReturnsInvalid()
    {
        // Arrange - Wrong prefix (not "PDF:")
        var snippets = new List<Snippet>
        {
            new Snippet("text", $"DOC:{_pdf1Id}", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task Test23_ValidateSingleCitation_NegativePage_ReturnsFalse()
    {
        // Arrange
        var snippet = new Snippet("text", $"PDF:{_pdf1Id}", page: -1, line: 0, score: 0.9f);

        // Act
        var result = await _service.ValidateSingleCitationAsync(snippet, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Test24_ValidateCitations_AllErrorTypes_ReturnsMultipleErrors()
    {
        // Arrange - Create one of each error type
        var nonExistentPdfId = Guid.NewGuid();
        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{nonExistentPdfId}", page: 1, line: 0, score: 0.9f), // DocumentNotFound
            new Snippet("text2", $"PDF:{_pdf1Id}", page: 100, line: 0, score: 0.8f),        // InvalidPageNumber
            new Snippet("text3", "INVALID", page: 1, line: 0, score: 0.7f)                 // MalformedSource
        };

        // Act
        var result = await _service.ValidateCitationsAsync(snippets, _gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.TotalCitations);
        Assert.Equal(0, result.ValidCitations);
        Assert.Equal(3, result.Errors.Count);

        // Verify we have different error types
        var errorTypes = result.Errors.Select(e => e.ErrorType).ToHashSet();
        Assert.Contains(CitationErrorType.DocumentNotFound, errorTypes);
        Assert.Contains(CitationErrorType.InvalidPageNumber, errorTypes);
        Assert.Contains(CitationErrorType.MalformedSource, errorTypes);
    }
}
