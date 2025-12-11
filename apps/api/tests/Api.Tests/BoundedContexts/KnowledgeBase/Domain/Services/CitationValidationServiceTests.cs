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
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for CitationValidationService
/// ISSUE-971: BGAI-029 - Citation validation (verify source references)
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CitationValidationServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"CitationValidationTestDb_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();

        // FIX: Configure GetAndClearEvents() to return empty collection instead of null
        // This prevents NullReferenceException in MeepleAiDbContext.SaveChangesAsync(TestContext.Current.CancellationToken) line 90
        mockEventCollector
            .Setup(ec => ec.GetAndClearEvents())
            .Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }

    /// <summary>
    /// Creates a CitationValidationService instance with the given context
    /// </summary>
    private static CitationValidationService CreateService(MeepleAiDbContext context)
    {
        var mockLogger = new Mock<ILogger<CitationValidationService>>();
        return new CitationValidationService(context, mockLogger.Object);
    }

    /// <summary>
    /// Seeds test data into the given context and returns the IDs
    /// </summary>
    private static async Task<(Guid gameId, Guid pdf1Id, Guid pdf2Id)> SeedTestDataAsync(
        MeepleAiDbContext context)
    {
        var gameId = Guid.NewGuid();
        var pdf1Id = Guid.NewGuid();
        var pdf2Id = Guid.NewGuid();

        context.PdfDocuments.AddRange(
            new PdfDocumentEntity
            {
                Id = pdf1Id,
                GameId = gameId,
                FileName = "test-rules.pdf",
                FilePath = "/test/rules.pdf",
                FileSizeBytes = 1000,
                PageCount = 10,
                UploadedByUserId = Guid.NewGuid(),
                UploadedAt = DateTime.UtcNow
            },
            new PdfDocumentEntity
            {
                Id = pdf2Id,
                GameId = gameId,
                FileName = "test-expansion.pdf",
                FilePath = "/test/expansion.pdf",
                FileSizeBytes = 2000,
                PageCount = 5,
                UploadedByUserId = Guid.NewGuid(),
                UploadedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (gameId, pdf1Id, pdf2Id);
    }

    [Fact]
    public async Task ValidateCitations_AllValid_ReturnsValid()
    {
        // Arrange
        await using var context = CreateFreshDbContext();
        var (gameId, pdf1Id, pdf2Id) = await SeedTestDataAsync(context);
        var service = CreateService(context);

        var snippets = TestDataFactory.CreateValidSnippets(pdf1Id, count: 2)
            .Concat(TestDataFactory.CreateValidSnippets(pdf2Id, count: 1).Select(s => new Snippet(s.text, s.source, page: 3, s.line, score: 0.7f)))
            .ToList();

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(3, result.TotalCitations);
        Assert.Equal(3, result.ValidCitations);
        Assert.Equal(0, result.InvalidCitations);
        Assert.Empty(result.Errors);
        Assert.Equal(1.0, result.ValidationAccuracy);
        Assert.Contains("All 3 citations valid", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCitations_Empty_ReturnsValid()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>();

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0, result.TotalCitations);
        Assert.Equal(0, result.ValidCitations);
        Assert.Empty(result.Errors);
        Assert.Equal("No citations to validate", result.Message);
    }

    [Fact]
    public async Task ValidateCitations_DocumentNotFound_ReturnsInvalid()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var nonExistentPdfId = Guid.NewGuid();
        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{nonExistentPdfId}", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(1, result.TotalCitations);
        Assert.Equal(0, result.ValidCitations);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.DocumentNotFound, result.Errors[0].ErrorType);
        Assert.Contains("not found", result.Errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCitations_InvalidPageNumber_ReturnsInvalid()
    {
        // Arrange - fresh context per test - PDF1 has 10 pages, try page 15
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{pdf1Id}", page: 15, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(1, result.TotalCitations);
        Assert.Equal(0, result.ValidCitations);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.InvalidPageNumber, result.Errors[0].ErrorType);
        Assert.Contains("Invalid page number", result.Errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCitations_PageZero_ReturnsInvalid()
    {
        // Arrange - Page 0 is invalid (pages start at 1)
        await using var context = CreateFreshDbContext();
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);
        var service = CreateService(context);

        var snippets = TestDataFactory.CreateSnippetsWithInvalidPages(pdf1Id).Take(1).ToList();

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.InvalidPageNumber, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task ValidateCitations_MalformedSource_ReturnsInvalid()
    {
        // Arrange - fresh context per test - Invalid source format (not "PDF:guid")
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", "INVALID_FORMAT", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
        Assert.Contains("Invalid source format", result.Errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCitations_NullSource_ReturnsInvalid()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", source: null!, page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task ValidateCitations_MixedValidInvalid_ReturnsPartiallyValid()
    {
        // Arrange - fresh context per test - 2 valid, 1 invalid
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, pdf2Id) = await SeedTestDataAsync(context);

        var nonExistentPdfId = Guid.NewGuid();
        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{pdf1Id}", page: 1, line: 0, score: 0.9f), // Valid
            new Snippet("text2", $"PDF:{nonExistentPdfId}", page: 1, line: 0, score: 0.8f), // Invalid
            new Snippet("text3", $"PDF:{pdf2Id}", page: 2, line: 0, score: 0.7f)  // Valid
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

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
    public async Task ValidateCitations_BoundaryPages_ValidatesCorrectly()
    {
        // Arrange - fresh context per test - Test boundary pages (first and last)
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, pdf2Id) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{pdf1Id}", page: 1, line: 0, score: 0.9f),   // First page (valid)
            new Snippet("text2", $"PDF:{pdf1Id}", page: 10, line: 0, score: 0.8f),  // Last page (valid)
            new Snippet("text3", $"PDF:{pdf2Id}", page: 1, line: 0, score: 0.7f),   // First page (valid)
            new Snippet("text4", $"PDF:{pdf2Id}", page: 5, line: 0, score: 0.6f)    // Last page (valid)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(4, result.ValidCitations);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateSingleCitation_Valid_ReturnsTrue()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);

        var snippet = new Snippet("text", $"PDF:{pdf1Id}", page: 5, line: 0, score: 0.9f);

        // Act
        var result = await service.ValidateSingleCitationAsync(snippet, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateSingleCitation_Invalid_ReturnsFalse()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var nonExistentPdfId = Guid.NewGuid();
        var snippet = new Snippet("text", $"PDF:{nonExistentPdfId}", page: 1, line: 0, score: 0.9f);

        // Act
        var result = await service.ValidateSingleCitationAsync(snippet, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCitations_InvalidGameId_ReturnsInvalid()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (_, pdf1Id, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{pdf1Id}", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, "invalid-game-id", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
        Assert.Contains("Invalid game ID", result.Errors[0].ErrorMessage);
    }

    // ========== Additional Comprehensive Tests (BGAI-031) ==========

    [Fact]
    public async Task ValidateCitations_NegativePageNumber_ReturnsInvalid()
    {
        // Arrange - fresh context per test - Negative page number
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{pdf1Id}", page: -5, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.InvalidPageNumber, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task ValidateCitations_LargeCollection_ValidatesAll()
    {
        // Arrange - fresh context per test - Large collection of valid citations
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>();
        for (int i = 1; i <= 100; i++)
        {
            var page = (i % 10) + 1; // Pages 1-10 for pdf1
            snippets.Add(new Snippet($"text{i}", $"PDF:{pdf1Id}", page: page, line: 0, score: 0.9f));
        }

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(100, result.TotalCitations);
        Assert.Equal(100, result.ValidCitations);
        Assert.Equal(1.0, result.ValidationAccuracy);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateCitations_ValidationAccuracy_CalculatesCorrectly()
    {
        // Arrange - fresh context per test - 7 valid, 3 invalid = 70% accuracy
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, validPdfId, _) = await SeedTestDataAsync(context);

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
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(10, result.TotalCitations);
        Assert.Equal(7, result.ValidCitations);
        Assert.Equal(3, result.InvalidCitations);
        Assert.Equal(0.7, result.ValidationAccuracy);
    }

    [Fact]
    public async Task ValidateCitations_DuplicateCitations_ValidatesEach()
    {
        // Arrange - fresh context per test - Same citation repeated
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", $"PDF:{pdf1Id}", page: 5, line: 0, score: 0.9f),
            new Snippet("text", $"PDF:{pdf1Id}", page: 5, line: 0, score: 0.9f),
            new Snippet("text", $"PDF:{pdf1Id}", page: 5, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(3, result.TotalCitations);
        Assert.Equal(3, result.ValidCitations);
    }

    [Fact]
    public async Task ValidateCitations_WhitespaceSource_ReturnsInvalid()
    {
        // Arrange - fresh context per test - Whitespace-only source
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", "   ", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task ValidateCitations_EmptyStringSource_ReturnsInvalid()
    {
        // Arrange - fresh context per test - Empty string source
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", "", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task ValidateCitations_InvalidGuidInSource_ReturnsInvalid()
    {
        // Arrange - fresh context per test - Invalid GUID format
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", "PDF:not-a-valid-guid", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.DocumentNotFound, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task ValidateCitations_MultiplePdfDocuments_ValidatesCrossDocument()
    {
        // Arrange - fresh context per test - Citations from both PDFs
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, pdf2Id) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{pdf1Id}", page: 1, line: 0, score: 0.9f),
            new Snippet("text2", $"PDF:{pdf1Id}", page: 5, line: 0, score: 0.8f),
            new Snippet("text3", $"PDF:{pdf2Id}", page: 1, line: 0, score: 0.7f),
            new Snippet("text4", $"PDF:{pdf2Id}", page: 3, line: 0, score: 0.6f),
            new Snippet("text5", $"PDF:{pdf1Id}", page: 10, line: 0, score: 0.5f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(5, result.TotalCitations);
        Assert.Equal(5, result.ValidCitations);
    }

    [Fact]
    public async Task ValidateCitations_SourceWithoutColon_ReturnsInvalid()
    {
        // Arrange - fresh context per test - Source missing colon separator
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, _, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", "PDF123456", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task ValidateCitations_SourceWithWrongPrefix_ReturnsInvalid()
    {
        // Arrange - fresh context per test - Wrong prefix (not "PDF:")
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text", $"DOC:{pdf1Id}", page: 1, line: 0, score: 0.9f)
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(CitationErrorType.MalformedSource, result.Errors[0].ErrorType);
    }

    [Fact]
    public async Task ValidateSingleCitation_NegativePage_ReturnsFalse()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);

        var snippet = new Snippet("text", $"PDF:{pdf1Id}", page: -1, line: 0, score: 0.9f);

        // Act
        var result = await service.ValidateSingleCitationAsync(snippet, gameId.ToString(), TestCancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCitations_AllErrorTypes_ReturnsMultipleErrors()
    {
        // Arrange - fresh context per test - Create one of each error type
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var (gameId, pdf1Id, _) = await SeedTestDataAsync(context);

        var nonExistentPdfId = Guid.NewGuid();
        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{nonExistentPdfId}", page: 1, line: 0, score: 0.9f), // DocumentNotFound
            new Snippet("text2", $"PDF:{pdf1Id}", page: 100, line: 0, score: 0.8f),        // InvalidPageNumber
            new Snippet("text3", "INVALID", page: 1, line: 0, score: 0.7f)                 // MalformedSource
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), TestCancellationToken);

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

