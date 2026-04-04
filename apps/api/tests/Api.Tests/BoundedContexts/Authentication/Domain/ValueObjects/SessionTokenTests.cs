using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class SessionTokenTests
{
    [Fact]
    public void Generate_CreatesValidToken()
    {
        // Act
        var token = SessionToken.Generate();

        // Assert
        token.Should().NotBeNull();
        token.Value.Should().NotBeNull();
        token.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_CreatesBase64EncodedToken()
    {
        // Act
        var token = SessionToken.Generate();

        // Assert - Base64 string should decode without exception
        var isValidBase64 = TryDecodeBase64(token.Value, out var bytes);
        isValidBase64.Should().BeTrue();
        bytes.Should().NotBeNull();
        bytes.Length.Should().Be(32); // 32 bytes = 256 bits
    }

    [Fact]
    public void Generate_CreatesTokenWithCorrectLength()
    {
        // Act
        var token = SessionToken.Generate();

        // Assert - Base64 encoding of 32 bytes should be 44 characters (with padding)
        token.Value.Length.Should().Be(44);
    }

    [Fact]
    public void Generate_CreatesUniqueTokens()
    {
        // Act
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();
        var token3 = SessionToken.Generate();

        // Assert
        token2.Value.Should().NotBe(token1.Value);
        token3.Value.Should().NotBe(token1.Value);
        token3.Value.Should().NotBe(token2.Value);
    }

    [Fact]
    public void Generate_MultipleInvocations_ProduceDifferentTokens()
    {
        // Arrange
        var tokens = new HashSet<string>();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var token = SessionToken.Generate();
            tokens.Add(token.Value);
        }

        // Assert - All tokens should be unique
        tokens.Count.Should().Be(iterations);
    }

    [Fact]
    public void FromStored_WithValidToken_ReconstructsSuccessfully()
    {
        // Arrange
        var originalToken = SessionToken.Generate();
        var storedValue = originalToken.Value;

        // Act
        var reconstructedToken = SessionToken.FromStored(storedValue);

        // Assert
        reconstructedToken.Value.Should().Be(originalToken.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromStored_WithEmptyValue_ThrowsValidationException(string invalidValue)
    {
        // Act & Assert
        var act = () => SessionToken.FromStored(invalidValue);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("Session token cannot be empty");
    }

    [Theory]
    [InlineData("invalid-token-format")]        // hyphens not in Base-64 alphabet
    [InlineData("not!base64@value")]            // special characters
    [InlineData("AAAA====BBBB")]                // invalid padding placement
    public void FromStored_WithNonBase64Value_ThrowsValidationException(string invalidValue)
    {
        // Act & Assert
        // A cookie that cannot be Base-64 decoded is a corrupted or foreign token;
        // reject it at construction time rather than crashing later in ComputeHash().
        var act = () => SessionToken.FromStored(invalidValue);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("not a valid format");
    }

    [Fact]
    public void ComputeHash_ReturnsSHA256Hash()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var hash = token.ComputeHash();

        // Assert
        hash.Should().NotBeNull();
        hash.Should().NotBeEmpty();

        // SHA256 produces 32 bytes, which Base64 encodes to 44 characters
        hash.Length.Should().Be(44);

        // Verify it's valid Base64
        TryDecodeBase64(hash, out var hashBytes).Should().BeTrue();
        hashBytes!.Length.Should().Be(32); // 256 bits = 32 bytes
    }

    [Fact]
    public void ComputeHash_SameToken_ProducesSameHash()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var hash1 = token.ComputeHash();
        var hash2 = token.ComputeHash();

        // Assert
        hash2.Should().Be(hash1);
    }

    [Fact]
    public void ComputeHash_DifferentTokens_ProduceDifferentHashes()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act
        var hash1 = token1.ComputeHash();
        var hash2 = token2.ComputeHash();

        // Assert
        hash2.Should().NotBe(hash1);
    }

    [Fact]
    public void MatchesHash_WithCorrectHash_ReturnsTrue()
    {
        // Arrange
        var token = SessionToken.Generate();
        var correctHash = token.ComputeHash();

        // Act
        var result = token.MatchesHash(correctHash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesHash_WithWrongHash_ReturnsFalse()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();
        var wrongHash = token2.ComputeHash();

        // Act
        var result = token1.MatchesHash(wrongHash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MatchesHash_WithEmptyHash_ReturnsFalse(string invalidHash)
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var result = token.MatchesHash(invalidHash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesHash_WithInvalidHash_ReturnsFalse()
    {
        // Arrange
        var token = SessionToken.Generate();
        var invalidHash = "not-a-valid-hash";

        // Act
        var result = token.MatchesHash(invalidHash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ToString_RedactsTokenValue()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var stringValue = token.ToString();

        // Assert
        stringValue.Should().Be("[REDACTED]");
        stringValue.Should().NotContain(token.Value);
    }

    [Fact]
    public void EqualityComparison_WithSameToken_AreEqual()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.FromStored(token1.Value);

        // Act & Assert
        token2.Should().Be(token1);
    }

    [Fact]
    public void EqualityComparison_WithDifferentTokens_AreNotEqual()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act & Assert
        token2.Should().NotBe(token1);
    }

    [Fact]
    public void ImplicitConversion_ToStringWorks()
    {
        // Arrange
        var token = SessionToken.Generate();
        var expectedValue = token.Value;

        // Act
        string tokenString = token;

        // Assert
        tokenString.Should().Be(expectedValue);
    }

    private static bool TryDecodeBase64(string base64String, out byte[]? bytes)
    {
        bytes = null;
        try
        {
            bytes = Convert.FromBase64String(base64String);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
