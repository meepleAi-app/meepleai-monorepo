using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Cryptography;

namespace Api.Tests.Integration;

/// <summary>
/// Test helper methods for PDF upload integration tests (Issue #1819).
/// Provides reusable utilities for:
/// - File generation (valid PDFs, corrupted, edge cases)
/// - Cancellation scenarios (delayed, random timing)
/// - Security attack patterns (path traversal, XSS, SQL injection)
/// - Database verification (consistency, cleanup, relationships)
///
/// <para><b>Usage:</b> Import in test classes to reduce code duplication and improve maintainability</para>
/// </summary>
internal static class PdfUploadTestHelpers
{
    #region File Generation Helpers

    /// <summary>
    /// Creates a minimal valid PDF byte array with specified size.
    ///
    /// <para><b>Structure:</b> %PDF-1.4 header + padding + %%EOF trailer</para>
    /// <para><b>Production Scenario:</b> Simulates real PDF files for upload testing</para>
    /// </summary>
    public static byte[] CreateValidPdfBytes(int sizeInBytes)
    {
        var header = "%PDF-1.4\n"u8.ToArray();
        var trailer = "%%EOF\n"u8.ToArray();
        var padding = new byte[Math.Max(0, sizeInBytes - header.Length - trailer.Length)];

        var pdf = new byte[header.Length + padding.Length + trailer.Length];
        Buffer.BlockCopy(header, 0, pdf, 0, header.Length);
        Buffer.BlockCopy(padding, 0, pdf, header.Length, padding.Length);
        Buffer.BlockCopy(trailer, 0, pdf, header.Length + padding.Length, trailer.Length);

        return pdf;
    }

    /// <summary>
    /// Creates a corrupted PDF (invalid structure).
    /// </summary>
    public static byte[] CreateCorruptedPdfBytes()
        => "This is not a valid PDF file content"u8.ToArray();

    /// <summary>
    /// Creates a PDF with valid header but corrupted body (Issue #1819 - #1747).
    /// </summary>
    public static byte[] CreatePdfWithValidHeaderCorruptedBody()
        => "%PDF-1.4\n"u8.ToArray()
            .Concat("CORRUPTED_BINARY_DATA_!@#$%^&*()"u8.ToArray())
            .ToArray();

