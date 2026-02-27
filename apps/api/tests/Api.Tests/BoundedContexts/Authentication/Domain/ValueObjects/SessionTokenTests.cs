using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
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
        Assert.NotNull(token);
        Assert.NotNull(token.Value);
        Assert.NotEmpty(token.Value);
    }

    [Fact]
    public void Generate_CreatesBase64EncodedToken()
    {
        // Act
        var token = SessionToken.Generate();

        // Assert - Base64 string should decode without exception
        var isValidBase64 = TryDecodeBase64(token.Value, out var bytes);
        Assert.True(isValidBase64);
        Assert.NotNull(bytes);
        Assert.Equal(32, bytes.Length); // 32 bytes = 256 bits
    }

    [Fact]
    public void Generate_CreatesTokenWithCorrectLength()
    {
        // Act
        var token = SessionToken.Generate();

        // Assert - Base64 encoding of 32 bytes should be 44 characters (with padding)
        Assert.Equal(44, token.Value.Length);
    }

    [Fact]
    public void Generate_CreatesUniqueTokens()
    {
        // Act
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();
        var token3 = SessionToken.Generate();

        // Assert
        Assert.NotEqual(token1.Value, token2.Value);
        Assert.NotEqual(token1.Value, token3.Value);
        Assert.NotEqual(token2.Value, token3.Value);
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
        Assert.Equal(iterations, tokens.Count);
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
        Assert.Equal(originalToken.Value, reconstructedToken.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromStored_WithEmptyValue_ThrowsValidationException(string invalidValue)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => SessionToken.FromStored(invalidValue));
        Assert.Contains("Session token cannot be empty", exception.Message);
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
        var exception = Assert.Throws<ValidationException>(() => SessionToken.FromStored(invalidValue));
        Assert.Contains("not a valid format", exception.Message);
    }

    [Fact]
    public void ComputeHash_ReturnsSHA256Hash()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var hash = token.ComputeHash();

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);

        // SHA256 produces 32 bytes, which Base64 encodes to 44 characters
        Assert.Equal(44, hash.Length);

        // Verify it's valid Base64
        Assert.True(TryDecodeBase64(hash, out var hashBytes));
        Assert.Equal(32, hashBytes!.Length); // 256 bits = 32 bytes
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
        Assert.Equal(hash1, hash2);
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
        Assert.NotEqual(hash1, hash2);
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
        Assert.True(result);
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
        Assert.False(result);
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
        Assert.False(result);
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
        Assert.False(result);
    }

    [Fact]
    public void ToString_RedactsTokenValue()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var stringValue = token.ToString();

        // Assert
        Assert.Equal("[REDACTED]", stringValue);
        Assert.DoesNotContain(token.Value, stringValue);
    }

    [Fact]
    public void EqualityComparison_WithSameToken_AreEqual()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.FromStored(token1.Value);

        // Act & Assert
        Assert.Equal(token1, token2);
    }

    [Fact]
    public void EqualityComparison_WithDifferentTokens_AreNotEqual()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act & Assert
        Assert.NotEqual(token1, token2);
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
        Assert.Equal(expectedValue, tokenString);
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
