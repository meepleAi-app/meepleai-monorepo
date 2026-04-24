using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpdateCertificationThresholdsHandlerTests
{
    private readonly Mock<ICertificationThresholdsConfigRepository> _configRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<UpdateCertificationThresholdsHandler>> _loggerMock = new();

    private readonly UpdateCertificationThresholdsHandler _handler;

    public UpdateCertificationThresholdsHandlerTests()
    {
        _handler = new UpdateCertificationThresholdsHandler(
            _configRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    // ============================================================================================
    // Constructor null-argument tests
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullConfigRepository_Throws()
    {
        var act = () => new UpdateCertificationThresholdsHandler(
            configRepository: null!,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new UpdateCertificationThresholdsHandler(
            _configRepoMock.Object,
            unitOfWork: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new UpdateCertificationThresholdsHandler(
            _configRepoMock.Object,
            _unitOfWorkMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ============================================================================================
    // Handle argument-guard tests
    // ============================================================================================

    [Fact]
    public async Task Handle_WithNullRequest_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    // ============================================================================================
    // Happy path
    // ============================================================================================

    [Fact]
    public async Task Handle_HappyPath_UpdatesConfigAndPersists()
    {
        var config = CertificationThresholdsConfig.Seed();
        var userId = Guid.NewGuid();

        _configRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var sequence = new List<string>();
        _configRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<CertificationThresholdsConfig>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("config.Update"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("uow.Save"))
            .ReturnsAsync(1);

        var command = new UpdateCertificationThresholdsCommand(
            MinCoveragePct: 75m,
            MaxPageTolerance: 5,
            MinBggMatchPct: 85m,
            MinOverallScore: 65m,
            UserId: userId);

        var before = DateTimeOffset.UtcNow;
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);
        var after = DateTimeOffset.UtcNow;

        result.Should().Be(MediatR.Unit.Value);

        // Aggregate state mutated by Update(thresholds, userId).
        config.Thresholds.Should().Be(new CertificationThresholds(75m, 5, 85m, 65m));
        config.UpdatedByUserId.Should().Be(userId);
        config.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);

        // Repository + UoW were called exactly once each, in Update-then-Save order.
        _configRepoMock.Verify(
            r => r.GetAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _configRepoMock.Verify(
            r => r.UpdateAsync(config, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        sequence.Should().Equal("config.Update", "uow.Save");
    }

    // ============================================================================================
    // Defense-in-depth: factory rejects out-of-bounds inputs even if validation is bypassed
    // ============================================================================================

    [Fact]
    public async Task Handle_WithOutOfBoundsCoverage_PropagatesArgumentExceptionAndDoesNotPersist()
    {
        var config = CertificationThresholdsConfig.Seed();
        _configRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // 150m violates CertificationThresholds.Create's 0..100 bound on MinCoveragePct.
        var command = new UpdateCertificationThresholdsCommand(
            MinCoveragePct: 150m,
            MaxPageTolerance: 5,
            MinBggMatchPct: 85m,
            MinOverallScore: 65m,
            UserId: Guid.NewGuid());

        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();

        _configRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<CertificationThresholdsConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
