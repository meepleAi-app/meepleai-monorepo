using Api.Services;
using System;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.Services;

[Trait("Category", TestCategories.Unit)]

public class PasswordHashingServiceTests
{
    private readonly PasswordHashingService _service = new();

    [Fact]
    public void HashSecret_ProducesVersionedFormat()
    {
        // Act
        var hash = _service.HashSecret("Secret123!");

        // Assert
        hash.Should().StartWithEquivalentOf("v1.");
        hash.Should().Contain(".");
    }

    [Fact]
    public void VerifySecret_WithVersionedHash_ReturnsTrue()
    {
        // Arrange
        var hash = _service.HashSecret("Secret123!");

        // Act
        var result = _service.VerifySecret("Secret123!", hash);

        // Assert
        result.Should().BeTrue();
    }

}

