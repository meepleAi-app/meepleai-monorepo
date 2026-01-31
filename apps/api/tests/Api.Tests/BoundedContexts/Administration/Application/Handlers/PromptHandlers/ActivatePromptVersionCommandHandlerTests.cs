using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.PromptHandlers;

[Trait("Category", TestCategories.Unit)]
public class ActivatePromptVersionCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly Mock<ILogger<ActivatePromptVersionCommandHandler>> _mockLogger;
    private readonly ActivatePromptVersionCommandHandler _handler;

    public ActivatePromptVersionCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"ActivatePromptVersionTests_{Guid.NewGuid()}");
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockLogger = new Mock<ILogger<ActivatePromptVersionCommandHandler>>();

        _handler = new ActivatePromptVersionCommandHandler(
            _dbContext,
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

        // Seed the database
        await _dbContext.Set<UserEntity>().AddAsync(user);
        await _dbContext.Set<PromptTemplateEntity>().AddAsync(template);
        await _dbContext.PromptVersions.AddRangeAsync(versionToActivate, activeVersion);
        await _dbContext.SaveChangesAsync();

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

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

        // Verify in database
        var activatedVersion = await _dbContext.PromptVersions.FindAsync(versionId);
        activatedVersion!.IsActive.Should().BeTrue();

        var deactivatedVersion = await _dbContext.PromptVersions.FindAsync(otherVersionId);
        deactivatedVersion!.IsActive.Should().BeFalse();

        // Verify audit log was created
        var auditLogs = await _dbContext.PromptAuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle(log => log.Action == "version_activated");
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

        // Seed the database
        await _dbContext.Set<UserEntity>().AddAsync(user);
        await _dbContext.Set<PromptTemplateEntity>().AddAsync(template);
        await _dbContext.PromptVersions.AddAsync(activeVersion);
        await _dbContext.SaveChangesAsync();

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

        // No audit log should be created for already active version
        var auditLogs = await _dbContext.PromptAuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty();
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbContext?.Dispose();
        }
    }
}
