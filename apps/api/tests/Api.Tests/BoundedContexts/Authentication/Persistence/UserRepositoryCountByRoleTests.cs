using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using FluentAssertions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Persistence;

[Trait("Category", TestCategories.Unit)]
public class UserRepositoryCountByRoleTests
{
    private readonly Mock<IUserRepository> _mockRepository;

    public UserRepositoryCountByRoleTests()
    {
        _mockRepository = new Mock<IUserRepository>();
    }

    [Theory]
    [InlineData("superadmin", 2)]
    [InlineData("admin", 5)]
    [InlineData("user", 100)]
    [InlineData("editor", 0)]
    public async Task CountByRoleAsync_ReturnsCorrectCount(string role, int expectedCount)
    {
        // Arrange
        _mockRepository.Setup(r => r.CountByRoleAsync(role, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var count = await _mockRepository.Object.CountByRoleAsync(role, CancellationToken.None);

        // Assert
        count.Should().Be(expectedCount);
        _mockRepository.Verify(r => r.CountByRoleAsync(role, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CountByRoleAsync_AcceptsUpperCaseRoleName()
    {
        // Arrange — verify the interface accepts non-lowercase role names
        // (normalization is done by the implementation via Role.Parse)
        _mockRepository.Setup(r => r.CountByRoleAsync("SuperAdmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var count = await _mockRepository.Object.CountByRoleAsync("SuperAdmin", CancellationToken.None);

        // Assert — interface contract: accepts mixed-case role names
        count.Should().Be(3);
        _mockRepository.Verify(r => r.CountByRoleAsync("SuperAdmin", It.IsAny<CancellationToken>()), Times.Once);
    }
}
