using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BulkEnqueueCatalogSeedsCommandHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly BulkEnqueueCatalogSeedsCommandHandler _handler;
    private readonly Guid _actor = Guid.NewGuid();

    public BulkEnqueueCatalogSeedsCommandHandlerTests()
    {
        _handler = new BulkEnqueueCatalogSeedsCommandHandler(
            _mediatorMock.Object,
            NullLogger<BulkEnqueueCatalogSeedsCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_AllNew_ReturnsEnqueuedCountAndIds()
    {
        var bggIds = new[] { 1, 2, 3 };
        var generatedIds = bggIds.ToDictionary(b => b, _ => Guid.NewGuid());
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<EnqueueCatalogSeedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object req, CancellationToken _) =>
            {
                var c = (EnqueueCatalogSeedCommand)req;
                return new EnqueueCatalogSeedResult(
                    generatedIds[c.BggId!.Value],
                    c.BggId,
                    nameof(CatalogSeedStatus.Pending),
                    IsDuplicate: false);
            });

        var result = await _handler.Handle(
            new BulkEnqueueCatalogSeedsCommand(bggIds, _actor),
            TestContext.Current.CancellationToken);

        result.Total.Should().Be(3);
        result.Enqueued.Should().Be(3);
        result.Duplicates.Should().Be(0);
        result.NewDraftIds.Should().BeEquivalentTo(generatedIds.Values);

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<EnqueueCatalogSeedCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_MixDuplicates_ReportsBothCounters()
    {
        var bggIds = new[] { 10, 20, 30 };
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<EnqueueCatalogSeedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object req, CancellationToken _) =>
            {
                var c = (EnqueueCatalogSeedCommand)req;
                var isDup = c.BggId == 20; // middle id is duplicate
                return new EnqueueCatalogSeedResult(
                    isDup ? Guid.Empty : Guid.NewGuid(),
                    c.BggId,
                    nameof(CatalogSeedStatus.Pending),
                    IsDuplicate: isDup);
            });

        var result = await _handler.Handle(
            new BulkEnqueueCatalogSeedsCommand(bggIds, _actor),
            TestContext.Current.CancellationToken);

        result.Total.Should().Be(3);
        result.Enqueued.Should().Be(2);
        result.Duplicates.Should().Be(1);
        result.NewDraftIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public void Validator_BatchTooLarge_FailsAt100Cap()
    {
        var validator = new BulkEnqueueCatalogSeedsCommandValidator();
        var ids = Enumerable.Range(1, 101).ToArray();
        var cmd = new BulkEnqueueCatalogSeedsCommand(ids, _actor);

        var result = validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.BggIds)
            .WithErrorMessage("Max 100 BGG IDs per batch.");
    }

    [Fact]
    public void Validator_ExactlyCap_PassesBatchSize()
    {
        var validator = new BulkEnqueueCatalogSeedsCommandValidator();
        var ids = Enumerable.Range(1, 100).ToArray();
        var cmd = new BulkEnqueueCatalogSeedsCommand(ids, _actor);

        var result = validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.BggIds);
    }

    [Fact]
    public void Validator_EmptyBatch_Fails()
    {
        var validator = new BulkEnqueueCatalogSeedsCommandValidator();
        var cmd = new BulkEnqueueCatalogSeedsCommand(Array.Empty<int>(), _actor);

        var result = validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.BggIds);
    }

    [Fact]
    public void Constructor_NullMediator_Throws()
    {
        var act = () => new BulkEnqueueCatalogSeedsCommandHandler(
            mediator: null!,
            NullLogger<BulkEnqueueCatalogSeedsCommandHandler>.Instance);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediator");
    }
}
