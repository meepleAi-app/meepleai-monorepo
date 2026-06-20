using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;

namespace Api.Helpers;

/// <summary>
/// Shared image thumbnail generation (300px max, JPEG q80). #2436 PR-B.
///
/// Issue #2055 Phase G DEC-3d-1: migrated from SixLabors.ImageSharp 3.x
/// (Six Labors Split License → incompatible with proprietary use) to
/// Magick.NET-Q8-AnyCPU 14.x (Apache 2.0). MagickImage constructor is
/// synchronous, so we wrap in <see cref="Task.Run(System.Action, CancellationToken)"/>
/// to preserve the public async signature.
/// </summary>
internal static class ImageThumbnailHelper
{
    private const int ThumbnailMaxDimension = 300;
    private const int ThumbnailJpegQuality = 80;

    public static async Task<MemoryStream?> GenerateThumbnailAsync(Stream sourceStream, CancellationToken ct = default)
    {
        using var image = await Task.Run(() => new MagickImage(sourceStream), ct).ConfigureAwait(false);

        // Calculate new dimensions maintaining aspect ratio. Magick.NET 14.x
        // exposes Width/Height as uint; cast to int for the existing helper.
        var (newWidth, newHeight) = CalculateThumbnailDimensions((int)image.Width, (int)image.Height);

        image.Resize((uint)newWidth, (uint)newHeight);

        image.Format = MagickFormat.Jpeg;
        image.Quality = ThumbnailJpegQuality;

        var outputStream = new MemoryStream();
        await Task.Run(() => image.Write(outputStream), ct).ConfigureAwait(false);
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
