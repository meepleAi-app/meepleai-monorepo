using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SysDrawing = System.Drawing;
using SysImaging = System.Drawing.Imaging;

namespace Api.Tests;

/// <summary>
/// Utility to create a "scanned" PDF (text rendered as image) for OCR testing
/// </summary>
public static class CreateScannedPdf
{
    public static void Generate(string outputPath, string text)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // Create a bitmap image with the text rendered on it
        var imageBytes = CreateTextImage(text, 600, 800);

        // Create PDF with the image (simulates a scanned document)
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0); // No margins for scanned look
                page.Content().Image(imageBytes);
            });
        }).GeneratePdf(outputPath);
    }

    private static byte[] CreateTextImage(string text, int width, int height)
    {
        using var bitmap = new SysDrawing.Bitmap(width, height);
        using var graphics = SysDrawing.Graphics.FromImage(bitmap);

        // White background (paper)
        graphics.Clear(SysDrawing.Color.White);

        // Draw text in black
        using var font = new SysDrawing.Font("Arial", 24, SysDrawing.FontStyle.Regular);
        using var brush = new SysDrawing.SolidBrush(SysDrawing.Color.Black);

        // Add some margin
        var rect = new SysDrawing.RectangleF(50, 50, width - 100, height - 100);
        graphics.DrawString(text, font, brush, rect);

        // Convert to byte array
        using var ms = new MemoryStream();
        bitmap.Save(ms, SysImaging.ImageFormat.Png);
        return ms.ToArray();
    }
}
