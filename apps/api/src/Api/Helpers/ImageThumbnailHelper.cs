using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Api.Helpers;

/// <summary>Shared image thumbnail generation (300px max, JPEG q80). #2436 PR-B.</summary>
internal static class ImageThumbnailHelper
{
    private const int ThumbnailMaxDimension = 300;
    private const int ThumbnailJpegQuality = 80;

    public static async Task<MemoryStream?> GenerateThumbnailAsync(Stream sourceStream, CancellationToken ct = default)
    {
        using var image = await Image.LoadAsync(sourceStream, ct).ConfigureAwait(false);

        // Calculate new dimensions maintaining aspect ratio
        var (newWidth, newHeight) = CalculateThumbnailDimensions(image.Width, image.Height);

        image.Mutate(ctx => ctx.Resize(newWidth, newHeight));

        var outputStream = new MemoryStream();
        var encoder = new JpegEncoder
        {
            Quality = ThumbnailJpegQuality
        };
        await image.SaveAsync(outputStream, encoder, ct).ConfigureAwait(false);
        outputStream.Position = 0;
        return outputStream;
    }

    public static (int width, int height) CalculateThumbnailDimensions(int originalWidth, int originalHeight)
    {
        if (originalWidth <= ThumbnailMaxDimension && originalHeight <= ThumbnailMaxDimension)
        {
            return (originalWidth, originalHeight);
        }

        double ratio;
        if (originalWidth >= originalHeight)
        {
            ratio = (double)ThumbnailMaxDimension / originalWidth;
        }
        else
        {
            ratio = (double)ThumbnailMaxDimension / originalHeight;
        }

        return (Math.Max(1, (int)(originalWidth * ratio)), Math.Max(1, (int)(originalHeight * ratio)));
    }
}
