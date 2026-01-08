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
public class CreatePromptTemplateCommandHandlerTests
{
    private readonly Mock<MeepleAiDbContext> _mockDbContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly CreatePromptTemplateCommandHandler _handler;
    private readonly Mock<DbSet<PromptTemplateEntity>> _mockTemplateSet;
    private readonly Mock<DbSet<PromptVersionEntity>> _mockVersionSet;
    private readonly Mock<DbSet<UserEntity>> _mockUserSet;

    public CreatePromptTemplateCommandHandlerTests()
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

        _handler = new CreatePromptTemplateCommandHandler(
            _mockDbContext.Object,
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

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var templateList = new List<PromptTemplateEntity>().AsQueryable();
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Provider).Returns(templateList.Provider);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Expression).Returns(templateList.Expression);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.ElementType).Returns(templateList.ElementType);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.GetEnumerator()).Returns(templateList.GetEnumerator());

        _mockUserSet.Setup(s => s.FindAsync(new object[] { userId }, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

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

        _mockTemplateSet.Verify(s => s.Add(It.Is<PromptTemplateEntity>(t => t.Name == "qa-system-prompt")), Times.Once);
        _mockVersionSet.Verify(s => s.Add(It.Is<PromptVersionEntity>(v =>
            v.VersionNumber == 1 &&
            v.IsActive &&
            v.Content == "You are a board game rules assistant."
        )), Times.Once);
        _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingTemplate = new PromptTemplateEntity
        {
            Id = Guid.NewGuid(),
            Name = "qa-system-prompt",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null!
        };

        var templateList = new List<PromptTemplateEntity> { existingTemplate }.AsQueryable();
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Provider).Returns(templateList.Provider);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Expression).Returns(templateList.Expression);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.ElementType).Returns(templateList.ElementType);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.GetEnumerator()).Returns(templateList.GetEnumerator());

        var command = new CreatePromptTemplateCommand(
            Name: "qa-system-prompt",
            Description: "Duplicate template",
            Category: "qa",
            InitialContent: "Content",
            Metadata: null,
            CreatedByUserId: userId
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("already exists");
        _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var templateList = new List<PromptTemplateEntity>().AsQueryable();
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Provider).Returns(templateList.Provider);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.Expression).Returns(templateList.Expression);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.ElementType).Returns(templateList.ElementType);
        _mockTemplateSet.As<IQueryable<PromptTemplateEntity>>().Setup(m => m.GetEnumerator()).Returns(templateList.GetEnumerator());

        _mockUserSet.Setup(s => s.FindAsync(new object[] { userId }, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        var command = new CreatePromptTemplateCommand(
            Name: "qa-system-prompt",
            Description: "Test",
            Category: "qa",
            InitialContent: "Content",
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
