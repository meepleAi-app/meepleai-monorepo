using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for OpenRouterErrorParser (Issue #5087).
/// Verifies RPM vs RPD classification, model extraction, and header parsing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5087")]
public sealed class OpenRouterErrorParserTests
{
    // ─── Non-rate-limit status codes ────────────────────────────────────────

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(400)]
    [InlineData(500)]
    public void TryParseRateLimitError_NonRateLimitStatus_ReturnsNull(int statusCode)
    {
        var result = OpenRouterErrorParser.TryParseRateLimitError("{}", statusCode);
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseRateLimitError_EmptyBody_429_ReturnsNull()
    {
        var result = OpenRouterErrorParser.TryParseRateLimitError("", 429);
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseRateLimitError_WhitespaceBody_429_ReturnsNull()
    {
        var result = OpenRouterErrorParser.TryParseRateLimitError("   ", 429);
        result.Should().BeNull();
    }

    // ─── HTTP 402 PaymentRequired ────────────────────────────────────────────

    [Fact]
    public void TryParseRateLimitError_402_ReturnsPaymentRequired()
    {
        var result = OpenRouterErrorParser.TryParseRateLimitError("{}", 402);

        result.Should().NotBeNull();
        result.ErrorType.Should().Be(RateLimitErrorType.PaymentRequired);
        result.ResetTimestampMs.Should().BeNull();
        result.ModelId.Should().BeNull();
        result.Limit.Should().BeNull();
        result.Remaining.Should().BeNull();
    }

    // ─── RPD limit ───────────────────────────────────────────────────────────

    [Fact]
    public void TryParseRateLimitError_RpdMessage_ClassifiesAsRpd()
    {
        const string body = """
            {
              "error": {
                "message": "Rate limit exceeded: limit_rpd/meta-llama/llama-3.3-70b-instruct:free",
                "code": 429,
                "metadata": {
                  "headers": {
                    "X-RateLimit-Limit": "1000",
                    "X-RateLimit-Remaining": "0",
                    "X-RateLimit-Reset": "1741305600000"
                  }
                }
              }
            }
            """;

        var result = OpenRouterErrorParser.TryParseRateLimitError(body, 429);

        result.Should().NotBeNull();
        result.ErrorType.Should().Be(RateLimitErrorType.Rpd);
        result.ResetTimestampMs.Should().Be(1741305600000L);
        result.ModelId.Should().Be("meta-llama/llama-3.3-70b-instruct:free");
        result.Limit.Should().Be(1000);
        result.Remaining.Should().Be(0);
    }

    // ─── RPM limit ───────────────────────────────────────────────────────────

    [Fact]
    public void TryParseRateLimitError_RpmMessage_ClassifiesAsRpm()
    {
        const string body = """
            {
              "error": {
                "message": "Rate limit exceeded: limit_rpm/meta-llama/llama-3.3-70b-instruct:free",
                "code": 429,
                "metadata": {
                  "headers": {
                    "X-RateLimit-Limit": "20",
                    "X-RateLimit-Remaining": "0",
                    "X-RateLimit-Reset": "1741305660000"
                  }
                }
              }
            }
            """;

        var result = OpenRouterErrorParser.TryParseRateLimitError(body, 429);

        result.Should().NotBeNull();
        result.ErrorType.Should().Be(RateLimitErrorType.Rpm);
        result.ResetTimestampMs.Should().Be(1741305660000L);
        result.ModelId.Should().Be("meta-llama/llama-3.3-70b-instruct:free");
    }

    // ─── Unknown 429 ─────────────────────────────────────────────────────────

    [Fact]
    public void TryParseRateLimitError_NoLimitKeyword_ClassifiesAsUnknown()
    {
        const string body = """{"error":{"message":"Too many requests","code":429}}""";

        var result = OpenRouterErrorParser.TryParseRateLimitError(body, 429);

        result.Should().NotBeNull();
        result.ErrorType.Should().Be(RateLimitErrorType.Unknown);
        result.ModelId.Should().BeNull();
    }

    [Fact]
    public void TryParseRateLimitError_InvalidJson_ReturnsUnknown()
    {
        var result = OpenRouterErrorParser.TryParseRateLimitError("not-json", 429);

        result.Should().NotBeNull();
        result.ErrorType.Should().Be(RateLimitErrorType.Unknown);
    }

    // ─── Missing metadata headers ────────────────────────────────────────────

    [Fact]
    public void TryParseRateLimitError_NoMetadataHeaders_NullResetAndLimits()
    {
        const string body = """
            {
              "error": {
                "message": "Rate limit exceeded: limit_rpd/some/model:free",
                "code": 429
              }
            }
            """;

        var result = OpenRouterErrorParser.TryParseRateLimitError(body, 429);

        result.Should().NotBeNull();
        result.ErrorType.Should().Be(RateLimitErrorType.Rpd);
        result.ResetTimestampMs.Should().BeNull();
        result.Limit.Should().BeNull();
        result.Remaining.Should().BeNull();
    }

    // ─── Case-insensitive classification ─────────────────────────────────────

    [Theory]
    [InlineData("Rate limit exceeded: LIMIT_RPD/some/model")]
    [InlineData("Rate limit exceeded: Limit_Rpd/some/model")]
    public void TryParseRateLimitError_RpdCaseInsensitive_ClassifiesAsRpd(string message)
    {
        var body = $$$"""{"error":{"message":"{{{message}}}","code":429}}""";

        var result = OpenRouterErrorParser.TryParseRateLimitError(body, 429);

        result.Should().NotBeNull();
        result.ErrorType.Should().Be(RateLimitErrorType.Rpd);
    }

    // ─── Root-level error (no "error" wrapper) ───────────────────────────────

    [Fact]
    public void TryParseRateLimitError_RootLevelMessage_ParsesCorrectly()
    {
        const string body = """
            {
              "message": "Rate limit exceeded: limit_rpm/some/model:free",
              "code": 429
            }
            """;

        var result = OpenRouterErrorParser.TryParseRateLimitError(body, 429);

        result.Should().NotBeNull();
        result.ErrorType.Should().Be(RateLimitErrorType.Rpm);
    }
}
