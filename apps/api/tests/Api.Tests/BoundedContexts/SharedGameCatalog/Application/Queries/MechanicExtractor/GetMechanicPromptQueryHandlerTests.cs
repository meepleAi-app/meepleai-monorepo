using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Tests for <see cref="GetMechanicPromptQueryHandler"/> (#539 follow-up). Uses the REAL
/// <see cref="EmbeddedMechanicPromptProvider"/> so the test also proves every per-section
/// markdown resource (incl. the v1.1.0 additions setup/components/endgame) is embedded and
/// loadable — a mock would hide a missing embedded resource.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetMechanicPromptQueryHandlerTests
{
    private static GetMechanicPromptQueryHandler CreateHandler() =>
        new(new EmbeddedMechanicPromptProvider());

    [Fact]
    public async Task Handle_ReturnsPromptVersion_SystemPrompt_AndEverySection()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMechanicPromptQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.PromptVersion.Should().Be("v1.1.0");
        result.SystemPrompt.Should().NotBeNullOrWhiteSpace();

        var expectedCount = Enum.GetValues<MechanicSection>().Length;
        result.Sections.Should().HaveCount(expectedCount);
        result.Sections.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Prompt));
        result.Sections.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.SectionName));
    }

    [Fact]
    public async Task Handle_IncludesV11Sections_SetupComponentsEndgame()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMechanicPromptQuery(), CancellationToken.None);

        var names = result.Sections.Select(s => s.SectionName).ToList();
        names.Should().Contain(nameof(MechanicSection.Setup));
        names.Should().Contain(nameof(MechanicSection.Components));
        names.Should().Contain(nameof(MechanicSection.EndgameScoring));
    }

    [Fact]
    public async Task Handle_SectionsAreOrderedByEnumValueAscending()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMechanicPromptQuery(), CancellationToken.None);

        var ordinals = result.Sections.Select(s => s.Section).ToList();
        ordinals.Should().BeInAscendingOrder();
        ordinals.Should().Equal(
            Enum.GetValues<MechanicSection>().Select(s => (int)s).OrderBy(v => v));
    }

    [Fact]
    public async Task Handle_NullRequest_Throws()
    {
        var handler = CreateHandler();

        var act = async () => await handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
