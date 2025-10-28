using Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class EncryptionServiceTests
{
    private readonly Mock<IDataProtectionProvider> _mockDataProtectionProvider;
    private readonly Mock<IDataProtector> _mockDataProtector;
    private readonly Mock<ILogger<EncryptionService>> _mockLogger;
    private readonly EncryptionService _service;

    public EncryptionServiceTests()
    {
        _mockDataProtectionProvider = new Mock<IDataProtectionProvider>();
        _mockDataProtector = new Mock<IDataProtector>();
        _mockLogger = new Mock<ILogger<EncryptionService>>();

        _mockDataProtectionProvider
            .Setup(p => p.CreateProtector(It.IsAny<string>()))
            .Returns(_mockDataProtector.Object);

        _service = new EncryptionService(_mockDataProtectionProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task EncryptAsync_ValidPlaintext_ReturnsEncrypted()
    {
        // Arrange
        var plaintext = "secret-token-12345";
        var encrypted = "encrypted-data-xyz";
        _mockDataProtector.Setup(p => p.Protect(plaintext)).Returns(encrypted);

        // Act
        var result = await _service.EncryptAsync(plaintext);

        // Assert
        Assert.Equal(encrypted, result);
        _mockDataProtector.Verify(p => p.Protect(plaintext), Times.Once);
    }

    [Fact]
    public async Task EncryptAsync_WithCustomPurpose_UsesCorrectProtector()
    {
        // Arrange
        var plaintext = "sensitive-data";
        var purpose = "CustomPurpose";
        _mockDataProtector.Setup(p => p.Protect(It.IsAny<string>())).Returns("encrypted");

        // Act
        await _service.EncryptAsync(plaintext, purpose);

        // Assert
        _mockDataProtectionProvider.Verify(p => p.CreateProtector(purpose), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EncryptAsync_NullOrEmptyPlaintext_ThrowsArgumentException(string? plaintext)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.EncryptAsync(plaintext!));
    }

    [Fact]
    public async Task DecryptAsync_ValidCiphertext_ReturnsDecrypted()
    {
        // Arrange
        var ciphertext = "encrypted-data";
        var decrypted = "original-secret";
        _mockDataProtector.Setup(p => p.Unprotect(ciphertext)).Returns(decrypted);

        // Act
        var result = await _service.DecryptAsync(ciphertext);

        // Assert
        Assert.Equal(decrypted, result);
        _mockDataProtector.Verify(p => p.Unprotect(ciphertext), Times.Once);
    }

    [Fact]
    public async Task DecryptAsync_WithCustomPurpose_UsesCorrectProtector()
    {
        // Arrange
        var ciphertext = "encrypted";
        var purpose = "TestPurpose";
        _mockDataProtector.Setup(p => p.Unprotect(It.IsAny<string>())).Returns("decrypted");

        // Act
        await _service.DecryptAsync(ciphertext, purpose);

        // Assert
        _mockDataProtectionProvider.Verify(p => p.CreateProtector(purpose), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task DecryptAsync_NullOrEmptyCiphertext_ThrowsArgumentException(string? ciphertext)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DecryptAsync(ciphertext!));
    }

    [Fact]
    public async Task DecryptAsync_InvalidCiphertext_ThrowsInvalidOperationException()
    {
        // Arrange
        var ciphertext = "corrupted-data";
        _mockDataProtector.Setup(p => p.Unprotect(ciphertext))
            .Throws(new System.Security.Cryptography.CryptographicException("Decryption failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DecryptAsync(ciphertext));
        Assert.Contains("Decryption operation failed", ex.Message);
    }

    [Fact]
    public async Task EncryptAsync_DefaultPurpose_UsesDefaultString()
    {
        // Arrange
        var plaintext = "test";
        _mockDataProtector.Setup(p => p.Protect(It.IsAny<string>())).Returns("encrypted");

        // Act
        await _service.EncryptAsync(plaintext); // No purpose parameter

        // Assert
        _mockDataProtectionProvider.Verify(p => p.CreateProtector("default"), Times.Once);
    }

    [Fact]
    public async Task DecryptAsync_DefaultPurpose_UsesDefaultString()
    {
        // Arrange
        var ciphertext = "encrypted";
        _mockDataProtector.Setup(p => p.Unprotect(It.IsAny<string>())).Returns("decrypted");

        // Act
        await _service.DecryptAsync(ciphertext); // No purpose parameter

        // Assert
        _mockDataProtectionProvider.Verify(p => p.CreateProtector("default"), Times.Once);
    }
}
