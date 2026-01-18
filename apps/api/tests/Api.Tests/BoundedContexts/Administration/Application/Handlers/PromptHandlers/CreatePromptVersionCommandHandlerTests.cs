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
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.PromptHandlers;

[Trait("Category", TestCategories.Unit)]
public class CreatePromptVersionCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly CreatePromptVersionCommandHandler _handler;

    public CreatePromptVersionCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"CreatePromptVersionTests_{Guid.NewGuid()}");
        _mockTimeProvider = new Mock<TimeProvider>();

        _handler = new CreatePromptVersionCommandHandler(
            _dbContext,
            _mockTimeProvider.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateNewVersionWithIncrementedNumber()
    {
        // Arrange
        var templateId = Guid.NewGuid();
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
            CreatedBy = user,
            Versions = new List<PromptVersionEntity>()
        };

        var existingVersion1 = new PromptVersionEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            VersionNumber = 1,
            Content = "V1",
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            Template = template,
            CreatedBy = user
        };

        var existingVersion2 = new PromptVersionEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            VersionNumber = 2,
            Content = "V2",
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            Template = template,
            CreatedBy = user
        };

        template.Versions.Add(existingVersion1);
        template.Versions.Add(existingVersion2);

        // Seed the database
        await _dbContext.Set<UserEntity>().AddAsync(user);
        await _dbContext.Set<PromptTemplateEntity>().AddAsync(template);
        await _dbContext.SaveChangesAsync();

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var command = new CreatePromptVersionCommand(
            TemplateId: templateId,
            Content: "You are an improved board game rules assistant with better context awareness.",
            Metadata: "{\"changes\":\"Improved context handling\"}",
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TemplateId.Should().Be(templateId.ToString());
        result.VersionNumber.Should().Be(3);
        result.Content.Should().Contain("improved");
        result.IsActive.Should().BeFalse();
        result.CreatedByUserId.Should().Be(userId.ToString());
        result.CreatedByEmail.Should().Be("admin@test.com");
        result.Metadata.Should().Be("{\"changes\":\"Improved context handling\"}");

        // Verify in database
        var versions = await _dbContext.Set<PromptVersionEntity>().ToListAsync();
        versions.Should().HaveCount(3);
        versions.Should().ContainSingle(v => v.VersionNumber == 3 && !v.IsActive);
    }

    [Fact]
    public async Task Handle_WithFirstVersion_ShouldCreateVersionNumberOne()
    {
        // Arrange
        var templateId = Guid.NewGuid();
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
            Name = "new-prompt",
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            CreatedBy = user,
            Versions = new List<PromptVersionEntity>()
        };

        // Seed the database
        await _dbContext.Set<UserEntity>().AddAsync(user);
        await _dbContext.Set<PromptTemplateEntity>().AddAsync(template);
        await _dbContext.SaveChangesAsync();

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var command = new CreatePromptVersionCommand(
            TemplateId: templateId,
            Content: "Initial content",
            Metadata: null,
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.VersionNumber.Should().Be(1);
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithNonExistentTemplate_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new CreatePromptVersionCommand(
            TemplateId: templateId,
            Content: "Content",
            Metadata: null,
            CreatedByUserId: userId
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("not found");

        // Verify no versions were created
        var versions = await _dbContext.Set<PromptVersionEntity>().ToListAsync();
        versions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var now = new DateTimeOffset(2025, 1, 6, 12, 0, 0, TimeSpan.Zero);

        var user = new UserEntity
        {
            Id = anotherUserId,
            Email = "admin@test.com",
            DisplayName = "Admin",
            CreatedAt = now.UtcDateTime
        };

        var template = new PromptTemplateEntity
        {
            Id = templateId,
            Name = "qa-system-prompt",
            CreatedByUserId = anotherUserId,
            CreatedAt = now.UtcDateTime,
            CreatedBy = user,
            Versions = new List<PromptVersionEntity>()
        };

        // Seed the database
        await _dbContext.Set<UserEntity>().AddAsync(user);
        await _dbContext.Set<PromptTemplateEntity>().AddAsync(template);
        await _dbContext.SaveChangesAsync();

        var command = new CreatePromptVersionCommand(
            TemplateId: templateId,
            Content: "Content",
            Metadata: null,
            CreatedByUserId: userId  // This user doesn't exist
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("not found");

        // Verify no versions were created
        var versions = await _dbContext.Set<PromptVersionEntity>().ToListAsync();
        versions.Should().BeEmpty();
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