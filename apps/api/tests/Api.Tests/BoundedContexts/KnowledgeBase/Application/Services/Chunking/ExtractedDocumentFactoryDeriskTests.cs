using System.Net;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// SP1 de-risk gate (§8.3): proves that a REAL rulebook, run through the REAL unstructured-service,
/// produces raw partition <c>elements[]</c> with <c>"Title"</c> categories that
/// <see cref="ExtractedDocumentFactory"/> turns into useful headings.
///
/// Fixture: apps/api/tests/Api.Tests/TestData/unstructured-terraforming-response.json — a TRIMMED
/// regression fixture (ADR-059: minimize verbatim third-party content committed to the repo). The
/// original capture (via `curl .../api/v1/extract` against the live Docker service, `strategy=fast`,
/// `language=ita`, from `terraforming-mars_rulebook.pdf` — the game that motivated the epic) had 307
/// elements (Title 82, NarrativeText 120, UncategorizedText 47, Footer 24, ListItem 29, Header 5);
/// full details are documented in .superpowers/sdd/task-5-report.md and are NOT reproduced here. The
/// trimmed fixture keeps only the 6 real, standalone "Title"-category section headings verified
/// present verbatim in the original capture (CONTESTO on p2, PARAMETRI GLOBALI on p3, GENERAZIONI on
/// p8, AZIONI on p9, FINE DEL GIOCO on p12, VARIANTI on p13 — real rulebook section boundaries:
/// context/setup, global-parameters mechanic, round structure, turn actions, end-of-game,
/// solo/alternate-rules variants), each paired with a short SYNTHETIC placeholder body (not copied
/// from the real rulebook) so the extractor-&gt;factory grouping path still has content to exercise.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ExtractedDocumentFactoryDeriskTests
{
    // Verified present verbatim as standalone "Title"-category elements in the captured fixture
    // (see class remarks above for page numbers / rationale).
    private static readonly string[] ExpectedHeadings =
    {
        "CONTESTO",
        "PARAMETRI GLOBALI",
        "GENERAZIONI",
        "AZIONI",
        "FINE DEL GIOCO",
        "VARIANTI",
    };

    // The fixture is a static, deterministically-replayed capture (not a live network call), so all
    // 6 headings are expected to survive extractor mapping + factory grouping on every run.
    private const int MinExpectedHits = 6; // gate: ≥ N of M heading recovered
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RealRulebook_ThroughExtractor_RecoversKeyHeadings_AboveThreshold()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "unstructured-terraforming-response.json");
        var fixtureJson = File.ReadAllText(fixturePath);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(fixtureJson) });
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("UnstructuredService"))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("http://test:8001") });

        var extractor = new UnstructuredPdfTextExtractor(httpFactory.Object, Mock.Of<ILogger<UnstructuredPdfTextExtractor>>());
        using var pdf = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest\n%%EOF"));
        var paged = await extractor.ExtractPagedTextAsync(pdf, cancellationToken: Ct);

        var flatText = paged.PageChunks.Count > 0 ? string.Concat(paged.PageChunks.Select(c => c.Text)) : "";
        var doc = ExtractedDocumentFactory.FromExtraction(Guid.NewGuid(), null, paged.StructuredElements, flatText);

        var headings = doc.Sections.Where(s => s.Heading != null).Select(s => s.Heading!).ToList();
        // Exact (trimmed, case-insensitive) match, not substring: "GENERAZIONI".Contains("AZIONI") is
        // true, so a substring check would let AZIONI piggyback on GENERAZIONI instead of matching its
        // own standalone Title element.
        var hits = ExpectedHeadings.Count(expected =>
            headings.Any(h => string.Equals(h.Trim(), expected, StringComparison.OrdinalIgnoreCase)));

        hits.Should().BeGreaterThanOrEqualTo(MinExpectedHits,
            $"il gate SP1 richiede >={MinExpectedHits}/{ExpectedHeadings.Length} heading chiave; trovati: {string.Join(", ", headings)}");
    }
}
