using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]

public class BulkImportUsersCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<BulkImportUsersCommandHandler>> _mockLogger;
    private readonly BulkImportUsersCommandHandler _handler;

    public BulkImportUsersCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<BulkImportUsersCommandHandler>>();
        _handler = new BulkImportUsersCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCsv_ShouldImportUsersSuccessfully()
    {
        // Arrange
        var csvContent = @"email,displayName,role,password
user1@test.com,User One,user,Password123!
user2@test.com,User Two,admin,Password456!";

        _mockUserRepository.Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new BulkImportUsersCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();

        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyCsv_ShouldThrowDomainException()
    {
        // Arrange
        var command = new BulkImportUsersCommand("", Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Handle_WithInvalidHeader_ShouldThrowDomainException()
    {
        // Arrange
        var csvContent = @"wrong,header,columns
user1@test.com,User One,user,Password123!";

        var command = new BulkImportUsersCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("Invalid CSV header");
    }

    [Fact]
    public async Task Handle_WithDuplicateEmailsInCsv_ShouldThrowDomainException()
    {
        // Arrange
        var csvContent = @"email,displayName,role,password
duplicate@test.com,User One,user,Password123!
duplicate@test.com,User Two,user,Password456!";

        var command = new BulkImportUsersCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("duplicate emails");
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldThrowDomainException()
    {
        // Arrange
        var csvContent = @"email,displayName,role,password
existing@test.com,User One,user,Password123!";

        _mockUserRepository.Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new BulkImportUsersCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("already exist");
    }

    [Fact]
    public async Task Handle_WithTooManyUsers_ShouldThrowDomainException()
    {
        // Arrange
        var csvLines = new List<string> { "email,displayName,role,password" };
        for (int i = 0; i < 1001; i++)
        {
            csvLines.Add($"user{i}@test.com,User {i},user,Password123!");
        }
        var csvContent = string.Join("\n", csvLines);

        var command = new BulkImportUsersCommand(csvContent, Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("maximum limit of 1000");
    }

    [Fact]
    public async Task Handle_WithShortPassword_ShouldSkipInvalidRow()
    {
        // Arrange
        var csvContent = @"email,displayName,role,password
user1@test.com,User One,user,short
user2@test.com,User Two,user,ValidPassword123!";

        _mockUserRepository.Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new BulkImportUsersCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalRequested.Should().Be(1);
        result.SuccessCount.Should().Be(1);
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMissingFields_ShouldSkipInvalidRows()
    {
        // Arrange
        var csvContent = @"email,displayName,role,password
,User One,user,Password123!
user2@test.com,,user,Password456!
user3@test.com,User Three,,Password789!
user4@test.com,User Four,user,ValidPassword!";

        _mockUserRepository.Setup(r => r.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new BulkImportUsersCommand(csvContent, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.SuccessCount.Should().Be(1);
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}