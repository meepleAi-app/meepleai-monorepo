using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.PromptHandlers;

[Trait("Category", TestCategories.Unit)]
public class ActivatePromptVersionCommandHandlerTests
{
    private readonly Mock<MeepleAiDbContext> _mockDbContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly Mock<ILogger<ActivatePromptVersionCommandHandler>> _mockLogger;
    private readonly ActivatePromptVersionCommandHandler _handler;
    private readonly Mock<DbSet<PromptVersionEntity>> _mockVersionSet;
    private readonly Mock<DbSet<PromptAuditLogEntity>> _mockAuditLogSet;

    public ActivatePromptVersionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockDbContext = new Mock<MeepleAiDbContext>(options);
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockLogger = new Mock<ILogger<ActivatePromptVersionCommandHandler>>();
        _mockVersionSet = new Mock<DbSet<PromptVersionEntity>>();
        _mockAuditLogSet = new Mock<DbSet<PromptAuditLogEntity>>();

        _mockDbContext.Setup(db => db.PromptVersions).Returns(_mockVersionSet.Object);
        _mockDbContext.Setup(db => db.PromptAuditLogs).Returns(_mockAuditLogSet.Object);

        _handler = new ActivatePromptVersionCommandHandler(
            _mockDbContext.Object,
            _mockLogger.Object,
            _mockTimeProvider.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldActivateVersionAndDeactivateOthers()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var otherVersionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2025, 1, 6, 12, 0, 0, TimeSpan.Zero);

        var user = new UserEntity
        {
            Id = userId,
            Email = "admin@test.com",
            DisplayName = "Admin",
            CreatedAt = now.UtcDateTime
        };

        var template = new PromptTemplateEntity
        {
            Id = templateId,
            Name = "qa-system-prompt",
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            CreatedBy = user
        };

        var versionToActivate = new PromptVersionEntity
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 2,
            Content = "V2",
            IsActive = false,
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            Template = template,
            CreatedBy = user
        };

        var activeVersion = new PromptVersionEntity
        {
            Id = otherVersionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Content = "V1",
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            Template = template,
            CreatedBy = user
        };

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        // Setup for LoadAndValidateVersionAsync
        var versionList = new List<PromptVersionEntity> { versionToActivate }.AsQueryable();
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.Provider).Returns(versionList.Provider);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.Expression).Returns(versionList.Expression);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.ElementType).Returns(versionList.ElementType);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.GetEnumerator()).Returns(versionList.GetEnumerator());

        // Setup for DeactivateOtherVersionsAsync
        var otherVersionsList = new List<PromptVersionEntity> { activeVersion }.AsQueryable();
        var mockOtherVersionsSet = new Mock<DbSet<PromptVersionEntity>>();
        mockOtherVersionsSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.Provider).Returns(otherVersionsList.Provider);
        mockOtherVersionsSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.Expression).Returns(otherVersionsList.Expression);
        mockOtherVersionsSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.ElementType).Returns(otherVersionsList.ElementType);
        mockOtherVersionsSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.GetEnumerator()).Returns(otherVersionsList.GetEnumerator());

        // Mock transaction
        var mockTransaction = new Mock<IDbContextTransaction>();
        _mockDbContext.Setup(db => db.Database.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTransaction.Object);

        var command = new ActivatePromptVersionCommand(
            TemplateId: templateId,
            VersionId: versionId,
            ActivatedByUserId: userId,
            Reason: "Testing improved version"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.VersionNumber.Should().Be(2);
        result.IsActive.Should().BeTrue();
        result.ActivatedByUserId.Should().Be(userId.ToString());
        result.ActivationReason.Should().Be("Testing improved version");

        _mockAuditLogSet.Verify(s => s.Add(It.Is<PromptAuditLogEntity>(log =>
            log.Action == "version_activated"
        )), Times.Once);

        _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAlreadyActiveVersion_ShouldReturnImmediately()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2025, 1, 6, 12, 0, 0, TimeSpan.Zero);

        var user = new UserEntity
        {
            Id = userId,
            Email = "admin@test.com",
            DisplayName = "Admin",
            CreatedAt = now.UtcDateTime
        };

        var template = new PromptTemplateEntity
        {
            Id = templateId,
            Name = "qa-system-prompt",
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            CreatedBy = user
        };

        var activeVersion = new PromptVersionEntity
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Content = "V1",
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            Template = template,
            CreatedBy = user
        };

        var versionList = new List<PromptVersionEntity> { activeVersion }.AsQueryable();
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.Provider).Returns(versionList.Provider);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.Expression).Returns(versionList.Expression);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.ElementType).Returns(versionList.ElementType);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.GetEnumerator()).Returns(versionList.GetEnumerator());

        var command = new ActivatePromptVersionCommand(
            TemplateId: templateId,
            VersionId: versionId,
            ActivatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsActive.Should().BeTrue();

        // Should not start transaction for already active version
        _mockDbContext.Verify(db => db.Database.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyTemplateId_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new ActivatePromptVersionCommand(
            TemplateId: Guid.Empty,
            VersionId: Guid.NewGuid(),
            ActivatedByUserId: Guid.NewGuid()
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithEmptyVersionId_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new ActivatePromptVersionCommand(
            TemplateId: Guid.NewGuid(),
            VersionId: Guid.Empty,
            ActivatedByUserId: Guid.NewGuid()
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNonExistentVersion_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var versionList = new List<PromptVersionEntity>().AsQueryable();
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.Provider).Returns(versionList.Provider);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.Expression).Returns(versionList.Expression);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.ElementType).Returns(versionList.ElementType);
        _mockVersionSet.As<IQueryable<PromptVersionEntity>>().Setup(m => m.GetEnumerator()).Returns(versionList.GetEnumerator());

        var command = new ActivatePromptVersionCommand(
            TemplateId: templateId,
            VersionId: versionId,
            ActivatedByUserId: userId
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("not found");
    }
}
