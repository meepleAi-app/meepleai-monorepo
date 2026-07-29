using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #3373 D1 (ADR-087): bounded-retry classification for PDF cover generation.
/// A transient failure (R2/DB infra) is retry-eligible — the PDF returns to Pending so
/// BackfillPdfCoversJob re-picks it — until MaxAttempts, after which it is terminal Failed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfCoverRetryPolicyTests
{
    [Theory]
    [InlineData(0, PdfCoverGenerationStatus.Pending, 1)]  // first transient failure → retry
    [InlineData(1, PdfCoverGenerationStatus.Pending, 2)]  // second → retry
    [InlineData(2, PdfCoverGenerationStatus.Failed, 3)]   // third reaches the budget → terminal
    [InlineData(5, PdfCoverGenerationStatus.Failed, 6)]   // already past → stays terminal
    public void NextAfterTransientFailure_RetriesUntilMaxThenTerminal(
        int currentAttempts, PdfCoverGenerationStatus expectedStatus, int expectedAttempts)
    {
        var (status, attempts) = PdfCoverRetryPolicy.NextAfterTransientFailure(currentAttempts);

        attempts.Should().Be(expectedAttempts, "each transient failure increments the counter");
        status.Should().Be(expectedStatus,
            "retry-eligible (Pending) while under MaxAttempts, terminal (Failed) once reached");
    }

    [Fact]
    public void MaxAttempts_IsThree()
    {
        PdfCoverRetryPolicy.MaxAttempts.Should().Be(3, "DEC-3j mirror: 3 attempts before terminal");
    }
}
