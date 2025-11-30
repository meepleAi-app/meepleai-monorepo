using Api.Services;
using System;
using Xunit;

namespace Api.Tests.Services;

public class PasswordHashingServiceTests
{
    private readonly PasswordHashingService _service = new();

    [Fact]
    public void HashSecret_ProducesVersionedFormat()
    {
        // Act
        var hash = _service.HashSecret("Secret123!");

        // Assert
        Assert.StartsWith("v1.", hash, StringComparison.OrdinalIgnoreCase);
        Assert.Contains('.', hash);
    }

    [Fact]
    public void VerifySecret_WithVersionedHash_ReturnsTrue()
    {
        // Arrange
        var hash = _service.HashSecret("Secret123!");

        // Act
        var result = _service.VerifySecret("Secret123!", hash);

        // Assert
        Assert.True(result);
    }

}

