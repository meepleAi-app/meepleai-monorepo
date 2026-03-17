using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;
using MediatRUnit = MediatR.Unit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class SetRegistrationModeCommandHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly SetRegistrationModeCommandHandler _handler;
    private readonly Guid _adminId = Guid.NewGuid();

    public SetRegistrationModeCommandHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _configServiceMock = new Mock<IConfigurationService>();
        _handler = new SetRegistrationModeCommandHandler(
            _mediatorMock.Object,
            _configServiceMock.Object);
    }

    private static SystemConfigurationDto CreateSystemConfigDto(
        string id, string value) =>
        new(id, "Registration:PublicEnabled", value,
            "Boolean", "Controls registration", "Authentication",
            true, false, "Production", 1, null,
            DateTime.UtcNow, DateTime.UtcNow,
            Guid.NewGuid().ToString(), null, null);

    private static ConfigurationDto CreateConfigDto(
        Guid id, string value, int version = 1) =>
        new(id, "Registration:PublicEnabled", value,
            "Boolean", "Controls registration", "Authentication",
            true, false, "Production", version,
            DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task Handle_WhenConfigExists_UpdatesExistingConfig()
    {
        var configId = Guid.NewGuid();
        var existingConfig = CreateSystemConfigDto(configId.ToString(), "false");

        _configServiceMock
            .Setup(x => x.GetConfigurationByKeyAsync(
                "Registration:PublicEnabled", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(configId, "true", 2));

        var command = new SetRegistrationModeCommand(true, _adminId);
        await _handler.Handle(command, CancellationToken.None);

        _mediatorMock.Verify(x => x.Send(
            It.Is<UpdateConfigValueCommand>(c =>
                c.ConfigId == configId &&
                c.NewValue == "true" &&
                c.UpdatedByUserId == _adminId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenConfigDoesNotExist_CreatesNewConfig()
    {
        _configServiceMock
            .Setup(x => x.GetConfigurationByKeyAsync(
                "Registration:PublicEnabled", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(Guid.NewGuid(), "true"));

        var command = new SetRegistrationModeCommand(true, _adminId);
        await _handler.Handle(command, CancellationToken.None);

        _mediatorMock.Verify(x => x.Send(
            It.Is<CreateConfigurationCommand>(c =>
                c.Key == "Registration:PublicEnabled" &&
                c.Value == "true" &&
                c.ValueType == "Boolean" &&
                c.Category == "Authentication" &&
                c.Environment == "Production" &&
!c.RequiresRestart &&
                c.CreatedByUserId == _adminId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EnabledTrue_SetsValueToLowercaseTrue()
    {
        _configServiceMock
            .Setup(x => x.GetConfigurationByKeyAsync(
                "Registration:PublicEnabled", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(Guid.NewGuid(), "true"));

        var command = new SetRegistrationModeCommand(true, _adminId);
        await _handler.Handle(command, CancellationToken.None);

        _mediatorMock.Verify(x => x.Send(
            It.Is<CreateConfigurationCommand>(c => c.Value == "true"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EnabledFalse_SetsValueToLowercaseFalse()
    {
        var configId = Guid.NewGuid();
        var existingConfig = CreateSystemConfigDto(configId.ToString(), "true");

        _configServiceMock
            .Setup(x => x.GetConfigurationByKeyAsync(
                "Registration:PublicEnabled", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(configId, "false", 2));

        var command = new SetRegistrationModeCommand(false, _adminId);
        await _handler.Handle(command, CancellationToken.None);

        _mediatorMock.Verify(x => x.Send(
            It.Is<UpdateConfigValueCommand>(c => c.NewValue == "false"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsUnit()
    {
        _configServiceMock
            .Setup(x => x.GetConfigurationByKeyAsync(
                "Registration:PublicEnabled", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _mediatorMock
            .Setup(x => x.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigDto(Guid.NewGuid(), "false"));

        var command = new SetRegistrationModeCommand(false, _adminId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(MediatRUnit.Value);
    }
}
