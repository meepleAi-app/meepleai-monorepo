using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Tests for GetPdfUploadLimitsQueryHandler.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class GetPdfUploadLimitsQueryHandlerTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly GetPdfUploadLimitsQueryHandler _handler;

    private const string MaxFileSizeKey = "PdfUpload:MaxFileSizeBytes";
    private const string MaxPagesKey = "PdfUpload:MaxPagesPerDocument";
    private const string MaxDocumentsKey = "PdfUpload:MaxDocumentsPerGame";
    private const string AllowedMimeTypesKey = "PdfUpload:AllowedMimeTypes";

    public GetPdfUploadLimitsQueryHandlerTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _handler = new GetPdfUploadLimitsQueryHandler(_mockConfigService.Object);
    }

    [Fact]
    public async Task Handle_WithExistingConfigurations_ReturnsConfiguredLimits()
    {
        // Arrange
        var query = new GetPdfUploadLimitsQuery();
        var lastUpdatedAt = DateTime.UtcNow.AddMinutes(-5);

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxFileSizeKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "1", MaxFileSizeKey, "209715200", "long", null, "PdfUpload",
                true, false, "All", 1, null, DateTime.UtcNow, lastUpdatedAt,
                "admin-user-1", "admin-user-1", null
            ));

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxPagesKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "2", MaxPagesKey, "1000", "int", null, "PdfUpload",
                true, false, "All", 1, null, DateTime.UtcNow, lastUpdatedAt.AddMinutes(1),
                "admin-user-1", "admin-user-1", null
            ));

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxDocumentsKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "3", MaxDocumentsKey, "20", "int", null, "PdfUpload",
                true, false, "All", 1, null, DateTime.UtcNow, lastUpdatedAt.AddMinutes(2),
                "admin-user-1", "admin-user-1", null
            ));

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(AllowedMimeTypesKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "4", AllowedMimeTypesKey, "application/pdf,application/x-pdf", "string", null, "PdfUpload",
                true, false, "All", 1, null, DateTime.UtcNow, lastUpdatedAt.AddMinutes(3),
                "admin-user-1", "admin-user-1", null
            ));

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(209715200, result.MaxFileSizeBytes); // 200MB
        Assert.Equal(1000, result.MaxPagesPerDocument);
        Assert.Equal(20, result.MaxDocumentsPerGame);
        Assert.Equal(2, result.AllowedMimeTypes.Length);
        Assert.Contains("application/pdf", result.AllowedMimeTypes);
        Assert.Contains("application/x-pdf", result.AllowedMimeTypes);
        Assert.Equal("admin-user-1", result.LastUpdatedByUserId);
    }

    [Fact]
    public async Task Handle_WithMissingConfigurations_ReturnsDefaults()
    {
        // Arrange
        var query = new GetPdfUploadLimitsQuery();

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(104857600, result.MaxFileSizeBytes); // Default 100MB
        Assert.Equal(500, result.MaxPagesPerDocument);    // Default 500 pages
        Assert.Equal(10, result.MaxDocumentsPerGame);     // Default 10 documents
        Assert.Single(result.AllowedMimeTypes);
        Assert.Equal("application/pdf", result.AllowedMimeTypes[0]);
        Assert.Null(result.LastUpdatedByUserId);
    }

    [Fact]
    public async Task Handle_WithPartialConfigurations_ReturnsMixedValues()
    {
        // Arrange
        var query = new GetPdfUploadLimitsQuery();

        // Only MaxFileSizeBytes is configured
        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxFileSizeKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "1", MaxFileSizeKey, "52428800", "long", null, "PdfUpload",
                true, false, "All", 1, null, DateTime.UtcNow, DateTime.UtcNow,
                "admin-user-1", "admin-user-1", null
            ));

        // Other configs return null (not configured)
        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxPagesKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxDocumentsKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(AllowedMimeTypesKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(52428800, result.MaxFileSizeBytes);  // Configured: 50MB
        Assert.Equal(500, result.MaxPagesPerDocument);    // Default
        Assert.Equal(10, result.MaxDocumentsPerGame);     // Default
        Assert.Single(result.AllowedMimeTypes);           // Default
        Assert.Equal("admin-user-1", result.LastUpdatedByUserId);
    }

    [Fact]
    public async Task Handle_WithInvalidNumericValues_ReturnsDefaults()
    {
        // Arrange
        var query = new GetPdfUploadLimitsQuery();

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxFileSizeKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "1", MaxFileSizeKey, "not-a-number", "long", null, "PdfUpload",
                true, false, "All", 1, null, DateTime.UtcNow, DateTime.UtcNow,
                "admin-user-1", "admin-user-1", null
            ));

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxPagesKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "2", MaxPagesKey, "invalid", "int", null, "PdfUpload",
                true, false, "All", 1, null, DateTime.UtcNow, DateTime.UtcNow,
                "admin-user-1", "admin-user-1", null
            ));

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(MaxDocumentsKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(AllowedMimeTypesKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(104857600, result.MaxFileSizeBytes); // Default due to parse failure
        Assert.Equal(500, result.MaxPagesPerDocument);    // Default due to parse failure
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }
}
