using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CalculateMechanicAnalysisMetricsHandlerTests
{
    private readonly Mock<IMechanicAnalysisRepository> _analysisRepoMock = new();
    private readonly Mock<IMechanicAnalysisMetricsRepository> _metricsRepoMock = new();
    private readonly Mock<IMechanicGoldenClaimRepository> _goldenRepoMock = new();
    private readonly Mock<IMechanicGoldenBggTagRepository> _bggTagRepoMock = new();
    private readonly Mock<ICertificationThresholdsConfigRepository> _thresholdsRepoMock = new();
    private readonly Mock<IMechanicMatchingEngine> _matchingEngineMock = new();
    private readonly Mock<IKeywordExtractor> _keywordExtractorMock = new();
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<CalculateMechanicAnalysisMetricsHandler>> _loggerMock = new();

    private readonly CalculateMechanicAnalysisMetricsHandler _handler;

    public CalculateMechanicAnalysisMetricsHandlerTests()
    {
        _handler = new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object,
            _metricsRepoMock.Object,
            _goldenRepoMock.Object,
            _bggTagRepoMock.Object,
            _thresholdsRepoMock.Object,
            _matchingEngineMock.Object,
            _keywordExtractorMock.Object,
            _embeddingServiceMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    // ============================================================================================
    // Constructor null-argument tests (one per collaborator)
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullAnalysisRepository_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            analysisRepository: null!,
            _metricsRepoMock.Object, _goldenRepoMock.Object, _bggTagRepoMock.Object,
            _thresholdsRepoMock.Object, _matchingEngineMock.Object, _keywordExtractorMock.Object,
            _embeddingServiceMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("analysisRepository");
    }

    [Fact]
    public void Constructor_WithNullMetricsRepository_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object,
            metricsRepository: null!,
            _goldenRepoMock.Object, _bggTagRepoMock.Object, _thresholdsRepoMock.Object,
            _matchingEngineMock.Object, _keywordExtractorMock.Object,
            _embeddingServiceMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("metricsRepository");
    }

    [Fact]
    public void Constructor_WithNullGoldenRepository_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object, _metricsRepoMock.Object,
            goldenRepository: null!,
            _bggTagRepoMock.Object, _thresholdsRepoMock.Object, _matchingEngineMock.Object,
            _keywordExtractorMock.Object, _embeddingServiceMock.Object,
            _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("goldenRepository");
    }

    [Fact]
    public void Constructor_WithNullBggTagRepository_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object, _metricsRepoMock.Object, _goldenRepoMock.Object,
            bggTagRepository: null!,
            _thresholdsRepoMock.Object, _matchingEngineMock.Object, _keywordExtractorMock.Object,
            _embeddingServiceMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("bggTagRepository");
    }

    [Fact]
    public void Constructor_WithNullThresholdsRepository_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object, _metricsRepoMock.Object, _goldenRepoMock.Object,
            _bggTagRepoMock.Object,
            thresholdsRepository: null!,
            _matchingEngineMock.Object, _keywordExtractorMock.Object,
            _embeddingServiceMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("thresholdsRepository");
    }

    [Fact]
    public void Constructor_WithNullMatchingEngine_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object, _metricsRepoMock.Object, _goldenRepoMock.Object,
            _bggTagRepoMock.Object, _thresholdsRepoMock.Object,
            matchingEngine: null!,
            _keywordExtractorMock.Object, _embeddingServiceMock.Object,
            _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("matchingEngine");
    }

    [Fact]
    public void Constructor_WithNullKeywordExtractor_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object, _metricsRepoMock.Object, _goldenRepoMock.Object,
            _bggTagRepoMock.Object, _thresholdsRepoMock.Object, _matchingEngineMock.Object,
            keywordExtractor: null!,
            _embeddingServiceMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("keywordExtractor");
    }

    [Fact]
    public void Constructor_WithNullEmbeddingService_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object, _metricsRepoMock.Object, _goldenRepoMock.Object,
            _bggTagRepoMock.Object, _thresholdsRepoMock.Object, _matchingEngineMock.Object,
            _keywordExtractorMock.Object,
            embeddingService: null!,
            _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("embeddingService");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object, _metricsRepoMock.Object, _goldenRepoMock.Object,
            _bggTagRepoMock.Object, _thresholdsRepoMock.Object, _matchingEngineMock.Object,
            _keywordExtractorMock.Object, _embeddingServiceMock.Object,
            unitOfWork: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new CalculateMechanicAnalysisMetricsHandler(
            _analysisRepoMock.Object, _metricsRepoMock.Object, _goldenRepoMock.Object,
            _bggTagRepoMock.Object, _thresholdsRepoMock.Object, _matchingEngineMock.Object,
            _keywordExtractorMock.Object, _embeddingServiceMock.Object,
            _unitOfWorkMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ============================================================================================
    // Handle argument-guard + repository-miss + wrong-status tests
    // ============================================================================================

    [Fact]
    public async Task Handle_WithNullRequest_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public async Task Handle_AnalysisNotFound_ThrowsNotFoundAndDoesNotPersist()
    {
        var analysisId = Guid.NewGuid();
        _analysisRepoMock
            .Setup(r => r.GetByIdWithClaimsAsync(analysisId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicAnalysis?)null);

        var act = () => _handler.Handle(
            new CalculateMechanicAnalysisMetricsCommand(analysisId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();

        _metricsRepoMock.Verify(
            m => m.AddAsync(It.IsAny<MechanicAnalysisMetrics>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _matchingEngineMock.Verify(
            e => e.Match(
                It.IsAny<IReadOnlyList<AnalysisClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenBggTag>>(),
                It.IsAny<IReadOnlyList<AnalysisMechanicTag>>(),
                It.IsAny<CertificationThresholds>()),
            Times.Never);
    }

    [Theory]
    [InlineData(MechanicAnalysisStatus.Draft)]
    [InlineData(MechanicAnalysisStatus.InReview)]
    [InlineData(MechanicAnalysisStatus.Rejected)]
    public async Task Handle_AnalysisNotPublished_ThrowsConflictAndDoesNotPersist(
        MechanicAnalysisStatus status)
    {
        var analysis = BuildAnalysis(status: status);
        _analysisRepoMock
            .Setup(r => r.GetByIdWithClaimsAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var act = () => _handler.Handle(
            new CalculateMechanicAnalysisMetricsCommand(analysis.Id),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*{analysis.Id}*Published*");

        _metricsRepoMock.Verify(
            m => m.AddAsync(It.IsAny<MechanicAnalysisMetrics>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingVersionHash_ThrowsConflict()
    {
        var analysis = BuildAnalysis(status: MechanicAnalysisStatus.Published);
        SetupRepos(
            analysis,
            goldenClaims: Array.Empty<MechanicGoldenClaim>(),
            bggTags: Array.Empty<MechanicGoldenBggTag>(),
            thresholdsConfig: CertificationThresholdsConfig.Seed(),
            versionHash: null);

        var act = () => _handler.Handle(
            new CalculateMechanicAnalysisMetricsCommand(analysis.Id),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*golden*{analysis.SharedGameId}*");

        _metricsRepoMock.Verify(
            m => m.AddAsync(It.IsAny<MechanicAnalysisMetrics>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ClaimWithoutCitations_ThrowsInvalidOperation()
    {
        // Build an analysis whose single claim has no citations. Reconstitute does not enforce the
        // invariant (unlike Create), so this path is reachable from malformed DB data.
        var analysisId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var claimWithoutCitations = MechanicClaim.Reconstitute(
            id: Guid.NewGuid(),
            analysisId: analysisId,
            section: MechanicSection.Summary,
            text: "Orphan claim",
            displayOrder: 0,
            status: MechanicClaimStatus.Approved,
            reviewedBy: Guid.NewGuid(),
            reviewedAt: DateTime.UtcNow,
            rejectionNote: null,
            citations: Array.Empty<MechanicCitation>());

        var analysis = MechanicAnalysis.Reconstitute(
            id: analysisId,
            sharedGameId: sharedGameId,
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            status: MechanicAnalysisStatus.Published,
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            reviewedBy: Guid.NewGuid(),
            reviewedAt: DateTime.UtcNow,
            rejectionReason: null,
            totalTokensUsed: 0,
            estimatedCostUsd: 0m,
            modelUsed: "gpt-4",
            provider: "openai",
            costCapUsd: 1m,
            costCapOverrideAt: null,
            costCapOverrideBy: null,
            costCapOverrideReason: null,
            isSuppressed: false,
            suppressedAt: null,
            suppressedBy: null,
            suppressionReason: null,
            suppressionRequestedAt: null,
            suppressionRequestSource: null,
            claims: new[] { claimWithoutCitations });

        SetupRepos(
            analysis,
            goldenClaims: Array.Empty<MechanicGoldenClaim>(),
            bggTags: Array.Empty<MechanicGoldenBggTag>(),
            thresholdsConfig: CertificationThresholdsConfig.Seed(),
            versionHash: new VersionHash(new string('a', 64)));

        var act = () => _handler.Handle(
            new CalculateMechanicAnalysisMetricsCommand(analysis.Id),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{claimWithoutCitations.Id}*citation*");
    }

    // ============================================================================================
    // Happy-path tests
    // ============================================================================================

    [Fact]
    public async Task Handle_HappyPath_Certified_PersistsMetricsAndRaisesCertifiedEvent()
    {
        var analysis = BuildAnalysis(status: MechanicAnalysisStatus.Published, claimCount: 2);
        var thresholdsConfig = CertificationThresholdsConfig.Seed();
        var versionHash = new VersionHash(new string('a', 64));

        SetupRepos(
            analysis,
            goldenClaims: Array.Empty<MechanicGoldenClaim>(),
            bggTags: Array.Empty<MechanicGoldenBggTag>(),
            thresholdsConfig: thresholdsConfig,
            versionHash: versionHash);

        _keywordExtractorMock
            .Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(new[] { "kw" });
        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        // Scores passing the default thresholds (coverage>=70, bgg>=80, overall>=60).
        var matchResult = new MatchResult(
            CoveragePct: 95m,
            PageAccuracyPct: 90m,
            BggMatchPct: 92m,
            Matches: new List<MatchDetail>
            {
                new(Guid.NewGuid(), analysis.Claims[0].Id, true, 0),
                new(Guid.NewGuid(), analysis.Claims[1].Id, true, 1),
            });

        _matchingEngineMock
            .Setup(e => e.Match(
                It.IsAny<IReadOnlyList<AnalysisClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenBggTag>>(),
                It.IsAny<IReadOnlyList<AnalysisMechanicTag>>(),
                It.IsAny<CertificationThresholds>()))
            .Returns(matchResult);

        MechanicAnalysisMetrics? capturedMetrics = null;
        _metricsRepoMock
            .Setup(m => m.AddAsync(It.IsAny<MechanicAnalysisMetrics>(), It.IsAny<CancellationToken>()))
            .Callback<MechanicAnalysisMetrics, CancellationToken>((metrics, _) => capturedMetrics = metrics)
            .Returns(Task.CompletedTask);

        // Track call order
        var sequence = new List<string>();
        _goldenRepoMock
            .Setup(r => r.GetByGameAsync(analysis.SharedGameId, It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("golden.GetByGame"))
            .ReturnsAsync(Array.Empty<MechanicGoldenClaim>());
        _bggTagRepoMock
            .Setup(r => r.GetByGameAsync(analysis.SharedGameId, It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("bgg.GetByGame"))
            .ReturnsAsync(Array.Empty<MechanicGoldenBggTag>());
        _thresholdsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("thresholds.Get"))
            .ReturnsAsync(thresholdsConfig);
        _goldenRepoMock
            .Setup(r => r.GetVersionHashAsync(analysis.SharedGameId, It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("golden.GetVersionHash"))
            .ReturnsAsync(versionHash);
        _matchingEngineMock
            .Setup(e => e.Match(
                It.IsAny<IReadOnlyList<AnalysisClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenBggTag>>(),
                It.IsAny<IReadOnlyList<AnalysisMechanicTag>>(),
                It.IsAny<CertificationThresholds>()))
            .Callback(() => sequence.Add("engine.Match"))
            .Returns(matchResult);
        _metricsRepoMock
            .Setup(m => m.AddAsync(It.IsAny<MechanicAnalysisMetrics>(), It.IsAny<CancellationToken>()))
            .Callback<MechanicAnalysisMetrics, CancellationToken>((metrics, _) =>
            {
                sequence.Add("metrics.Add");
                capturedMetrics = metrics;
            })
            .Returns(Task.CompletedTask);
        _analysisRepoMock
            .Setup(r => r.Update(It.IsAny<MechanicAnalysis>()))
            .Callback(() => sequence.Add("analysis.Update"));
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("uow.Save"))
            .ReturnsAsync(1);

        var result = await _handler.Handle(
            new CalculateMechanicAnalysisMetricsCommand(analysis.Id),
            TestContext.Current.CancellationToken);

        capturedMetrics.Should().NotBeNull();
        capturedMetrics!.CertificationStatus.Should().Be(CertificationStatus.Certified);
        result.Should().Be(capturedMetrics.Id);

        sequence.Should().Equal(
            "golden.GetByGame",
            "bgg.GetByGame",
            "thresholds.Get",
            "golden.GetVersionHash",
            "engine.Match",
            "metrics.Add",
            "analysis.Update",
            "uow.Save");

        // Domain event raised on the aggregate with the automatic-path payload.
        var certifiedEvents = analysis.DomainEvents
            .OfType<MechanicAnalysisCertifiedEvent>()
            .ToList();
        certifiedEvents.Should().ContainSingle();
        var evt = certifiedEvents[0];
        evt.AnalysisId.Should().Be(analysis.Id);
        evt.SharedGameId.Should().Be(analysis.SharedGameId);
        evt.WasOverride.Should().BeFalse();
        evt.OverrideReason.Should().BeNull();
        evt.CertifiedByUserId.Should().Be(Guid.Empty);
        evt.CertifiedAt.Should().Be(capturedMetrics.ComputedAt);

        // Keyword + embedding invoked once per claim (2 claims).
        _keywordExtractorMock.Verify(k => k.Extract(It.IsAny<string>()), Times.Exactly(2));
        _embeddingServiceMock.Verify(
            e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // MatchDetails round-trip via JSON.
        capturedMetrics.MatchDetailsJson.Should().NotBeNullOrEmpty();
        var deserialized = JsonSerializer.Deserialize<List<MatchDetail>>(capturedMetrics.MatchDetailsJson);
        deserialized.Should().NotBeNull();
        deserialized!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_HappyPath_NotCertified_PersistsMetricsButRaisesNoEvent()
    {
        var analysis = BuildAnalysis(status: MechanicAnalysisStatus.Published, claimCount: 1);
        var thresholdsConfig = CertificationThresholdsConfig.Seed();
        var versionHash = new VersionHash(new string('b', 64));

        SetupRepos(
            analysis,
            goldenClaims: Array.Empty<MechanicGoldenClaim>(),
            bggTags: Array.Empty<MechanicGoldenBggTag>(),
            thresholdsConfig: thresholdsConfig,
            versionHash: versionHash);

        _keywordExtractorMock
            .Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(new[] { "kw" });
        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        // All below thresholds so certification fails.
        var matchResult = new MatchResult(
            CoveragePct: 10m,
            PageAccuracyPct: 5m,
            BggMatchPct: 15m,
            Matches: Array.Empty<MatchDetail>());

        _matchingEngineMock
            .Setup(e => e.Match(
                It.IsAny<IReadOnlyList<AnalysisClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenBggTag>>(),
                It.IsAny<IReadOnlyList<AnalysisMechanicTag>>(),
                It.IsAny<CertificationThresholds>()))
            .Returns(matchResult);

        MechanicAnalysisMetrics? capturedMetrics = null;
        _metricsRepoMock
            .Setup(m => m.AddAsync(It.IsAny<MechanicAnalysisMetrics>(), It.IsAny<CancellationToken>()))
            .Callback<MechanicAnalysisMetrics, CancellationToken>((metrics, _) => capturedMetrics = metrics)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.Handle(
            new CalculateMechanicAnalysisMetricsCommand(analysis.Id),
            TestContext.Current.CancellationToken);

        capturedMetrics.Should().NotBeNull();
        capturedMetrics!.CertificationStatus.Should().Be(CertificationStatus.NotCertified);
        result.Should().Be(capturedMetrics.Id);

        analysis.DomainEvents
            .OfType<MechanicAnalysisCertifiedEvent>()
            .Should().BeEmpty("certification event must not fire when thresholds are not met");

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAnalysisAlreadyCertified_DoesNotRaiseDuplicateEvent()
    {
        // Regression for code-review finding on commit 4019c9f5:
        // MechanicAnalysis.Reconstitute previously dropped the five certification-state fields,
        // so an aggregate loaded from the DB always came back as NotEvaluated — re-invoking the
        // metrics handler on an already-Certified analysis silently re-certified it and dispatched
        // a duplicate MechanicAnalysisCertifiedEvent. This test rehydrates the aggregate with
        // Certified state and verifies: (a) rehydration is faithful (pre-handler assertion),
        // (b) CertifyViaOverride's "Already certified" guard can observe the rehydrated state,
        // (c) re-running the handler dispatches exactly one event (not zero, not two) for this run.
        var analysisId = Guid.NewGuid();
        var priorMetricsId = Guid.NewGuid();
        var priorCertifiedAt = new DateTimeOffset(2026, 4, 23, 10, 0, 0, TimeSpan.Zero);

        var claimId = Guid.NewGuid();
        var citationForClaim = MechanicCitation.Reconstitute(
            id: Guid.NewGuid(),
            claimId: claimId,
            pdfPage: 1,
            quote: "Quote",
            chunkId: null,
            displayOrder: 0);
        var claim = MechanicClaim.Reconstitute(
            id: claimId,
            analysisId: analysisId,
            section: MechanicSection.Summary,
            text: "Claim text",
            displayOrder: 0,
            status: MechanicClaimStatus.Approved,
            reviewedBy: Guid.NewGuid(),
            reviewedAt: DateTime.UtcNow,
            rejectionNote: null,
            citations: new[] { citationForClaim });

        var analysis = MechanicAnalysis.Reconstitute(
            id: analysisId,
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            status: MechanicAnalysisStatus.Published,
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            reviewedBy: Guid.NewGuid(),
            reviewedAt: DateTime.UtcNow,
            rejectionReason: null,
            totalTokensUsed: 0,
            estimatedCostUsd: 0m,
            modelUsed: "gpt-4",
            provider: "openai",
            costCapUsd: 1m,
            costCapOverrideAt: null,
            costCapOverrideBy: null,
            costCapOverrideReason: null,
            isSuppressed: false,
            suppressedAt: null,
            suppressedBy: null,
            suppressionReason: null,
            suppressionRequestedAt: null,
            suppressionRequestSource: null,
            claims: new[] { claim },
            certificationStatus: CertificationStatus.Certified,
            certifiedAt: priorCertifiedAt,
            certifiedByUserId: null,
            certificationOverrideReason: null,
            lastMetricsId: priorMetricsId);

        // (a) Rehydration invariant — state must survive Reconstitute.
        analysis.CertificationStatus.Should().Be(CertificationStatus.Certified);
        analysis.CertifiedAt.Should().Be(priorCertifiedAt);
        analysis.LastMetricsId.Should().Be(priorMetricsId);

        // (b) Guard observability — override must reject on a rehydrated-Certified aggregate.
        var overrideAct = () => analysis.CertifyViaOverride(
            reason: new string('x', 50),
            userId: Guid.NewGuid(),
            utcNow: DateTimeOffset.UtcNow);
        overrideAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*Already certified*");

        // (c) Re-invocation — handler must raise exactly one certified event for this run,
        // regardless of prior certification state. (Handler re-computes metrics and applies them,
        // so the event represents the current run, not an accumulated total across runs.)
        var thresholdsConfig = CertificationThresholdsConfig.Seed();
        var versionHash = new VersionHash(new string('a', 64));
        SetupRepos(
            analysis,
            goldenClaims: Array.Empty<MechanicGoldenClaim>(),
            bggTags: Array.Empty<MechanicGoldenBggTag>(),
            thresholdsConfig: thresholdsConfig,
            versionHash: versionHash);

        _keywordExtractorMock
            .Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(new[] { "kw" });
        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });

        var matchResult = new MatchResult(
            CoveragePct: 95m,
            PageAccuracyPct: 90m,
            BggMatchPct: 92m,
            Matches: new List<MatchDetail>
            {
                new(Guid.NewGuid(), claim.Id, true, 0)
            });
        _matchingEngineMock
            .Setup(e => e.Match(
                It.IsAny<IReadOnlyList<AnalysisClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenClaim>>(),
                It.IsAny<IReadOnlyList<MechanicGoldenBggTag>>(),
                It.IsAny<IReadOnlyList<AnalysisMechanicTag>>(),
                It.IsAny<CertificationThresholds>()))
            .Returns(matchResult);
        _metricsRepoMock
            .Setup(m => m.AddAsync(It.IsAny<MechanicAnalysisMetrics>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _handler.Handle(
            new CalculateMechanicAnalysisMetricsCommand(analysis.Id),
            TestContext.Current.CancellationToken);

        analysis.DomainEvents
            .OfType<MechanicAnalysisCertifiedEvent>()
            .Should().ContainSingle("re-certifying a rehydrated-Certified aggregate must raise exactly one event for the current run, not a duplicate");
    }

    // ============================================================================================
    // Test helpers
    // ============================================================================================

    /// <summary>
    /// Builds a freshly reconstituted <see cref="MechanicAnalysis"/> with <paramref name="claimCount"/>
    /// valid claims (each with one citation, per ADR-051 T3).
    /// </summary>
    private static MechanicAnalysis BuildAnalysis(
        MechanicAnalysisStatus status,
        int claimCount = 1)
    {
        var analysisId = Guid.NewGuid();
        var claims = new List<MechanicClaim>();
        for (var i = 0; i < claimCount; i++)
        {
            var claimId = Guid.NewGuid();
            var citation = MechanicCitation.Reconstitute(
                id: Guid.NewGuid(),
                claimId: claimId,
                pdfPage: i + 1,
                quote: $"Quote {i}",
                chunkId: null,
                displayOrder: 0);
            var claim = MechanicClaim.Reconstitute(
                id: claimId,
                analysisId: analysisId,
                section: MechanicSection.Summary,
                text: $"Claim text {i}",
                displayOrder: i,
                status: MechanicClaimStatus.Approved,
                reviewedBy: Guid.NewGuid(),
                reviewedAt: DateTime.UtcNow,
                rejectionNote: null,
                citations: new[] { citation });
            claims.Add(claim);
        }

        return MechanicAnalysis.Reconstitute(
            id: analysisId,
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            status: status,
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            reviewedBy: status == MechanicAnalysisStatus.Published ? Guid.NewGuid() : null,
            reviewedAt: status == MechanicAnalysisStatus.Published ? DateTime.UtcNow : null,
            rejectionReason: null,
            totalTokensUsed: 0,
            estimatedCostUsd: 0m,
            modelUsed: "gpt-4",
            provider: "openai",
            costCapUsd: 1m,
            costCapOverrideAt: null,
            costCapOverrideBy: null,
            costCapOverrideReason: null,
            isSuppressed: false,
            suppressedAt: null,
            suppressedBy: null,
            suppressionReason: null,
            suppressionRequestedAt: null,
            suppressionRequestSource: null,
            claims: claims);
    }

    private void SetupRepos(
        MechanicAnalysis analysis,
        IReadOnlyList<MechanicGoldenClaim> goldenClaims,
        IReadOnlyList<MechanicGoldenBggTag> bggTags,
        CertificationThresholdsConfig thresholdsConfig,
        VersionHash? versionHash)
    {
        _analysisRepoMock
            .Setup(r => r.GetByIdWithClaimsAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);
        _goldenRepoMock
            .Setup(r => r.GetByGameAsync(analysis.SharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(goldenClaims);
        _bggTagRepoMock
            .Setup(r => r.GetByGameAsync(analysis.SharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggTags);
        _thresholdsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(thresholdsConfig);
        _goldenRepoMock
            .Setup(r => r.GetVersionHashAsync(analysis.SharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versionHash);
    }
}
