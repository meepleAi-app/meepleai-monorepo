using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
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
public class CreatePromptTemplateCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly CreatePromptTemplateCommandHandler _handler;

    public CreatePromptTemplateCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"CreatePromptTemplateTests_{Guid.NewGuid()}");
        _mockTimeProvider = new Mock<TimeProvider>();

        _handler = new CreatePromptTemplateCommandHandler(
            _dbContext,
            _mockTimeProvider.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateTemplateWithInitialVersion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2025, 1, 6, 12, 0, 0, TimeSpan.Zero);
        var user = new UserEntity
        {
            Id = userId,
            Email = "admin@test.com",
            DisplayName = "Admin",
            CreatedAt = now.UtcDateTime
        };

        // Seed the user
        await _dbContext.Set<UserEntity>().AddAsync(user);
        await _dbContext.SaveChangesAsync();

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var command = new CreatePromptTemplateCommand(
            Name: "qa-system-prompt",
            Description: "QA system prompt template",
            Category: "qa",
            InitialContent: "You are a board game rules assistant.",
            Metadata: "{\"model\":\"gpt-4\"}",
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("qa-system-prompt");
        result.Description.Should().Be("QA system prompt template");
        result.Category.Should().Be("qa");
        result.CreatedByUserId.Should().Be(userId.ToString());
        result.CreatedByEmail.Should().Be("admin@test.com");
        result.VersionCount.Should().Be(1);
        result.ActiveVersionNumber.Should().Be(1);

        // Verify in database
        var templates = await _dbContext.Set<PromptTemplateEntity>().ToListAsync();
        templates.Should().ContainSingle(t => t.Name == "qa-system-prompt");

        var versions = await _dbContext.Set<PromptVersionEntity>().ToListAsync();
        versions.Should().ContainSingle(v =>
            v.VersionNumber == 1 &&
            v.IsActive &&
            v.Content == "You are a board game rules assistant.");
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2025, 1, 6, 12, 0, 0, TimeSpan.Zero);
        var user = new UserEntity
        {
            Id = userId,
            Email = "admin@test.com",
            DisplayName = "Admin",
            CreatedAt = now.UtcDateTime
        };

        var existingTemplate = new PromptTemplateEntity
        {
            Id = Guid.NewGuid(),
            Name = "qa-system-prompt",
            CreatedByUserId = userId,
            CreatedAt = now.UtcDateTime,
            CreatedBy = user
        };

        // Seed the database
        await _dbContext.Set<UserEntity>().AddAsync(user);
        await _dbContext.Set<PromptTemplateEntity>().AddAsync(existingTemplate);
        await _dbContext.SaveChangesAsync();

        var command = new CreatePromptTemplateCommand(
            Name: "qa-system-prompt",
            Description: "Duplicate template",
            Category: "qa",
            InitialContent: "Content",
            Metadata: null,
            CreatedByUserId: userId
        );

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Contain("already exists");

        // Verify no additional templates were created
        var templates = await _dbContext.Set<PromptTemplateEntity>().ToListAsync();
        templates.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new CreatePromptTemplateCommand(
            Name: "qa-system-prompt",
            Description: "Test",
            Category: "qa",
            InitialContent: "Content",
            Metadata: null,
            CreatedByUserId: userId
        );

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Contain("not found");

        // Verify no templates were created
        var templates = await _dbContext.Set<PromptTemplateEntity>().ToListAsync();
        templates.Should().BeEmpty();
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