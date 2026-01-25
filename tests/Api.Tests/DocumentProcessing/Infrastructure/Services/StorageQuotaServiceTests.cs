using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.DocumentProcessing.Infrastructure.Services;

public sealed class StorageQuotaServiceTests
{
    private readonly Mock<IPdfDocumentRepository> _documentRepo;
    private readonly Mock<IConfiguration> _configuration;
    private readonly Mock<ILogger<StorageQuotaService>> _logger;
    private readonly StorageQuotaService _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public StorageQuotaServiceTests()
    {
        _documentRepo = new Mock<IPdfDocumentRepository>();
        _configuration = new Mock<IConfiguration>();
        _logger = new Mock<ILogger<StorageQuotaService>>();

        // Setup configuration mock for Storage:MaxBytesPerUser
        var configSection = new Mock<IConfigurationSection>();
        configSection.Setup(x => x.Value).Returns((1024L * 1024 * 1024).ToString()); // 1 GB
        _configuration.Setup(x => x.GetSection("Storage:MaxBytesPerUser")).Returns(configSection.Object);

        _sut = new StorageQuotaService(_documentRepo.Object, _configuration.Object, _logger.Object);
    }

    [Fact]
    public async Task GetUserQuota_WithUserDocuments_CalculatesCorrectly()
    {
        // Arrange
        var userDocuments = new List<PdfDocument>
        {
            CreateTestDocument(_userId, 1024 * 1024),      // 1 MB
            CreateTestDocument(_userId, 5 * 1024 * 1024)   // 5 MB
        };

        _documentRepo.Setup(r => r.FindByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userDocuments);

        // Act
        var result = await _sut.GetUserQuota(_userId);

        // Assert
        result.Should().NotBeNull();
        result.UsedBytes.Should().Be(6 * 1024 * 1024); // 1 MB + 5 MB
        result.MaxBytes.Should().Be(1024L * 1024 * 1024); // 1 GB default
    }

    [Fact]
    public async Task CanUploadDocument_WhenWithinQuota_ReturnsTrue()
    {
        // Arrange
        var userDocuments = new List<PdfDocument>
        {
            CreateTestDocument(_userId, 100 * 1024 * 1024) // 100 MB
        };

        _documentRepo.Setup(r => r.FindByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userDocuments);

        // Act
        var result = await _sut.CanUploadDocument(_userId, 50 * 1024 * 1024);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanUploadDocument_WhenExceedsQuota_ReturnsFalse()
    {
        // Arrange
        var userDocuments = new List<PdfDocument>
        {
            CreateTestDocument(_userId, 900 * 1024 * 1024) // 900 MB
        };

        _documentRepo.Setup(r => r.FindByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userDocuments);

        // Act
        var result = await _sut.CanUploadDocument(_userId, 200 * 1024 * 1024);

        // Assert
        result.Should().BeFalse();
    }

    private static PdfDocument CreateTestDocument(Guid userId, long fileSize, Guid? id = null)
    {
        return new PdfDocument(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "uploads/test.pdf",
            new FileSize(fileSize),
            userId);
    }
}
