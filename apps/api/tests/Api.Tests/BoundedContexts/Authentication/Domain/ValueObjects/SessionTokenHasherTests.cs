using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using System.Security.Cryptography;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Tests for the centralized session token hashing utility.
/// SessionTokenHasher is the single source of truth used by both storage
/// (Session.TokenHash via SessionToken.ComputeHash) and lookup paths (extend / revoke endpoints).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class SessionTokenHasherTests
{
    [Fact]
    public void HashFromCookie_NullInput_ThrowsValidationException()
    {
        // Act
        Action act = () => SessionTokenHasher.HashFromCookie(null!);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void HashFromCookie_EmptyString_ThrowsValidationException()
    {
        // Act
        Action act = () => SessionTokenHasher.HashFromCookie(string.Empty);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("   \n")]
    public void HashFromCookie_WhitespaceInput_ThrowsValidationException(string input)
    {
        // Act
        Action act = () => SessionTokenHasher.HashFromCookie(input);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData("not-base64!@#$")]                // Disallowed characters.
    [InlineData("AAAA===extra")]                  // Padding in middle.
    [InlineData("ABCD!!!!!!!!!!!!!!!!!!!!!!!=")]  // Invalid characters with padding.
    public void HashFromCookie_MalformedBase64_ThrowsValidationException(string malformed)
    {
        // Act
        Action act = () => SessionTokenHasher.HashFromCookie(malformed);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void HashFromCookie_OversizedInput_ThrowsValidationException()
    {
        // Arrange — produce a Base-64 string whose decoded length exceeds the 256-byte cap.
        // 1024 bytes => ~1368 Base-64 chars, well above the hasher's MaxDecodedBytes guard.
        var oversizedBytes = new byte[1024];
        RandomNumberGenerator.Fill(oversizedBytes);
        var oversizedToken = Convert.ToBase64String(oversizedBytes);

        // Act
        Action act = () => SessionTokenHasher.HashFromCookie(oversizedToken);

        // Assert — guard rejects without OOM/stack issues.
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void HashFromCookie_Valid32ByteToken_ReturnsBase64HashOfDecodedBytes()
    {
        // Arrange — 32 random bytes Base-64 encoded (44-char string with padding).
        var tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes);
        var expected = Convert.ToBase64String(SHA256.HashData(tokenBytes));

        // Act
        var hash = SessionTokenHasher.HashFromCookie(token);

        // Assert
        hash.Should().Be(expected);
        hash.Length.Should().Be(44); // SHA-256 (32 bytes) Base-64 encoded with padding.
    }

    [Fact]
    public void HashFromCookie_RoundTripEquivalentToSessionTokenComputeHash()
    {
        // Arrange — generate via the canonical SessionToken factory.
        var sessionToken = SessionToken.Generate();
        var rawCookieValue = sessionToken.Value;

        // Act — compute hash via both paths.
        var viaSessionToken = sessionToken.ComputeHash();
        var viaHasher = SessionTokenHasher.HashFromCookie(rawCookieValue);

        // Assert — single source of truth: storage path and lookup path agree.
        viaHasher.Should().Be(viaSessionToken);
    }

    [Fact]
    public void HashFromCookie_DeterministicForSameInput()
    {
        // Arrange
        var tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes);

        // Act
        var hash1 = SessionTokenHasher.HashFromCookie(token);
        var hash2 = SessionTokenHasher.HashFromCookie(token);

        // Assert
        hash1.Should().Be(hash2);
    }
}
