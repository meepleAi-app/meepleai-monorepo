using Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class EncryptionServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly Mock<ILogger<EncryptionService>> _mockLogger;
    private readonly EncryptionService _service;

    public EncryptionServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Use EphemeralDataProtectionProvider for testing (in-memory, no persistence)
        _dataProtectionProvider = new EphemeralDataProtectionProvider();
        _mockLogger = new Mock<ILogger<EncryptionService>>();

        _service = new EncryptionService(_dataProtectionProvider, _mockLogger.Object);
    }

    [Fact]
    public async Task EncryptAsync_ValidPlaintext_ReturnsEncrypted()
    {
        // Arrange
        var plaintext = "secret-token-12345";

        // Act
        var result = await _service.EncryptAsync(plaintext);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().NotBe(plaintext); // Encrypted should be different from plaintext
    }

    [Fact]
    public async Task EncryptAsync_WithCustomPurpose_CanDecrypt()
    {
        // Arrange
        var plaintext = "sensitive-data";
        var purpose = "CustomPurpose";

        // Act
        var encrypted = await _service.EncryptAsync(plaintext, purpose);
        var decrypted = await _service.DecryptAsync(encrypted, purpose);

        // Assert
        decrypted.Should().Be(plaintext); // Can decrypt with same purpose
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EncryptAsync_NullOrEmptyPlaintext_ThrowsArgumentException(string? plaintext)
    {
        // Act & Assert
        await FluentActions.Invoking(() => _service.EncryptAsync(plaintext!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DecryptAsync_ValidCiphertext_ReturnsDecrypted()
    {
        // Arrange
        var originalPlaintext = "original-secret";
        var ciphertext = await _service.EncryptAsync(originalPlaintext);

        // Act
        var result = await _service.DecryptAsync(ciphertext);

        // Assert
        result.Should().Be(originalPlaintext);
    }

    [Fact]
    public async Task DecryptAsync_WithWrongPurpose_ThrowsException()
    {
        // Arrange
        var plaintext = "sensitive-data";
        var encryptPurpose = "EncryptPurpose";
        var decryptPurpose = "WrongPurpose";
        var ciphertext = await _service.EncryptAsync(plaintext, encryptPurpose);

        // Act & Assert
        await FluentActions.Invoking(() => _service.DecryptAsync(ciphertext, decryptPurpose))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task DecryptAsync_NullOrEmptyCiphertext_ThrowsArgumentException(string? ciphertext)
    {
        // Act & Assert
        await FluentActions.Invoking(() => _service.DecryptAsync(ciphertext!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DecryptAsync_InvalidCiphertext_ThrowsInvalidOperationException()
    {
        // Arrange
        var ciphertext = "corrupted-data-not-base64";

        // Act & Assert
        var ex = await FluentActions.Invoking(() => _service.DecryptAsync(ciphertext))
            .Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Decryption operation failed");
    }

    [Fact]
    public async Task EncryptAsync_DefaultPurpose_CanDecryptWithDefaultPurpose()
    {
        // Arrange
        var plaintext = "test-data";

        // Act - Encrypt with default purpose (no parameter)
        var encrypted = await _service.EncryptAsync(plaintext);

        // Act - Decrypt with default purpose (no parameter)
        var decrypted = await _service.DecryptAsync(encrypted);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public async Task EncryptDecrypt_RoundTrip_PreservesData()
    {
        // Arrange
        var originalData = "This is a secret message with special chars: !@#$%^&*()";

        // Act
        var encrypted = await _service.EncryptAsync(originalData);
        var decrypted = await _service.DecryptAsync(encrypted);

        // Assert
        decrypted.Should().Be(originalData);
        encrypted.Should().NotBe(originalData); // Verify it was actually encrypted
    }
}