using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.PromptHandlers;

[Trait("Category", TestCategories.Unit)]
public class CreatePromptVersionCommandHandlerTests
{
    private readonly Mock<MeepleAiDbContext> _mockDbContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly CreatePromptVersionCommandHandler _handler;
    private readonly Mock<DbSet<PromptTemplateEntity>> _mockTemplateSet;
    private readonly Mock<DbSet<PromptVersionEntity>> _mockVersionSet;
    private readonly Mock<DbSet<UserEntity>> _mockUserSet;

    public CreatePromptVersionCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockDbContext = new Mock<MeepleAiDbContext>(options);
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTemplateSet = new Mock<DbSet<PromptTemplateEntity>>();
        _mockVersionSet = new Mock<DbSet<PromptVersionEntity>>();
        _mockUserSet = new Mock<DbSet<UserEntity>>();

        _mockDbContext.Setup(db => db.Set<PromptTemplateEntity>()).Returns(_mockTemplateSet.Object);
        _mockDbContext.Setup(db => db.Set<PromptVersionEntity>()).Returns(_mockVersionSet.Object);
        _mockDbContext.Setup(db => db.Set<UserEntity>()).Returns(_mockUserSet.Object);

        _handler = new CreatePromptVersionCommandHandler(
            _mockDbContext.Object,
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
            Versions = new List<PromptVersionEntity>
            {
                new() { Id = Guid.NewGuid(), TemplateId = templateId, VersionNumber = 1, Content = "V1", CreatedByUserId = userId, CreatedAt = now.UtcDateTime, Template = null!, CreatedBy = user },
                new() { Id = Guid.NewGuid(), TemplateId = templateId, VersionNumber = 2, Content = "V2", CreatedByUserId = userId, CreatedAt = now.UtcDateTime, Template = null!, CreatedBy = user }
            }
        };

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var templateList = new List<PromptTemplateEntity> { template }.AsQueryable();
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Provider).Returns(templateList.Provider);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Expression).Returns(templateList.Expression);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.ElementType).Returns(templateList.ElementType);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.GetEnumerator()).Returns(templateList.GetEnumerator());

        _mockUserSet.Setup(s => s.FindAsync(new object[] { userId }, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

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

        _mockVersionSet.Verify(s => s.Add(It.Is<PromptVersionEntity>(v =>
            v.VersionNumber == 3 &&
            !v.IsActive &&
            v.TemplateId == templateId
        )), Times.Once);
        _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var templateList = new List<PromptTemplateEntity> { template }.AsQueryable();
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Provider).Returns(templateList.Provider);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Expression).Returns(templateList.Expression);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.ElementType).Returns(templateList.ElementType);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.GetEnumerator()).Returns(templateList.GetEnumerator());

        _mockUserSet.Setup(s => s.FindAsync(new object[] { userId }, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

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

        var templateList = new List<PromptTemplateEntity>().AsQueryable();
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Provider).Returns(templateList.Provider);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Expression).Returns(templateList.Expression);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.ElementType).Returns(templateList.ElementType);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.GetEnumerator()).Returns(templateList.GetEnumerator());

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
        _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var template = new PromptTemplateEntity
        {
            Id = templateId,
            Name = "qa-system-prompt",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null!,
            Versions = new List<PromptVersionEntity>()
        };

        var templateList = new List<PromptTemplateEntity> { template }.AsQueryable();
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Provider).Returns(templateList.Provider);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Expression).Returns(templateList.Expression);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.ElementType).Returns(templateList.ElementType);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.GetEnumerator()).Returns(templateList.GetEnumerator());

        _mockUserSet.Setup(s => s.FindAsync(new object[] { userId }, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

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
        _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
