using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Administration.Handlers;

/// <summary>
/// Unit tests for GetUserAiConsentQueryHandler (Issue #5512)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class GetUserAiConsentQueryHandlerTests
{
    private readonly Mock<IUserAiConsentRepository> _consentRepoMock;
    private readonly GetUserAiConsentQueryHandler _handler;

    public GetUserAiConsentQueryHandlerTests()
    {
        _consentRepoMock = new Mock<IUserAiConsentRepository>();
        _handler = new GetUserAiConsentQueryHandler(_consentRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ExistingConsent_ShouldReturnDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var consent = UserAiConsent.Create(userId, true, false, "1.0.0");
        _consentRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consent);

        var query = new GetUserAiConsentQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.ConsentedToAiProcessing.Should().BeTrue();
        result.ConsentedToExternalProviders.Should().BeFalse();
        result.ConsentVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Handle_NoConsent_ShouldReturnNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _consentRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAiConsent?)null);

        var query = new GetUserAiConsentQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
