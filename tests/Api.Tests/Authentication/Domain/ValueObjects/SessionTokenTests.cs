using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Authentication.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class SessionTokenTests
{
    #region Generate Factory Tests

    [Fact]
    public void Generate_CreatesToken()
    {
        // Act
        var token = SessionToken.Generate();

        // Assert
        token.Should().NotBeNull();
        token.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Generate_CreatesBase64EncodedToken()
    {
        // Act
        var token = SessionToken.Generate();

        // Assert - Should be valid Base64
        var action = () => Convert.FromBase64String(token.Value);
        action.Should().NotThrow();
    }

    [Fact]
    public void Generate_Creates256BitToken()
    {
        // Act
        var token = SessionToken.Generate();

        // Assert - 32 bytes = 256 bits, Base64 encoded
        var bytes = Convert.FromBase64String(token.Value);
        bytes.Should().HaveCount(32); // 256 bits = 32 bytes
    }

    [Fact]
    public void Generate_CreatesDifferentTokensEachTime()
    {
        // Act
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();
        var token3 = SessionToken.Generate();

        // Assert - All tokens should be unique
        var tokens = new[] { token1.Value, token2.Value, token3.Value };
        tokens.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_CreatesCryptographicallyRandomTokens()
    {
        // Act - Generate many tokens
        var tokens = Enumerable.Range(0, 100)
            .Select(_ => SessionToken.Generate().Value)
            .ToList();

        // Assert - All should be unique (cryptographic randomness)
        tokens.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region FromStored Factory Tests

    [Fact]
    public void FromStored_WithValidValue_ReconstructsToken()
    {
        // Arrange
        var original = SessionToken.Generate();
        var storedValue = original.Value;

        // Act
        var reconstructed = SessionToken.FromStored(storedValue);

        // Assert
        reconstructed.Value.Should().Be(storedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromStored_WithEmptyValue_ThrowsValidationException(string? invalidValue)
    {
        // Act & Assert
        var action = () => SessionToken.FromStored(invalidValue!);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Session token cannot be empty*");
    }

    [Fact]
    public void FromStored_WithAnyNonEmptyValue_Succeeds()
    {
        // Arrange - Token reconstruction doesn't validate format
        var customValue = "custom-token-value";

        // Act
        var token = SessionToken.FromStored(customValue);

        // Assert
        token.Value.Should().Be(customValue);
    }

    #endregion

    #region ComputeHash Tests

    [Fact]
    public void ComputeHash_ReturnsBase64EncodedHash()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var hash = token.ComputeHash();

        // Assert - Should be valid Base64
        var action = () => Convert.FromBase64String(hash);
        action.Should().NotThrow();
    }

    [Fact]
    public void ComputeHash_ReturnsSHA256Hash()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var hash = token.ComputeHash();

        // Assert - SHA256 produces 32 bytes = 256 bits
        var hashBytes = Convert.FromBase64String(hash);
        hashBytes.Should().HaveCount(32);
    }

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var hash1 = token.ComputeHash();
        var hash2 = token.ComputeHash();
        var hash3 = token.ComputeHash();

        // Assert - Same token always produces same hash
        hash1.Should().Be(hash2);
        hash2.Should().Be(hash3);
    }

    [Fact]
    public void ComputeHash_DifferentTokensProduceDifferentHashes()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act
        var hash1 = token1.ComputeHash();
        var hash2 = token2.ComputeHash();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    #endregion

    #region MatchesHash Tests

    [Fact]
    public void MatchesHash_WithCorrectHash_ReturnsTrue()
    {
        // Arrange
        var token = SessionToken.Generate();
        var hash = token.ComputeHash();

        // Act
        var result = token.MatchesHash(hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesHash_WithIncorrectHash_ReturnsFalse()
    {
        // Arrange
        var token = SessionToken.Generate();
        var differentToken = SessionToken.Generate();
        var wrongHash = differentToken.ComputeHash();

        // Act
        var result = token.MatchesHash(wrongHash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MatchesHash_WithEmptyHash_ReturnsFalse(string? emptyHash)
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var result = token.MatchesHash(emptyHash!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesHash_WithInvalidBase64Hash_ReturnsFalse()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var result = token.MatchesHash("not-valid-base64!!!");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesHash_WithTamperedHash_ReturnsFalse()
    {
        // Arrange
        var token = SessionToken.Generate();
        var originalHash = token.ComputeHash();

        // Tamper with the hash
        var tamperedBytes = Convert.FromBase64String(originalHash);
        tamperedBytes[0] = (byte)(tamperedBytes[0] ^ 0xFF); // Flip all bits in first byte
        var tamperedHash = Convert.ToBase64String(tamperedBytes);

        // Act
        var result = token.MatchesHash(tamperedHash);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsRedacted()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var result = token.ToString();

        // Assert - Should never expose actual token
        result.Should().Be("[REDACTED]");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        string stringValue = token;

        // Assert
        stringValue.Should().Be(token.Value);
    }

    [Fact]
    public void ImplicitConversionToString_WithNullToken_ThrowsArgumentNullException()
    {
        // Arrange
        SessionToken? token = null;

        // Act & Assert
        var action = () => { string _ = token!; };
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_SameTokenValue_AreEqual()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.FromStored(token1.Value);

        // Act & Assert
        token1.Should().Be(token2);
    }

    [Fact]
    public void Equals_DifferentTokenValues_AreNotEqual()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act & Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHashCode()
    {
        // Arrange
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.FromStored(token1.Value);

        // Act & Assert
        token1.GetHashCode().Should().Be(token2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnDifferentHashCodes()
    {
        // Arrange
        var tokens = Enumerable.Range(0, 10)
            .Select(_ => SessionToken.Generate())
            .ToList();

        // Act
        var hashCodes = tokens.Select(t => t.GetHashCode()).ToList();

        // Assert - Very likely to be unique (though collisions are possible)
        hashCodes.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Security Tests

    [Fact]
    public void Generate_ProducesHighEntropyTokens()
    {
        // Generate tokens and check they're sufficiently random
        var tokens = Enumerable.Range(0, 100)
            .Select(_ => SessionToken.Generate())
            .ToList();

        // Each token should be unique
        var uniqueValues = tokens.Select(t => t.Value).Distinct().Count();
        uniqueValues.Should().Be(100);
    }

    [Fact]
    public void ComputeHash_ProducesSecureHash()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var hash = token.ComputeHash();

        // Assert - Hash should not contain the original token
        hash.Should().NotContain(token.Value);
    }

    [Fact]
    public void TokenWorkflow_GenerateHashStoreVerify()
    {
        // Arrange - Simulate real workflow
        var token = SessionToken.Generate();
        var hashedForStorage = token.ComputeHash();

        // Act - Later, verify token against stored hash
        var tokenFromClient = SessionToken.FromStored(token.Value);
        var isValid = tokenFromClient.MatchesHash(hashedForStorage);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void TokenWorkflow_InvalidTokenDoesNotMatchStoredHash()
    {
        // Arrange
        var originalToken = SessionToken.Generate();
        var hashedForStorage = originalToken.ComputeHash();

        // Act - Attacker provides different token
        var attackerToken = SessionToken.Generate();
        var isValid = attackerToken.MatchesHash(hashedForStorage);

        // Assert
        isValid.Should().BeFalse();
    }

    #endregion
}
