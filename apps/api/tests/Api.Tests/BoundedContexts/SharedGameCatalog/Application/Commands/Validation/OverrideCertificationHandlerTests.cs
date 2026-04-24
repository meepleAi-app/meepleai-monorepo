using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
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
public class OverrideCertificationHandlerTests
{
    private readonly Mock<IMechanicAnalysisRepository> _analysisRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<OverrideCertificationHandler>> _loggerMock = new();

    private readonly OverrideCertificationHandler _handler;

    private const string ValidReason = "Admin justification covering more than twenty chars for audit trail.";

    public OverrideCertificationHandlerTests()
    {
        _handler = new OverrideCertificationHandler(
            _analysisRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    // ============================================================================================
    // Constructor null-argument tests
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullAnalysisRepository_Throws()
    {
        var act = () => new OverrideCertificationHandler(
            analysisRepository: null!,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("analysisRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new OverrideCertificationHandler(
            _analysisRepoMock.Object,
            unitOfWork: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new OverrideCertificationHandler(
            _analysisRepoMock.Object,
            _unitOfWorkMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ============================================================================================
    // Handle argument-guard + repository-miss + pre-check tests
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
            .Setup(r => r.GetByIdAsync(analysisId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicAnalysis?)null);

        var act = () => _handler.Handle(
            new OverrideCertificationCommand(analysisId, ValidReason, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();

        _analysisRepoMock.Verify(r => r.Update(It.IsAny<MechanicAnalysis>()), Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AnalysisWithoutPriorMetrics_ThrowsConflict()
    {
        // LastMetricsId is null — override has no baseline to escalate from.
        var analysis = BuildAnalysis(
            certificationStatus: CertificationStatus.NotCertified,
            lastMetricsId: null);

        _analysisRepoMock
            .Setup(r => r.GetByIdAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var act = () => _handler.Handle(
            new OverrideCertificationCommand(analysis.Id, ValidReason, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*{analysis.Id}*no prior metrics*");

        _analysisRepoMock.Verify(r => r.Update(It.IsAny<MechanicAnalysis>()), Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AnalysisAlreadyCertified_ThrowsConflict()
    {
        var analysis = BuildAnalysis(
            certificationStatus: CertificationStatus.Certified,
            lastMetricsId: Guid.NewGuid(),
            certifiedAt: DateTimeOffset.UtcNow.AddHours(-1));

        _analysisRepoMock
            .Setup(r => r.GetByIdAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var act = () => _handler.Handle(
            new OverrideCertificationCommand(analysis.Id, ValidReason, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*{analysis.Id}*already certified*");

        _analysisRepoMock.Verify(r => r.Update(It.IsAny<MechanicAnalysis>()), Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============================================================================================
    // Happy path
    // ============================================================================================

    [Fact]
    public async Task Handle_HappyPath_AppliesOverrideAndRaisesCertifiedEvent()
    {
        var priorMetricsId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var analysis = BuildAnalysis(
            certificationStatus: CertificationStatus.NotCertified,
            lastMetricsId: priorMetricsId);

        _analysisRepoMock
            .Setup(r => r.GetByIdAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var sequence = new List<string>();
        _analysisRepoMock
            .Setup(r => r.Update(It.IsAny<MechanicAnalysis>()))
            .Callback(() => sequence.Add("analysis.Update"));
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("uow.Save"))
            .ReturnsAsync(1);

        var before = DateTimeOffset.UtcNow;
        var result = await _handler.Handle(
            new OverrideCertificationCommand(analysis.Id, ValidReason, userId),
            TestContext.Current.CancellationToken);
        var after = DateTimeOffset.UtcNow;

        result.Should().Be(MediatR.Unit.Value);

        // Aggregate state mutated by CertifyViaOverride.
        analysis.CertificationStatus.Should().Be(CertificationStatus.Certified);
        analysis.CertifiedByUserId.Should().Be(userId);
        analysis.CertificationOverrideReason.Should().Be(ValidReason);
        analysis.CertifiedAt.Should().NotBeNull();
        analysis.CertifiedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        analysis.LastMetricsId.Should().Be(priorMetricsId, "override must not overwrite the baseline metrics id");

        // Persistence order: Update then SaveChanges (single UoW commit).
        sequence.Should().Equal("analysis.Update", "uow.Save");
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // Domain event queued with override-path payload.
        var certifiedEvents = analysis.DomainEvents
            .OfType<MechanicAnalysisCertifiedEvent>()
            .ToList();
        certifiedEvents.Should().ContainSingle();
        var evt = certifiedEvents[0];
        evt.AnalysisId.Should().Be(analysis.Id);
        evt.SharedGameId.Should().Be(analysis.SharedGameId);
        evt.WasOverride.Should().BeTrue();
        evt.OverrideReason.Should().Be(ValidReason);
        evt.CertifiedByUserId.Should().Be(userId);
        evt.CertifiedAt.Should().Be(analysis.CertifiedAt!.Value);
    }

    // ============================================================================================
    // Test helpers
    // ============================================================================================

    private static MechanicAnalysis BuildAnalysis(
        CertificationStatus certificationStatus,
        Guid? lastMetricsId,
        DateTimeOffset? certifiedAt = null)
    {
        var analysisId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var citation = MechanicCitation.Reconstitute(
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
            citations: new[] { citation });

        return MechanicAnalysis.Reconstitute(
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
            certificationStatus: certificationStatus,
            certifiedAt: certifiedAt,
            certifiedByUserId: null,
            certificationOverrideReason: null,
            lastMetricsId: lastMetricsId);
    }
}
