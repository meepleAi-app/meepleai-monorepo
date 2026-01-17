using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.SystemConfiguration.Handlers;

public sealed class CreateAiModelConfigCommandHandlerTests
{
    private readonly Mock<IAiModelConfigurationRepository> _repositoryMock;
    private readonly Mock<MeepleAiDbContext> _dbMock;
    private readonly CreateAiModelConfigCommandHandler _handler;

    public CreateAiModelConfigCommandHandlerTests()
    {
        _repositoryMock = new Mock<IAiModelConfigurationRepository>();
        _dbMock = new Mock<MeepleAiDbContext>();
        _handler = new CreateAiModelConfigCommandHandler(_repositoryMock.Object, _dbMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesModelAndReturnsDto()
    {
        // Arrange
        var command = new CreateAiModelConfigCommand(
            "gpt-4o-mini",
            "GPT-4o Mini",
            "OpenRouter",
            Priority: 1,
            Settings: new ModelSettingsDto { MaxTokens = 4096, Temperature = 0.7 },
            Pricing: new ModelPricingDto { InputPricePerMillion = 0.15m, OutputPricePerMillion = 0.6m });

        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<AiModelConfiguration>(), default))
            .Returns(Task.CompletedTask);
        _dbMock.Setup(db => db.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.ModelId.Should().Be("gpt-4o-mini");
        result.DisplayName.Should().Be("GPT-4o Mini");
        result.Provider.Should().Be("OpenRouter");
        result.Priority.Should().Be(1);
        result.Settings.MaxTokens.Should().Be(4096);
        result.Pricing.InputPricePerMillion.Should().Be(0.15m);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<AiModelConfiguration>(), default), Times.Once);
        _dbMock.Verify(db => db.SaveChangesAsync(default), Times.Once);
    }
}
