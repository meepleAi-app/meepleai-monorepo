using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Parses OpenRouter 429 and 402 error responses to distinguish rate limit types.
///
/// Issue #5087: Free model quota tracking — RPM vs RPD distinction.
///
/// OpenRouter 429 error body structure:
/// <code>
/// {
///   "error": {
///     "message": "Rate limit exceeded: limit_rpd/meta-llama/llama-3.3-70b-instruct:free",
///     "code": 429,
///     "metadata": {
///       "headers": {
///         "X-RateLimit-Limit": "1000",
///         "X-RateLimit-Remaining": "0",
///         "X-RateLimit-Reset": "1741305600000"
///       }
///     }
///   }
/// }
/// </code>
/// </summary>
internal static class OpenRouterErrorParser
{
    // CA1869: Cache JsonDocumentOptions for better performance
    private static readonly JsonDocumentOptions s_jsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Attempt to parse a rate limit error from an OpenRouter HTTP error response.
    /// Returns <c>null</c> if <paramref name="statusCode"/> is not 429 or 402.
    /// </summary>
    /// <param name="responseBody">Raw JSON body of the error response.</param>
    /// <param name="statusCode">HTTP status code of the response.</param>
    public static OpenRouterRateLimitError? TryParseRateLimitError(string responseBody, int statusCode)
    {
        // HTTP 402: payment required — balance negative, no retry
        if (statusCode == 402)
        {
            return new OpenRouterRateLimitError(RateLimitErrorType.PaymentRequired, null, null, null, null);
        }

        if (statusCode != 429 || string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(responseBody, s_jsonOptions);
            var root = doc.RootElement;

            // Navigate: root.error (or root if no "error" key)
            var error = root.TryGetProperty("error", out var errorEl) ? errorEl : root;

            // Extract message: "Rate limit exceeded: limit_rpd/meta-llama/..."
            string? message = null;
            if (error.TryGetProperty("message", out var msgEl))
                message = msgEl.GetString();

            // Classify error type from message prefix
            var errorType = ClassifyErrorType(message);

            // Extract model from message: after "limit_rpd/" or "limit_rpm/"
            var modelId = ExtractModelFromMessage(message);

            // Extract rate limit headers from metadata.headers
            long? resetMs = null;
            int? limit = null;
            int? remaining = null;

            if (error.TryGetProperty("metadata", out var metaEl) &&
                metaEl.TryGetProperty("headers", out var headersEl))
            {
                resetMs = TryGetLongHeader(headersEl, "X-RateLimit-Reset");
                limit = TryGetIntHeader(headersEl, "X-RateLimit-Limit");
                remaining = TryGetIntHeader(headersEl, "X-RateLimit-Remaining");
            }

            return new OpenRouterRateLimitError(errorType, resetMs, modelId, limit, remaining);
        }
        catch (JsonException)
        {
            // Body is not valid JSON — treat as unknown 429
            return new OpenRouterRateLimitError(RateLimitErrorType.Unknown, null, null, null, null);
        }
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    private static RateLimitErrorType ClassifyErrorType(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return RateLimitErrorType.Unknown;

        if (message.Contains("limit_rpd", StringComparison.OrdinalIgnoreCase))
            return RateLimitErrorType.Rpd;

        if (message.Contains("limit_rpm", StringComparison.OrdinalIgnoreCase))
            return RateLimitErrorType.Rpm;

        return RateLimitErrorType.Unknown;
    }

    private static string? ExtractModelFromMessage(string? message)
    {
        // Pattern: "Rate limit exceeded: limit_rpd/meta-llama/llama-3.3-70b-instruct:free"
        // Model ID starts after the first slash following the limit_* prefix
        if (string.IsNullOrEmpty(message))
            return null;

        // Find "limit_rpd/" or "limit_rpm/" and take everything after it
        var rpdIdx = message.IndexOf("limit_rpd/", StringComparison.OrdinalIgnoreCase);
        var rpmIdx = message.IndexOf("limit_rpm/", StringComparison.OrdinalIgnoreCase);

        var prefixEnd = rpdIdx >= 0 ? rpdIdx + "limit_rpd/".Length
            : rpmIdx >= 0 ? rpmIdx + "limit_rpm/".Length
            : -1;

        if (prefixEnd < 0 || prefixEnd >= message.Length)
            return null;

        return message.Substring(prefixEnd).Trim();
    }

    private static long? TryGetLongHeader(JsonElement headers, string key)
    {
        if (headers.TryGetProperty(key, out var el))
        {
            var s = el.GetString();
            if (long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var val))
                return val;
        }

        return null;
    }

    private static int? TryGetIntHeader(JsonElement headers, string key)
    {
        if (headers.TryGetProperty(key, out var el))
        {
            var s = el.GetString();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
                return val;
        }

        return null;
    }
}
