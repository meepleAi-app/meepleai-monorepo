namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validates image files using magic byte signatures to prevent malicious file uploads.
/// Issue #2255: Security fix for MIME type validation vulnerability.
/// </summary>
internal static class ImageFileValidator
{
    // Magic bytes for supported image formats
    private static readonly Dictionary<string, byte[][]> MagicByteSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        ["image/jpeg"] = new[]
        {
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, // JFIF
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, // Exif
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 }, // Canon
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 }, // Samsung
            new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }, // JPEG raw
        },
        ["image/webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // RIFF (WebP container)
    };

    /// <summary>
    /// Validates that a file stream contains a valid image by checking magic bytes.
    /// Stream position is reset to 0 after validation.
    /// </summary>
    /// <param name="stream">File stream to validate (must be seekable)</param>
    /// <param name="expectedMimeType">Expected MIME type from file extension</param>
    /// <returns>True if file signature matches expected type</returns>
    public static async Task<bool> ValidateMagicBytesAsync(Stream stream, string expectedMimeType)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable for magic byte validation", nameof(stream));
        }

        // Ensure stream is at beginning
        stream.Position = 0;

        // Get expected signatures for this MIME type
        if (!MagicByteSignatures.TryGetValue(expectedMimeType, out var signatures))
        {
            // No magic byte validation available for this type
            stream.Position = 0;
            return false;
        }

        // Read enough bytes for validation (max signature length)
        var maxSignatureLength = signatures.Max(s => s.Length);
        var buffer = new byte[maxSignatureLength];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, maxSignatureLength)).ConfigureAwait(false);

        // Reset stream position for subsequent operations
        stream.Position = 0;

        if (bytesRead < signatures.Min(s => s.Length))
        {
            // File too small to contain a valid signature
            return false;
        }

        // Check if buffer starts with any of the valid signatures
        foreach (var signature in signatures)
        {
            if (BufferStartsWith(buffer, signature))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if buffer starts with the given signature bytes
    /// </summary>
    private static bool BufferStartsWith(byte[] buffer, byte[] signature)
    {
        if (buffer.Length < signature.Length)
        {
            return false;
        }

        for (int i = 0; i < signature.Length; i++)
        {
            if (buffer[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets MIME type from file extension
    /// SVG removed due to XSS security risk (Issue #2255 code review)
    /// </summary>
    public static string? GetMimeTypeFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            // SVG intentionally removed - potential XSS vector
            _ => null
        };
    }

    /// <summary>
    /// Allowed MIME types for game images (SVG excluded for security)
    /// </summary>
    public static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp"
        // SVG intentionally excluded - can contain embedded scripts (XSS risk)
    };
}
