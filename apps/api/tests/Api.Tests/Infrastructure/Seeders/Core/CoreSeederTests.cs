using Api.Infrastructure.Seeders.Core;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Core;

[Trait("Category", TestCategories.Unit)]
public sealed class CoreSeederTests
{
    [Fact]
    public async Task AdminUserSeeder_SeedAsync_SendsSeedAdminUserCommand()
    {
        // Arrange
        var mediator = new Mock<IMediator>();
        var logger = new Mock<ILogger>();

        // Act
        await AdminUserSeeder.SeedAsync(mediator.Object, logger.Object, CancellationToken.None);

        // Assert
        mediator.Verify(m => m.Send(
            It.IsAny<Api.BoundedContexts.Administration.Application.Commands.SeedAdminUserCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiModelSeeder_SeedAsync_SendsSeedAiModelsCommand()
    {
        // Arrange
        var mediator = new Mock<IMediator>();
        var logger = new Mock<ILogger>();

        // Act
        await AiModelSeeder.SeedAsync(mediator.Object, logger.Object, CancellationToken.None);

        // Assert
        mediator.Verify(m => m.Send(
            It.IsAny<Api.BoundedContexts.SystemConfiguration.Application.Commands.SeedAiModelsCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdminUserSeeder_SeedAsync_PropagatesException()
    {
        // Arrange
        var mediator = new Mock<IMediator>();
        var logger = new Mock<ILogger>();

        mediator.Setup(m => m.Send(
            It.IsAny<Api.BoundedContexts.Administration.Application.Commands.SeedAdminUserCommand>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Admin secret not found"));

        // Act
        var act = () => AdminUserSeeder.SeedAsync(mediator.Object, logger.Object, CancellationToken.None);

        // Assert - fatal seeder should propagate exceptions
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Admin secret not found");
    }
}
