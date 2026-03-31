using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using AgentDef = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Handlers;

/// <summary>
/// Tests for SaveAgentConfigCommandHandler (Issue #3212)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class SaveAgentConfigCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo;
    private readonly Mock<IAgentDefinitionRepository> _mockDefinitionRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<SaveAgentConfigCommandHandler>> _mockLogger;
    private readonly SaveAgentConfigCommandHandler _handler;

    public SaveAgentConfigCommandHandlerTests()
    {
        _mockLibraryRepo = new Mock<IUserLibraryRepository>();
        _mockDefinitionRepo = new Mock<IAgentDefinitionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<SaveAgentConfigCommandHandler>>();

        _handler = new SaveAgentConfigCommandHandler(
            _mockLibraryRepo.Object,
            _mockDefinitionRepo.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    private static AgentDef CreateActiveDefinition(Guid id, string name)
    {
        var def = AgentDef.Create(
            name,
            "Test description",
            AgentType.RulesInterpreter,
            AgentDefinitionConfig.Default());
        def.Activate();
        return def;
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesConfigSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        var definition = CreateActiveDefinition(definitionId, "Rules Expert");
        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _mockDefinitionRepo
            .Setup(r => r.GetByIdAsync(definitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new SaveAgentConfigCommand(
            userId,
            gameId,
            definitionId,
            "GPT-4o-mini",
            0.001
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ConfigId.Should().Be(libraryEntry.Id);
        result.Message.Should().Contain("successfully");

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_DefinitionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var command = new SaveAgentConfigCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "GPT-4o",
            0.005
        );

        _mockDefinitionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDef?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DefinitionNotActive_ThrowsConflictException()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var inactiveDef = AgentDef.Create(
            "Inactive Definition",
            "Not active",
            AgentType.RulesInterpreter,
            AgentDefinitionConfig.Default());
        // not Activate() → IsActive=false

        _mockDefinitionRepo
            .Setup(r => r.GetByIdAsync(definitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveDef);

        var command = new SaveAgentConfigCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            definitionId,
            "GPT-4o",
            0.005
        );

        // Act & Assert
        var act2 = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act2.Should().ThrowAsync<ConflictException>()).Which;

        exception.Message.Should().Contain("not active");
    }

    [Fact]
    public async Task Handle_LibraryEntryNotExists_CreatesNewEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        var definition = CreateActiveDefinition(definitionId, "Quick Start");

        _mockDefinitionRepo
            .Setup(r => r.GetByIdAsync(definitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        _mockLibraryRepo
            .Setup(r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new SaveAgentConfigCommand(
            userId,
            gameId,
            definitionId,
            "Claude-3.5-Haiku",
            0.003
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();

        _mockLibraryRepo.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WithAllModels_MapsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        var definition = CreateActiveDefinition(definitionId, "Test Definition");
        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _mockDefinitionRepo
            .Setup(r => r.GetByIdAsync(definitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var testModels = new[]
        {
            ("GPT-4o-mini", 0.001),
            ("Llama-3.3-70b", 0.0008),
            ("Claude-3.5-Haiku", 0.003),
            ("GPT-4o", 0.005)
        };

        foreach (var (modelName, cost) in testModels)
        {
            var command = new SaveAgentConfigCommand(
                userId,
                gameId,
                definitionId,
                modelName,
                cost
            );

            // Act
            var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue();
            libraryEntry.CustomAgentConfig.Should().NotBeNull();
            libraryEntry.CustomAgentConfig!.LlmModel.Should().Be(modelName);
        }
    }
}
