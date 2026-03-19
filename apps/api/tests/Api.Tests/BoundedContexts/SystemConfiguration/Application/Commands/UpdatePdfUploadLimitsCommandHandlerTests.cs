using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;
using SystemConfigurationEntity = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Tests for UpdatePdfUploadLimitsCommandHandler.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class UpdatePdfUploadLimitsCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly UpdatePdfUploadLimitsCommandHandler _handler;

    public UpdatePdfUploadLimitsCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _handler = new UpdatePdfUploadLimitsCommandHandler(
            _mockMediator.Object,
            _mockConfigRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidLimits_ReturnsUpdatedDto()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 209715200, // 200MB
            MaxPagesPerDocument: 1000,
            MaxDocumentsPerGame: 20,
            AllowedMimeTypes: ["application/pdf", "application/x-pdf"],
            UpdatedByUserId: adminUserId
        );

        // Mock configuration repository to return null (will trigger creation)
        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationEntity?)null);

        // Mock mediator to return success for Create commands
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationDto(
                Guid.NewGuid(), "key", "value", "string", null, "PdfUpload",
                true, false, "All", 1, DateTime.UtcNow, DateTime.UtcNow
            ));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(209715200, result.MaxFileSizeBytes);
        Assert.Equal(1000, result.MaxPagesPerDocument);
        Assert.Equal(20, result.MaxDocumentsPerGame);
        Assert.Equal(2, result.AllowedMimeTypes.Length);
        Assert.Contains("application/pdf", result.AllowedMimeTypes);
        Assert.Contains("application/x-pdf", result.AllowedMimeTypes);
        Assert.Equal(adminUserId.ToString(), result.LastUpdatedByUserId);

        // Verify mediator was called 4 times (one for each config key)
        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task Handle_WithExistingConfigs_UpdatesInsteadOfCreates()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var existingConfigId = Guid.NewGuid();
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 52428800, // 50MB
            MaxPagesPerDocument: 250,
            MaxDocumentsPerGame: 5,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: adminUserId
        );

        // Mock configuration repository to return existing config
        var existingConfig = new SystemConfigurationEntity(
            id: existingConfigId,
            key: new ConfigKey("PdfUpload:MaxFileSizeBytes"),
            value: "104857600",
            valueType: "long",
            createdByUserId: adminUserId,
            description: "Original description",
            category: "PdfUpload",
            environment: "All",
            requiresRestart: false);

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Mock mediator to return success for Update commands
        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationDto(
                existingConfigId, "key", "value", "string", null, "PdfUpload",
                true, false, "All", 1, DateTime.UtcNow, DateTime.UtcNow
            ));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(52428800, result.MaxFileSizeBytes);

        // Verify Update commands were sent, not Create
        _mockMediator.Verify(
            m => m.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));

        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SerializesMimeTypesAsCommaSeparatedString()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: ["application/pdf", "application/x-pdf"],
            UpdatedByUserId: adminUserId
        );

        CreateConfigurationCommand? capturedCommand = null;

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationEntity?)null);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ConfigurationDto>, CancellationToken>((req, _) =>
            {
                if (req is CreateConfigurationCommand cmd && cmd.Key == "PdfUpload:AllowedMimeTypes")
                    capturedCommand = cmd;
            })
            .ReturnsAsync(new ConfigurationDto(
                Guid.NewGuid(), "key", "value", "string", null, "PdfUpload",
                true, false, "All", 1, DateTime.UtcNow, DateTime.UtcNow
            ));

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal("application/pdf,application/x-pdf", capturedCommand.Value);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }
}
