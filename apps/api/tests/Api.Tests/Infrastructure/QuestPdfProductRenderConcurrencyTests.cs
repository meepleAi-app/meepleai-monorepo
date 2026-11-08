using System.Text.RegularExpressions;
using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// #4950 — the three product renderers, run four at a time, must keep every word. Before <c>QuestPdfRenderGate</c>
/// (see its remarks), the same shape lost the whole text of about one document in ten on a fresh process, for each of
/// the three, and 1 thread never did.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 What this test can and cannot prove. It reads the PDF back with pdfium, so a broken <c>ToUnicode</c> map shows as
/// missing words. But the defect is a race: in a process where earlier tests already rendered, a run without the gate
/// can still come out clean, so a green here is weaker than a red. The deterministic guarantees are
/// <c>QuestPdfRenderGateTests</c> (the gate lets one render in at a time) and
/// <c>QuestPdfRenderGateArchitectureTests</c> (the three renderers go through it). This one is the end-to-end witness.
/// </para>
/// <para>
/// In the <see cref="QuestPdfRenderingCollection"/> because it renders; the collection keeps other test classes' renders
/// from overlapping with these, so the only overlap here is the one the test makes.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Collection(QuestPdfRenderingCollection.Name)]
public sealed class QuestPdfProductRenderConcurrencyTests
{
    private const int Renders = 24;
    private const int Threads = 4;

    private static readonly string[] Words =
    [
        "orchard", "lantern", "meadow", "harbor", "granite", "velvet", "copper", "thistle", "marble", "saffron",
        "willow", "juniper", "cobalt", "amber", "pewter", "quartz", "falcon", "ember", "glacier", "tundra",
        "bramble", "sparrow", "walnut", "cinder", "ribbon", "hollow", "summit", "canyon", "meteor", "tavern",
    ];

    public static TheoryData<string> Producers => new() { "chat export", "admin report", "ledger report" };

    [Theory]
    [MemberData(nameof(Producers))]
    public async Task FourConcurrentRenders_KeepEveryWordOfTheDocument(string producer)
    {
        Func<Task<byte[]>> render = producer switch
        {
            "chat export" => RenderChatExportAsync,
            "admin report" => () => Task.FromResult(RenderAdminReport()),
            "ledger report" => RenderLedgerReportAsync,
            _ => throw new ArgumentOutOfRangeException(nameof(producer), producer, null),
        };

        using var gate = new SemaphoreSlim(Threads);
        var pdfs = await Task.WhenAll(Enumerable.Range(0, Renders).Select(async _ =>
        {
            await gate.WaitAsync();
            try
            {
                return await Task.Run(render);
            }
            finally
            {
                gate.Release();
            }
        }));

        var extractor = new DocnetPdfTextExtractor(NullLogger<DocnetPdfTextExtractor>.Instance);
        var recalls = new List<double>();
        foreach (var pdf in pdfs)
        {
            using var stream = new MemoryStream(pdf);
            var result = await extractor.ExtractPagedTextAsync(stream, enableOcrFallback: false, CancellationToken.None);
            var text = string.Join(' ', result.PageChunks.Select(p => p.Text)).ToLowerInvariant();
            var found = Regex.Matches(text, @"\p{L}+").Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
            recalls.Add((double)Words.Count(found.Contains) / Words.Length);
        }

        recalls.Where(r => r < 1d).Should().BeEmpty(
            "every document is rendered from the same input, so every one must give back all {0} words; a render that " +
            "lost some was overlapped by another (#4950, #4933)", Words.Length);
    }

    private static Task<byte[]> RenderChatExportAsync()
    {
        var chat = new ChatEntity
        {
            Logs = Words.Chunk(3).Select((w, i) => new ChatLogEntity
            {
                Level = i % 2 == 0 ? "user" : "assistant",
                Message = string.Join(' ', w) + " " + string.Join(' ', w.Reverse()),
                CreatedAt = new DateTime(2026, 1, 1, 10, i, 0, DateTimeKind.Utc),
            }).ToList(),
        };
        return ExportToBytesAsync(chat);
    }

    private static async Task<byte[]> ExportToBytesAsync(ChatEntity chat)
    {
        await using var stream = await new PdfExportFormatter().FormatAsync(chat, dateFrom: null, dateTo: null);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    private static byte[] RenderAdminReport()
    {
        var rows = Words.Chunk(3).Select(w => new ReportDataRow(new Dictionary<string, object>
        {
            ["first"] = w[0],
            ["second"] = w[1],
            ["third"] = w[2],
        })).ToList();
        var content = new ReportContent(
            Title: "Concurrency report",
            Description: "Words for the render test",
            GeneratedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Metadata: new Dictionary<string, object>(),
            Sections: [new ReportSection("Words", "All of them", rows)]);
        return new PdfReportFormatter().Format(content);
    }

    private static async Task<byte[]> RenderLedgerReportAsync()
    {
        var entries = Words.Select((w, i) => LedgerEntry.CreateAutoEntry(
            new DateTime(2026, 1, 1 + (i % 27), 0, 0, 0, DateTimeKind.Utc),
            i % 2 == 0 ? LedgerEntryType.Income : LedgerEntryType.Expense,
            LedgerCategory.Subscription, 10m + i, "EUR", w)).ToList();
        var repository = new Mock<ILedgerEntryRepository>();
        repository
            .Setup(r => r.GetFilteredAsync(
                It.IsAny<LedgerEntryType?>(), It.IsAny<LedgerCategory?>(), It.IsAny<LedgerEntrySource?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 1, 10000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries.AsReadOnly(), entries.Count));

        var result = await new ExportLedgerQueryHandler(repository.Object).Handle(
            new ExportLedgerQuery(LedgerExportFormat.Pdf, null, null, null, null), CancellationToken.None);
        return result.Content;
    }
}
