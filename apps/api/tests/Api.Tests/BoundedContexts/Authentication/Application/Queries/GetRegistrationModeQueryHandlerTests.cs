using Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class GetRegistrationModeQueryHandlerTests
{
    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly GetRegistrationModeQueryHandler _handler;

    public GetRegistrationModeQueryHandlerTests()
    {
        _configServiceMock = new Mock<IConfigurationService>();
        _handler = new GetRegistrationModeQueryHandler(_configServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenPublicRegistrationEnabled_ReturnsTrue()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync(true);

        var result = await _handler.Handle(new GetRegistrationModeQuery(), CancellationToken.None);

        result.PublicRegistrationEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenPublicRegistrationDisabled_ReturnsFalse()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new GetRegistrationModeQuery(), CancellationToken.None);

        result.PublicRegistrationEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenConfigNotFound_ReturnsFalse_FailClosed()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync((bool?)null);

        var result = await _handler.Handle(new GetRegistrationModeQuery(), CancellationToken.None);

        result.PublicRegistrationEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenConfigServiceThrows_ReturnsFalse_FailClosed()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var result = await _handler.Handle(new GetRegistrationModeQuery(), CancellationToken.None);

        result.PublicRegistrationEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsRegistrationModeDto()
    {
        _configServiceMock
            .Setup(x => x.GetValueAsync<bool?>("Registration:PublicEnabled", false, null))
            .ReturnsAsync(true);

        var result = await _handler.Handle(new GetRegistrationModeQuery(), CancellationToken.None);

        result.Should().BeOfType<RegistrationModeDto>();
    }
}
