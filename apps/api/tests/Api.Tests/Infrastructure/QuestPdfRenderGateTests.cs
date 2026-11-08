using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// #4950 — <see cref="QuestPdfRenderGate"/> lets one render in at a time. Deterministic: the bodies do not render
/// anything, they record how many of them are inside the gate at once, so the exclusion does not depend on winning a race.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Collection(QuestPdfRenderingCollection.Name)] // GeneratePdf_ReturnsAPdf_… renders
public sealed class QuestPdfRenderGateTests
{
    [Fact]
    public async Task Run_NeverHasTwoBodiesInsideAtTheSameTime()
    {
        var inside = 0;
        var mostInside = 0;

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => QuestPdfRenderGate.Run(() =>
        {
            var now = Interlocked.Increment(ref inside);
            InterlockedMax(ref mostInside, now);
            Thread.Sleep(5); // long enough that a second body, if admitted, would be counted
            Interlocked.Decrement(ref inside);
            return 0;
        }))));

        mostInside.Should().Be(1, "two renders overlapping in the process is the defect the gate exists for");
    }

    [Fact]
    public void Run_ReturnsWhatTheBodyReturns()
    {
        QuestPdfRenderGate.Run(() => 42).Should().Be(42);
    }

    [Fact]
    public void Run_WhenABodyThrows_TheNextOneStillGetsIn()
    {
        var failing = () => QuestPdfRenderGate.Run<int>(() => throw new InvalidOperationException("render failed"));

        failing.Should().Throw<InvalidOperationException>().WithMessage("render failed");

        // A lock that stayed held would block here; the timeout turns that into a failure instead of a hung run.
        var next = Task.Run(() => QuestPdfRenderGate.Run(() => "ok"));
        next.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the gate must be released when a render throws");
        next.Result.Should().Be("ok");
    }

    [Fact]
    public void GeneratePdf_ReturnsAPdf_ToBytesAndToAStream()
    {
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Content().Text("gate smoke test");
        }));

        var bytes = QuestPdfRenderGate.GeneratePdf(document);
        using var stream = new MemoryStream();
        QuestPdfRenderGate.GeneratePdf(document, stream);

        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        System.Text.Encoding.ASCII.GetString(stream.ToArray(), 0, 5).Should().Be("%PDF-");
    }

    private static void InterlockedMax(ref int target, int candidate)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref target);
            if (candidate <= seen)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, candidate, seen) != seen);
    }
}
