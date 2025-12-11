using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Tests for PdfValidationDomainService business rule validation.
/// Covers: file size, page count, PDF version, MIME type validation.
/// ISSUE-1818: Migrated to FluentAssertions
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PdfValidationDomainServiceTests
{
    private readonly PdfValidationDomainService _service;

    public PdfValidationDomainServiceTests()
    {
        // Default configuration: MaxFileSizeMb=50, MinPageCount=1, MaxPageCount=500, MinVersion=1.0
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pdf:MaxFileSizeMb"] = "50",
                ["Pdf:MinPageCount"] = "1",
                ["Pdf:MaxPageCount"] = "500",
                ["Pdf:MinVersion"] = "1.0",
                ["Pdf:AllowedContentTypes:0"] = "application/pdf",
                ["Pdf:AllowedContentTypes:1"] = "application/x-pdf"
            })
            .Build();

        _service = new PdfValidationDomainService(configuration);
    }
    [Fact]
    public void ValidateFileSize_ValidSize_ReturnsSuccess()
    {
        // Arrange: 10 MB file (well under 50 MB limit)
        var fileSize = FileSize.FromMegabytes(10);

        // Act
        var result = _service.ValidateFileSize(fileSize);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FieldName.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ValidateFileSize_ExactLimit_ReturnsSuccess()
    {
        // Arrange: Exactly 50 MB (at limit)
        var fileSize = FileSize.FromMegabytes(50);

        // Act
        var result = _service.ValidateFileSize(fileSize);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateFileSize_OverLimit_ReturnsFailure()
    {
        // Arrange: 51 MB (over 50 MB limit)
        var fileSize = FileSize.FromMegabytes(51);

        // Act
        var result = _service.ValidateFileSize(fileSize);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FieldName.Should().Be("fileSize");
        result.Error.Should().Contain("exceeds maximum of 50 MB");
        // Locale-independent check: just verify MB is mentioned
        result.Error.Should().Contain("MB");
    }

    [Fact]
    public void ValidateFileSize_VerySmall_ReturnsSuccess()
    {
        // Arrange: 1 KB file
        var fileSize = FileSize.FromKilobytes(1);

        // Act
        var result = _service.ValidateFileSize(fileSize);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
    [Fact]
    public void ValidatePageCount_ValidCount_ReturnsSuccess()
    {
        // Arrange: 100 pages (well within 1-500 range)
        var pageCount = new PageCount(100);

        // Act
        var result = _service.ValidatePageCount(pageCount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FieldName.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ValidatePageCount_MinimumCount_ReturnsSuccess()
    {
        // Arrange: 1 page (minimum allowed)
        var pageCount = new PageCount(1);

        // Act
        var result = _service.ValidatePageCount(pageCount);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidatePageCount_MaximumCount_ReturnsSuccess()
    {
        // Arrange: 500 pages (exactly at limit)
        var pageCount = new PageCount(500);

        // Act
        var result = _service.ValidatePageCount(pageCount);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidatePageCount_OverMaximum_ReturnsFailure()
    {
        // Arrange: 501 pages (over 500 limit)
        var pageCount = new PageCount(501);

        // Act
        var result = _service.ValidatePageCount(pageCount);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FieldName.Should().Be("pageCount");
        result.Error.Should().Contain("501 pages");
        result.Error.Should().Contain("maximum allowed is 500");
    }

    [Fact]
    public void ValidatePageCount_CustomLimit_RespectsConfiguration()
    {
        // Arrange: Custom config with MaxPageCount=100
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pdf:MinPageCount"] = "1",
                ["Pdf:MaxPageCount"] = "100"
            })
            .Build();
        var service = new PdfValidationDomainService(config);
        var pageCount = new PageCount(150);

        // Act
        var result = service.ValidatePageCount(pageCount);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("150 pages");
        result.Error.Should().Contain("maximum allowed is 100");
    }
    [Fact]
    public void ValidatePdfVersion_ValidVersion_ReturnsSuccess()
    {
        // Arrange: PDF 1.4 (above minimum 1.0)
        var version = PdfVersion.Version14;

        // Act
        var result = _service.ValidatePdfVersion(version);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FieldName.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ValidatePdfVersion_MinimumVersion_ReturnsSuccess()
    {
        // Arrange: PDF 1.0 (exactly at minimum)
        var version = PdfVersion.Version10;

        // Act
        var result = _service.ValidatePdfVersion(version);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidatePdfVersion_NewestVersion_ReturnsSuccess()
    {
        // Arrange: PDF 2.0
        var version = PdfVersion.Version20;

        // Act
        var result = _service.ValidatePdfVersion(version);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidatePdfVersion_CustomMinimum_RespectsConfiguration()
    {
        // Arrange: Custom config requiring minimum PDF 1.4
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pdf:MinVersion"] = "1.4"
            })
            .Build();
        var service = new PdfValidationDomainService(config);
        var oldVersion = PdfVersion.Version13; // PDF 1.3 (below 1.4)

        // Act
        var result = service.ValidatePdfVersion(oldVersion);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FieldName.Should().Be("pdfVersion");
        result.Error.Should().Contain("1.3");
        result.Error.Should().Contain("Minimum version required is 1.4");
    }
    [Fact]
    public void ValidateMimeType_StandardPdfType_ReturnsSuccess()
    {
        // Arrange: Standard application/pdf
        var contentType = "application/pdf";

        // Act
        var result = _service.ValidateMimeType(contentType);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FieldName.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ValidateMimeType_AlternativePdfType_ReturnsSuccess()
    {
        // Arrange: Alternative application/x-pdf
        var contentType = "application/x-pdf";

        // Act
        var result = _service.ValidateMimeType(contentType);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateMimeType_CaseInsensitive_ReturnsSuccess()
    {
        // Arrange: Mixed case
        var contentType = "Application/PDF";

        // Act
        var result = _service.ValidateMimeType(contentType);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateMimeType_InvalidType_ReturnsFailure()
    {
        // Arrange: Non-PDF type
        var contentType = "application/msword";

        // Act
        var result = _service.ValidateMimeType(contentType);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FieldName.Should().Be("fileType");
        result.Error.Should().Contain("application/msword");
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public void ValidateMimeType_Empty_ReturnsFailure()
    {
        // Arrange: Empty content type
        var contentType = "";

        // Act
        var result = _service.ValidateMimeType(contentType);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FieldName.Should().Be("fileType");
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public void ValidateMimeType_Null_ReturnsFailure()
    {
        // Arrange: Null content type
        string? contentType = null;

        // Act
        var result = _service.ValidateMimeType(contentType!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FieldName.Should().Be("fileType");
    }
    [Fact]
    public void ValidateFileSize_DefaultConfiguration_UsesDefaultLimit()
    {
        // Arrange: Empty config (should use defaults)
        var config = new ConfigurationBuilder().Build();
        var service = new PdfValidationDomainService(config);
        var fileSize = FileSize.FromMegabytes(51); // Over default 50 MB

        // Act
        var result = service.ValidateFileSize(fileSize);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("50 MB");
    }

    [Fact]
    public void ValidateMimeType_DefaultConfiguration_UsesDefaultTypes()
    {
        // Arrange: Empty config (should use default types)
        var config = new ConfigurationBuilder().Build();
        var service = new PdfValidationDomainService(config);

        // Act
        var result = service.ValidateMimeType("application/pdf");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