    /// <summary>
    /// Creates a mock IFormFile for testing.
    ///
    /// <para><b>Important:</b> Returns NEW stream each time OpenReadStream() is called</para>
    /// <para>(Issue #1688: PDF validation + blob storage need separate streams)</para>
    /// </summary>
    public static IFormFile CreateMockFormFile(string fileName, byte[] content, string contentType = "application/pdf")
    {
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.Length).Returns(content.Length);
        formFile.Setup(f => f.ContentType).Returns(contentType);
        formFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return formFile.Object;
    }

    #endregion

    #region Cancellation Helpers

    /// <summary>
    /// Creates a CancellationTokenSource that cancels after specified delay.
    ///
    /// <para><b>Usage:</b> Test cancellation at specific pipeline stages (Issue #1819 - #1736)</para>
    /// </summary>
    public static CancellationTokenSource CreateDelayedCancellation(int delayMilliseconds)
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(delayMilliseconds);
        return cts;
    }

    /// <summary>
    /// Executes an action with random cancellation timing to test race conditions.
    ///
    /// <para><b>Production Scenario:</b> User closes browser/tab at unpredictable moment during upload</para>
    /// </summary>
    public static async Task<T> ExecuteWithRandomCancellation<T>(
        Func<CancellationToken, Task<T>> action,
        int minDelayMs = 50,
        int maxDelayMs = 500)
    {
        using var cts = new CancellationTokenSource();
        var randomDelay = RandomNumberGenerator.GetInt32(minDelayMs, maxDelayMs);
        cts.CancelAfter(randomDelay);

        return await action(cts.Token);
    }

    #endregion

    #region Database Verification Helpers

    /// <summary>
    /// Verifies no PDF documents exist in database (cleanup verification).
    ///
    /// <para><b>Usage:</b> Verify transaction rollback after failures (Issue #1819 - #1736)</para>
    /// </summary>
    public static async Task VerifyNoPdfDocumentsAsync(MeepleAiDbContext context, CancellationToken ct = default)
    {
        var count = await context.PdfDocuments.CountAsync(ct);
        count.Should().Be(0, "no PDF documents should exist after cleanup/rollback");
    }

    /// <summary>
    /// Verifies no orphaned files exist in test directory (resource cleanup verification).
    ///
    /// <para><b>Production Scenario:</b> Prevents storage bloat from failed uploads (Issue #1819 - #1736)</para>
    /// <para><b>Note:</b> Directory non-existence is acceptable (not created yet)</para>
    /// </summary>
    public static void VerifyNoOrphanedFiles(string directory, string pattern = "*")
    {
        if (!Directory.Exists(directory))
            return; // Directory not created yet - OK

        var files = Directory.GetFiles(directory, pattern);
        files.Should().BeEmpty($"no orphaned files matching '{pattern}' should exist in {directory}");
    }

    /// <summary>
    /// Verifies database consistency after operations (FK integrity, relationships).
    ///
    /// <para><b>Usage:</b> Ensure no orphaned records or broken relationships (Issue #1819 - #1736)</para>
    /// </summary>
    public static async Task VerifyDatabaseConsistencyAsync(
        MeepleAiDbContext context,
        Guid userId,
        Guid gameId,
        CancellationToken ct = default)
    {
        // Verify user still exists
        var userExists = await context.Users.AnyAsync(u => u.Id == userId, ct);
        userExists.Should().BeTrue($"User {userId} should not be deleted");

        // Verify game still exists
        var gameExists = await context.Games.AnyAsync(g => g.Id == gameId, ct);
        gameExists.Should().BeTrue($"Game {gameId} should not be deleted");

        // Verify no orphaned PDF documents (all must have valid FK references)
        var orphanedDocs = await context.PdfDocuments
            .Where(d => !context.Users.Any(u => u.Id == d.UploadedByUserId) ||
                       !context.Games.Any(g => g.Id == d.GameId))
            .CountAsync(ct);

        orphanedDocs.Should().Be(0, "no PDF documents with broken FK references should exist");
    }

    /// <summary>
    /// Cleans database for isolated test execution.
    ///
    /// <para><b>Usage:</b> Prevent test contamination and unique constraint violations</para>
    /// </summary>
    public static async Task CleanDatabaseAsync(MeepleAiDbContext context, CancellationToken ct = default)
    {
        context.PdfDocuments.RemoveRange(await context.PdfDocuments.ToListAsync(ct));
        context.Games.RemoveRange(await context.Games.ToListAsync(ct));
        context.Users.RemoveRange(await context.Users.ToListAsync(ct));
        await context.SaveChangesAsync(ct);
    }

    #endregion

    #region Test Data Seeding Helpers

    /// <summary>
    /// Seeds a unique test user in database context.
    ///
    /// <para><b>Uniqueness:</b> Uses GUID in email to prevent constraint violations</para>
    /// </summary>
    public static async Task<UserEntity> SeedTestUserAsync(
        MeepleAiDbContext context,
        string? email = null,
        CancellationToken ct = default)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email ?? $"test-{Guid.NewGuid():N}@uploadtest.com",
            DisplayName = "Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Seeds a unique test game in database context.
    ///
    /// <para><b>Uniqueness:</b> Uses random BGG ID and GUID in name</para>
    /// </summary>
    public static async Task<GameEntity> SeedTestGameAsync(
        MeepleAiDbContext context,
        string? name = null,
        CancellationToken ct = default)
    {
        var bggId = RandomNumberGenerator.GetInt32(100000, 1_000_000);

        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"Test Game {Guid.NewGuid():N}",
            BggId = bggId,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 30,
            MaxPlayTimeMinutes = 90,
            CreatedAt = DateTime.UtcNow
        };
        context.Games.Add(game);
        await context.SaveChangesAsync(ct);
        return game;
    }

    #endregion
}
