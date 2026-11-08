using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// #4727: <c>%</c> and <c>_</c> typed in the user search are characters, not ILIKE wildcards. The query itself
/// is covered against PostgreSQL in <c>UserProfileRepositorySearchIntegrationTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class UserProfileRepositoryEscapeLikeTests
{
    [Theory]
    [InlineData("anna", "anna")]
    [InlineData("a_b", @"a\_b")]
    [InlineData("100%", @"100\%")]
    [InlineData("__", @"\_\_")]
    [InlineData("%%", @"\%\%")]
    [InlineData(@"a\b", @"a\\b")]
    [InlineData(@"\%", @"\\\%")]
    public void EscapeLikePattern_MakesTheWildcardsLiteral(string term, string expected)
    {
        UserProfileRepository.EscapeLikePattern(term).Should().Be(expected);
    }

    [Fact]
    public void EscapeLikePattern_RejectsNull()
    {
        var act = () => UserProfileRepository.EscapeLikePattern(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
