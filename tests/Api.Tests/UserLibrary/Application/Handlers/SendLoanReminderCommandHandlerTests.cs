using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Handlers;

public sealed class SendLoanReminderCommandHandlerTests
{
    [Fact]
    public void Constructor_WithNullLibraryRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var mockNotificationRepo = new Mock<INotificationRepository>();
        var mockUow = new Mock<IUnitOfWork>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SendLoanReminderCommandHandler(null!, mockNotificationRepo.Object, mockUow.Object, NullLogger<SendLoanReminderCommandHandler>.Instance));
    }

    [Fact]
    public void Constructor_WithNullNotificationRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLibraryRepo = new Mock<IUserLibraryRepository>();
        var mockUow = new Mock<IUnitOfWork>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SendLoanReminderCommandHandler(mockLibraryRepo.Object, null!, mockUow.Object, NullLogger<SendLoanReminderCommandHandler>.Instance));
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var mockLibraryRepo = new Mock<IUserLibraryRepository>();
        var mockNotificationRepo = new Mock<INotificationRepository>();
        var mockUow = new Mock<IUnitOfWork>();

        // Act
        var handler = new SendLoanReminderCommandHandler(
            mockLibraryRepo.Object,
            mockNotificationRepo.Object,
            mockUow.Object,
            NullLogger<SendLoanReminderCommandHandler>.Instance);

        // Assert
        handler.Should().NotBeNull();
    }
}
