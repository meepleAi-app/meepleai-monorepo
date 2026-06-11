using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="SetRegistrationModeCommandHandler"/>.
/// Regression test for issue #2116: hardcoded Environment="Production" in Create branch
/// caused 23505 duplicate key violation when current environment != "Production".
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class SetRegistrationModeCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly SetRegistrationModeCommandHandler _handler;

    public SetRegistrationModeCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockConfigService = new Mock<IConfigurationService>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();

        _handler = new SetRegistrationModeCommandHandler(
            _mockMediator.Object,
            _mockConfigService.Object,
            _mockEnvironment.Object);
    }

    [Fact]
    public async Task Handle_InDevelopmentEnv_NoExistingConfig_CreatesConfigWithCurrentEnvironment()
    {
        // Bug #2116: when running in Development and no row exists in DB,
        // the Create branch must use the current environment (Development), NOT hardcode "Production".
        // Otherwise INSERT (Key=Registration:PublicEnabled, Environment=Production) collides with
        // any seed row already present at Environment="Production" -> 23505 duplicate key.
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        _mockConfigService
            .Setup(c => c.GetConfigurationByKeyAsync(
                "Registration:PublicEnabled",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockConfigDto("Development"));

        var adminId = Guid.NewGuid();
        var cmd = new SetRegistrationModeCommand(true, adminId);

        await _handler.Handle(cmd, TestContext.Current.CancellationToken);

        _mockMediator.Verify(
            m => m.Send(
                It.Is<CreateConfigurationCommand>(c =>
                    c.Key == "Registration:PublicEnabled" &&
                    c.Value == "true" &&
                    c.Environment == "Development" &&
                    c.CreatedByUserId == adminId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InProductionEnv_NoExistingConfig_CreatesConfigWithProductionEnvironment()
    {
        // Regression guard: prevent re-introducing a hardcoded string literal.
        // Production environment must be derived from IWebHostEnvironment, not from a literal.
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Production");
        _mockConfigService
            .Setup(c => c.GetConfigurationByKeyAsync(
                "Registration:PublicEnabled",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockConfigDto("Production"));

        var adminId = Guid.NewGuid();
        var cmd = new SetRegistrationModeCommand(false, adminId);

        await _handler.Handle(cmd, TestContext.Current.CancellationToken);

        _mockMediator.Verify(
            m => m.Send(
                It.Is<CreateConfigurationCommand>(c =>
                    c.Value == "false" &&
                    c.Environment == "Production"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InDevelopmentEnv_ExistingConfig_SendsUpdateAndNotCreate()
    {
        // Idempotency guard for #2116: repeated toggles must hit the Update path, not duplicate-key.
        // After the first call writes a row at Environment="Development", the second call must find it
        // and Update it - it must NOT enter the Create branch and re-insert under a different Environment.
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        var existingId = Guid.NewGuid();
        var existingDto = new SystemConfigurationDto(
            Id: existingId.ToString(),
            Key: "Registration:PublicEnabled",
            Value: "false",
            ValueType: "Boolean",
            Description: "Controls whether public registration is enabled",
            Category: "Authentication",
            IsActive: true,
            RequiresRestart: false,
            Environment: "Development",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: Guid.Empty.ToString(),
            UpdatedByUserId: null,
            LastToggledAt: null);

        _mockConfigService
            .Setup(c => c.GetConfigurationByKeyAsync(
                "Registration:PublicEnabled",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockConfigDto("Development"));

        var adminId = Guid.NewGuid();
        var cmd = new SetRegistrationModeCommand(true, adminId);

        await _handler.Handle(cmd, TestContext.Current.CancellationToken);

        _mockMediator.Verify(
            m => m.Send(
                It.Is<UpdateConfigValueCommand>(c =>
                    c.ConfigId == existingId &&
                    c.NewValue == "true" &&
                    c.UpdatedByUserId == adminId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ConfigurationDto CreateMockConfigDto(string environment) =>
        new(
            Id: Guid.NewGuid(),
            Key: "Registration:PublicEnabled",
            Value: "true",
            ValueType: "Boolean",
            Description: "Controls whether public registration is enabled",
            Category: "Authentication",
            IsActive: true,
            RequiresRestart: false,
            Environment: environment,
            Version: 1,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);
}
