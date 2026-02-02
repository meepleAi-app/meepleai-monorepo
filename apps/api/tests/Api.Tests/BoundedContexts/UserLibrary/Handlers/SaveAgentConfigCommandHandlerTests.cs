using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
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
    private readonly Mock<IAgentTypologyRepository> _mockTypologyRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<SaveAgentConfigCommandHandler>> _mockLogger;
    private readonly SaveAgentConfigCommandHandler _handler;

    public SaveAgentConfigCommandHandlerTests()
    {
        _mockLibraryRepo = new Mock<IUserLibraryRepository>();
        _mockTypologyRepo = new Mock<IAgentTypologyRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<SaveAgentConfigCommandHandler>>();

        _handler = new SaveAgentConfigCommandHandler(
            _mockLibraryRepo.Object,
            _mockTypologyRepo.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesConfigSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();

        var typology = new AgentTypology(
            typologyId,
            "Rules Expert",
            "Expert in rules",
            "You are a rules expert",
            AgentStrategy.HybridSearch(),
            userId,
            TypologyStatus.Approved
        );

        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _mockTypologyRepo
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new SaveAgentConfigCommand(
            userId,
            gameId,
            typologyId,
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
    public async Task Handle_TypologyNotFound_ThrowsDomainException()
    {
        // Arrange
        var command = new SaveAgentConfigCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "GPT-4o",
            0.005
        );

        _mockTypologyRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentTypology?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_TypologyNotApproved_ThrowsConflictException()
    {
        // Arrange
        var typologyId = Guid.NewGuid();
        var typology = new AgentTypology(
            typologyId,
            "Draft Typology",
            "Not approved yet",
            "Draft prompt",
            AgentStrategy.HybridSearch(),
            Guid.NewGuid(),
            TypologyStatus.Draft
        );

        _mockTypologyRepo
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new SaveAgentConfigCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            typologyId,
            "GPT-4o",
            0.005
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        exception.Message.Should().Contain("not approved");
    }

    [Fact]
    public async Task Handle_LibraryEntryNotExists_CreatesNewEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();

        var typology = new AgentTypology(
            typologyId,
            "Quick Start",
            "Setup help",
            "Help with setup",
            AgentStrategy.HybridSearch(),
            userId,
            TypologyStatus.Approved
        );

        _mockTypologyRepo
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

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
            typologyId,
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
        var typologyId = Guid.NewGuid();

        var typology = new AgentTypology(
            typologyId,
            "Test Typology",
            "Test",
            "Test prompt",
            AgentStrategy.HybridSearch(),
            userId,
            TypologyStatus.Approved
        );

        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _mockTypologyRepo
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

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
                typologyId,
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
