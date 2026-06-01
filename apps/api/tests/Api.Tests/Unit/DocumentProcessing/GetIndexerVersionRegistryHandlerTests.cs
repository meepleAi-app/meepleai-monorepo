using Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class GetIndexerVersionRegistryHandlerTests
{
    private readonly GetIndexerVersionRegistryHandler _handler = new();

    [Fact]
    public async Task Handle_ReturnsOnlySelectableVersions()
    {
        var result = await _handler.Handle(new GetIndexerVersionRegistryQuery(), CancellationToken.None);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(v => !string.Equals(v.Version, IndexerVersionRegistry.Legacy.Version, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_MarksCurrentVersion()
    {
        var result = await _handler.Handle(new GetIndexerVersionRegistryQuery(), CancellationToken.None);

        var current = result.Should().ContainSingle(v => v.IsCurrent).Subject;
        current.Version.Should().Be(IndexerVersionRegistry.Current.Version);
        current.DisplayName.Should().NotBeNullOrWhiteSpace();
    }
}
