using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using ImageMagick;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Handler-driven tests for <see cref="EnrichCatalogCoverCommandHandler"/>.
/// Wires the REAL Phase B pipeline (M3 <see cref="WikidataCatalogProvider"/> +
/// M4 <see cref="WikimediaCommonsClient"/> + M6 <see cref="WebpVariantGenerator"/>
/// + M7 <see cref="CoverR2UploadPipeline"/>) and mocks ONLY the HTTP boundary
/// (HttpMessageHandler instances) + the S3 client + the repository — per the
/// feedback memory <c>acceptance_tests_must_exercise_real_pipeline</c> (PR #1555
/// SP5 S2 lesson: fixture-only DTO setup masks handler-level gaps).
/// Issue #1823 Phase B M8.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class EnrichCatalogCoverCommandHandlerTests
{
    private const string TestQid = "Q98056728";
    private const string TestFilename = "Brass_Birmingham_cover.jpg";
    private const string ExpectedSourceUrl = "https://www.wikidata.org/wiki/" + TestQid;
    private static readonly DateTime FixedNowUtc = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    // ──────────────────────────────────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidGameWithQid_SuccessfullyEnrichesCover()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        harness.WikidataHandler.SparqlJson = BuildSparqlImageResponse(TestFilename);
        harness.CommonsHandler.LicenseJson = BuildImageInfoResponse("CC BY-SA 4.0", artistHtml: "John Doe");
        harness.CommonsHandler.ImageBytes = await CreateSolidImagePngAsync(800, 600);

        PutObjectRequest? capturedRequest = null;
        harness.S3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Success>();
        var success = (EnrichCatalogCoverResult.Success)result;
        success.R2Key.Should().Be($"covers/{game.Id}/cover",
            "DB key MUST NOT include .webp suffix — CoverUrlResolver appends it");
        success.R2Key.Should().NotEndWith(".webp");
        success.License.Should().Be("CC BY-SA 4.0");
        success.Attribution.Should().Be("John Doe");
        success.SourceUrl.Should().Be(ExpectedSourceUrl);

        // Verify the aggregate was updated + persisted
        game.WikidataCoverR2Key.Should().Be(success.R2Key);
        game.WikidataCoverLicense.Should().Be("CC BY-SA 4.0");
        game.WikidataCoverAttribution.Should().Be("John Doe");
        game.WikidataCoverSourceUrl.Should().Be(ExpectedSourceUrl);
        game.WikidataQidLastVerifiedAt.Should().Be(FixedNowUtc);

        harness.RepoMock.Verify(r => r.Update(game), Times.Once);
        harness.UowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Key.Should().Be($"covers/{game.Id}/cover.webp",
            "physical R2 object key INCLUDES .webp suffix");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Game not found → NotFoundException (HTTP 404 via middleware)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        var harness = BuildHarness();
        var gameId = Guid.NewGuid();

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        await harness.Sut
            .Invoking(h => h.Handle(new EnrichCatalogCoverCommand(gameId), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();

        harness.UowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Skipped reasons
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GameWithoutQid_ReturnsSkippedQidMissing()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: null);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Skipped>();
        ((EnrichCatalogCoverResult.Skipped)result).Reason
            .Should().Be(EnrichCatalogCoverCommandHandler.SkipReasonQidMissing);

        harness.WikidataHandler.Requests.Should().BeEmpty(
            "QID-missing must short-circuit BEFORE issuing any Wikidata SPARQL request");
        harness.UowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GameAlreadyEnrichedRecent_ReturnsSkippedAlreadyEnrichedRecent()
    {
        var harness = BuildHarness();
        // 30 days ago — well inside the 90-day freshness window.
        var lastVerified = FixedNowUtc.AddDays(-30);
        var game = BuildGame(
            qid: TestQid,
            wikidataCoverR2Key: $"covers/{Guid.NewGuid()}/cover",
            wikidataCoverLicense: "CC BY 4.0",
            wikidataCoverSourceUrl: ExpectedSourceUrl,
            wikidataQidLastVerifiedAt: lastVerified);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Skipped>();
        ((EnrichCatalogCoverResult.Skipped)result).Reason
            .Should().Be(EnrichCatalogCoverCommandHandler.SkipReasonAlreadyEnrichedRecent);

        harness.WikidataHandler.Requests.Should().BeEmpty(
            "freshness-window skip must short-circuit BEFORE the SPARQL call");
    }

    [Fact]
    public async Task Handle_GameAlreadyEnrichedStale_RefreshesCover()
    {
        var harness = BuildHarness();
        // 100 days ago — well outside the 90-day window.
        var lastVerified = FixedNowUtc.AddDays(-100);
        var game = BuildGame(
            qid: TestQid,
            wikidataCoverR2Key: "covers/stale/cover",
            wikidataCoverLicense: "Old license",
            wikidataCoverSourceUrl: ExpectedSourceUrl,
            wikidataQidLastVerifiedAt: lastVerified);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        harness.WikidataHandler.SparqlJson = BuildSparqlImageResponse(TestFilename);
        harness.CommonsHandler.LicenseJson = BuildImageInfoResponse("CC0");
        harness.CommonsHandler.ImageBytes = await CreateSolidImagePngAsync(400, 600);
        harness.S3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Success>(
            "stale entries (> 90 days) must trigger a refresh of all four cover columns");

        game.WikidataCoverLicense.Should().Be("CC0");
        game.WikidataQidLastVerifiedAt.Should().Be(FixedNowUtc);
        game.WikidataCoverR2Key.Should().Be($"covers/{game.Id}/cover");
    }

    [Fact]
    public async Task Handle_NoP18Image_ReturnsSkippedImageNotAvailable()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // SPARQL returns empty bindings → no wdt:P18 claim
        harness.WikidataHandler.SparqlJson = BuildSparqlEmptyResponse();

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Skipped>();
        ((EnrichCatalogCoverResult.Skipped)result).Reason
            .Should().Be(EnrichCatalogCoverCommandHandler.SkipReasonImageNotAvailable);

        harness.CommonsHandler.Requests.Should().BeEmpty(
            "no wdt:P18 image means no Commons license/image calls are issued");
    }

    [Fact]
    public async Task Handle_LicenseNotWhitelisted_ReturnsSkippedLicenseNotWhitelisted()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        harness.WikidataHandler.SparqlJson = BuildSparqlImageResponse(TestFilename);
        // "All rights reserved" is NOT whitelisted (DEC-3c)
        harness.CommonsHandler.LicenseJson = BuildImageInfoResponse("All rights reserved");

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Skipped>();
        ((EnrichCatalogCoverResult.Skipped)result).Reason
            .Should().Be(EnrichCatalogCoverCommandHandler.SkipReasonLicenseNotWhitelisted);

        harness.CommonsHandler.Requests
            .Should().NotContain(r => r.RequestUri!.AbsolutePath.Contains("Special:FilePath", StringComparison.Ordinal),
                "non-whitelisted license must short-circuit BEFORE downloading image bytes");
    }

    [Fact]
    public async Task Handle_ImageBytesNotAvailable_ReturnsSkippedImageBytesNotAvailable()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        harness.WikidataHandler.SparqlJson = BuildSparqlImageResponse(TestFilename);
        harness.CommonsHandler.LicenseJson = BuildImageInfoResponse("CC BY 4.0");
        // Special:FilePath returns 404
        harness.CommonsHandler.ImageStatus = HttpStatusCode.NotFound;
        harness.CommonsHandler.ImageBytes = Array.Empty<byte>();

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Skipped>();
        ((EnrichCatalogCoverResult.Skipped)result).Reason
            .Should().Be(EnrichCatalogCoverCommandHandler.SkipReasonImageBytesNotAvailable);

        harness.S3Mock.Verify(
            s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "image-bytes-not-available short-circuits BEFORE WebP + R2 upload");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Failed reasons
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ImageProcessingError_ReturnsFailedImageProcessingError()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        harness.WikidataHandler.SparqlJson = BuildSparqlImageResponse(TestFilename);
        harness.CommonsHandler.LicenseJson = BuildImageInfoResponse("CC0");
        // Corrupted image bytes → Magick.NET decode fails → WebpVariantGenerator
        // throws ImageProcessingException (DEC-3d-1, issue #2055 Phase G)
        harness.CommonsHandler.ImageBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE };

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Failed>();
        var failed = (EnrichCatalogCoverResult.Failed)result;
        failed.Reason.Should().Be(EnrichCatalogCoverCommandHandler.FailReasonImageProcessing);
        failed.Details.Should().NotBeNullOrEmpty(
            "Failed.Details carries the exception message for diagnostics");

        harness.S3Mock.Verify(
            s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "image-processing error short-circuits BEFORE R2 upload");
        harness.UowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WikidataCircuitBreakerOpen_ReturnsFailedCircuitOpen()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Simulate the M10 circuit breaker tripping for the Wikidata client.
        // BrokenCircuitException propagates through the SUT pipeline; the M8
        // handler MUST catch it via the reflection-based detector and return
        // Failed("circuit-open") rather than letting it crash the runner.
        harness.WikidataHandler.OverrideThrow = new Polly.CircuitBreaker.BrokenCircuitException("circuit OPEN");

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Failed>();
        var failed = (EnrichCatalogCoverResult.Failed)result;
        failed.Reason.Should().Be(EnrichCatalogCoverCommandHandler.FailReasonCircuitOpen);
        failed.Details.Should().Contain("circuit OPEN");
        harness.UowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CommonsCircuitBreakerOpen_ReturnsFailedCircuitOpen()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Wikidata SPARQL still responds; Commons API circuit OPEN — exception
        // surfaces on the license fetch hop.
        harness.WikidataHandler.SparqlJson = BuildSparqlImageResponse(TestFilename);
        harness.CommonsHandler.OverrideThrow = new Polly.CircuitBreaker.BrokenCircuitException("commons circuit OPEN");

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Failed>();
        ((EnrichCatalogCoverResult.Failed)result).Reason
            .Should().Be(EnrichCatalogCoverCommandHandler.FailReasonCircuitOpen);
    }

    [Fact]
    public async Task Handle_R2UploadError_ReturnsFailedR2UploadError()
    {
        var harness = BuildHarness();
        var game = BuildGame(qid: TestQid);

        harness.RepoMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        harness.WikidataHandler.SparqlJson = BuildSparqlImageResponse(TestFilename);
        harness.CommonsHandler.LicenseJson = BuildImageInfoResponse("CC0");
        harness.CommonsHandler.ImageBytes = await CreateSolidImagePngAsync(400, 600);

        harness.S3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("simulated R2 outage"));

        var result = await harness.Sut.Handle(
            new EnrichCatalogCoverCommand(game.Id),
            CancellationToken.None);

        result.Should().BeOfType<EnrichCatalogCoverResult.Failed>();
        var failed = (EnrichCatalogCoverResult.Failed)result;
        failed.Reason.Should().Be(EnrichCatalogCoverCommandHandler.FailReasonR2Upload);
        failed.Details.Should().Contain("simulated R2 outage");

        harness.UowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never,
            "DB save must NOT run when R2 upload fails");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers — fixture builders + routing handlers
    // ──────────────────────────────────────────────────────────────────────

    private sealed record TestHarness(
        EnrichCatalogCoverCommandHandler Sut,
        Mock<ISharedGameRepository> RepoMock,
        Mock<IUnitOfWork> UowMock,
        Mock<IAmazonS3> S3Mock,
        WikidataSparqlHandler WikidataHandler,
        CommonsRoutingHandler CommonsHandler,
        FakeTimeProvider TimeProvider);

    private static TestHarness BuildHarness()
    {
        var repo = new Mock<ISharedGameRepository>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var rateLimiter = new Mock<IWikimediaRateLimiter>();
        rateLimiter.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var sparqlHandler = new WikidataSparqlHandler();
        var commonsHandler = new CommonsRoutingHandler();

        var wikidataHttp = new HttpClient(sparqlHandler)
        {
            BaseAddress = new Uri("https://query.wikidata.org/")
        };
        var commonsHttp = new HttpClient(commonsHandler)
        {
            BaseAddress = new Uri("https://commons.wikimedia.org/")
        };

        var wikidataProvider = new WikidataCatalogProvider(
            wikidataHttp,
            NullLogger<WikidataCatalogProvider>.Instance,
            rateLimiter.Object);
        var commonsClient = new WikimediaCommonsClient(
            commonsHttp,
            rateLimiter.Object,
            NullLogger<WikimediaCommonsClient>.Instance);
        var webpGenerator = new WebpVariantGenerator(NullLogger<WebpVariantGenerator>.Instance);

        var s3Mock = new Mock<IAmazonS3>();
        var s3Opts = new S3StorageOptions
        {
            Endpoint = "https://test.r2.cloudflarestorage.com",
            AccessKey = "test-access",
            SecretKey = "test-secret",
            BucketName = "test-bucket",
            Region = "auto",
            ForcePathStyle = false,
        };
        var r2Pipeline = new CoverR2UploadPipeline(
            s3Mock.Object,
            s3Opts,
            NullLogger<CoverR2UploadPipeline>.Instance);

        var fakeTime = new FakeTimeProvider(new DateTimeOffset(FixedNowUtc, TimeSpan.Zero));

        var handler = new EnrichCatalogCoverCommandHandler(
            repo.Object,
            uow.Object,
            wikidataProvider,
            commonsClient,
            webpGenerator,
            r2Pipeline,
            fakeTime,
            NullLogger<EnrichCatalogCoverCommandHandler>.Instance);

        return new TestHarness(handler, repo, uow, s3Mock, sparqlHandler, commonsHandler, fakeTime);
    }

    private static SharedGame BuildGame(
        string? qid = TestQid,
        string? wikidataCoverR2Key = null,
        string? wikidataCoverLicense = null,
        string? wikidataCoverAttribution = null,
        string? wikidataCoverSourceUrl = null,
        DateTime? wikidataQidLastVerifiedAt = null)
    {
        var createdAt = FixedNowUtc.AddDays(-180);
        return new SharedGame(
            id: Guid.NewGuid(),
            title: "Test Game",
            yearPublished: 2020,
            description: "Test description",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 12,
            complexityRating: 3.0m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            status: GameStatus.Published,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: createdAt,
            modifiedAt: null,
            isDeleted: false,
            bggId: 12345,
            agentDefinitionId: null,
            gameDataStatus: GameDataStatus.Complete,
            hasUploadedPdf: false,
            pdfCoverR2Key: null,
            wikidataQid: qid,
            wikidataCoverR2Key: wikidataCoverR2Key,
            wikidataCoverLicense: wikidataCoverLicense,
            wikidataCoverAttribution: wikidataCoverAttribution,
            wikidataCoverSourceUrl: wikidataCoverSourceUrl,
            wikidataQidLastVerifiedAt: wikidataQidLastVerifiedAt);
    }

    private static string BuildSparqlImageResponse(string filename) => $@"{{
  ""head"": {{ ""vars"": [""image""] }},
  ""results"": {{
    ""bindings"": [{{
      ""image"": {{
        ""type"": ""uri"",
        ""value"": ""http://commons.wikimedia.org/wiki/Special:FilePath/{filename}""
      }}
    }}]
  }}
}}";

    private static string BuildSparqlEmptyResponse() => @"{
  ""head"": { ""vars"": [""image""] },
  ""results"": { ""bindings"": [] }
}";

    private static string BuildImageInfoResponse(string licenseShortName, string? artistHtml = null)
    {
        var artistFragment = artistHtml is null
            ? string.Empty
            : $",\"Artist\":{{\"value\":\"{artistHtml}\",\"source\":\"commons-desc-page\"}}";

        return $@"{{
  ""query"": {{
    ""pages"": {{
      ""12345"": {{
        ""pageid"": 12345,
        ""ns"": 6,
        ""title"": ""File:{TestFilename}"",
        ""imageinfo"": [
          {{
            ""extmetadata"": {{
              ""LicenseShortName"": {{ ""value"": ""{licenseShortName}"", ""source"": ""commons-desc-page"" }}
              {artistFragment}
            }}
          }}
        ]
      }}
    }}
  }}
}}";
    }

    private static Task<byte[]> CreateSolidImagePngAsync(int width, int height)
    {
        // ADR DEC-3d-1 (issue #2055 Phase G): Magick.NET 14.x replaces ImageSharp.
        // Solid steel-blue (#4682B4) preserved for parity with the prior fixture.
        using var image = new MagickImage(new MagickColor("#4682B4"), (uint)width, (uint)height);
        image.Format = MagickFormat.Png;
        using var ms = new MemoryStream();
        image.Write(ms);
        return Task.FromResult(ms.ToArray());
    }

    private sealed class WikidataSparqlHandler : HttpMessageHandler
    {
        public string SparqlJson { get; set; } = string.Empty;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        /// <summary>
        /// When non-null, the handler throws this exception instead of returning
        /// a response. Used to simulate the M10 circuit-breaker tripping.
        /// </summary>
        public Exception? OverrideThrow { get; set; }
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (OverrideThrow is not null)
            {
                return Task.FromException<HttpResponseMessage>(OverrideThrow);
            }
            var content = new StringContent(SparqlJson, Encoding.UTF8, "application/sparql-results+json");
            return Task.FromResult(new HttpResponseMessage(Status) { Content = content });
        }
    }

    /// <summary>
    /// Test harness for the Commons HttpClient: dispatches by URL path to the
    /// imageinfo API (license JSON) or to the Special:FilePath endpoint (raw
    /// image bytes).
    /// <para>
    /// NOTE on redirect-following: in production, Commons returns a 302 redirect
    /// from <c>commons.wikimedia.org/wiki/Special:FilePath/...</c> to
    /// <c>upload.wikimedia.org/...</c>. <see cref="HttpClientHandler.AllowAutoRedirect"/>
    /// defaults to <see langword="true"/> so the production
    /// <see cref="WikimediaCommonsClient"/> transparently follows the redirect.
    /// This in-memory handler short-circuits the redirect entirely (no 302 →
    /// upload.wikimedia.org round-trip) and serves the bytes directly from the
    /// Special:FilePath path. Any future change to the DI registration that
    /// disables <c>AllowAutoRedirect</c> would NOT be caught by these tests —
    /// such a change MUST be paired with an integration test or a documented
    /// invariant on the typed HttpClient configuration.
    /// </para>
    /// </summary>
    private sealed class CommonsRoutingHandler : HttpMessageHandler
    {
        public string LicenseJson { get; set; } = string.Empty;
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public HttpStatusCode LicenseStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode ImageStatus { get; set; } = HttpStatusCode.OK;
        /// <summary>
        /// When non-null, the handler throws this exception instead of dispatching.
        /// Used to simulate the M10 circuit-breaker tripping on the Commons client.
        /// </summary>
        public Exception? OverrideThrow { get; set; }
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (OverrideThrow is not null)
            {
                return Task.FromException<HttpResponseMessage>(OverrideThrow);
            }
            var path = request.RequestUri!.AbsolutePath;

            if (path.Contains("api.php", StringComparison.Ordinal))
            {
                var content = new StringContent(LicenseJson, Encoding.UTF8, "application/json");
                return Task.FromResult(new HttpResponseMessage(LicenseStatus) { Content = content });
            }

            if (path.Contains("Special:FilePath", StringComparison.Ordinal))
            {
                var content = new ByteArrayContent(ImageBytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                return Task.FromResult(new HttpResponseMessage(ImageStatus) { Content = content });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
