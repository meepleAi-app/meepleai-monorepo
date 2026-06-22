using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class GameBookRoleTests
{
    [Fact]
    public void GameBookRole_SupportsMultiValuedFlags()
    {
        var allInOne = GameBookRole.Tutorial | GameBookRole.RulesReference
                     | GameBookRole.Narrative | GameBookRole.Encounter;

        Assert.True(allInOne.HasFlag(GameBookRole.Tutorial));
        Assert.True(allInOne.HasFlag(GameBookRole.RulesReference));
        Assert.True(allInOne.HasFlag(GameBookRole.Narrative));
        Assert.True(allInOne.HasFlag(GameBookRole.Encounter));
        Assert.False(allInOne.HasFlag(GameBookRole.Lore));
    }

    [Fact]
    public void GameBookRole_NoneIsZero()
    {
        Assert.Equal(0, (int)GameBookRole.None);
    }

    [Fact]
    public void ParagraphScheme_HasAllExpectedValues()
    {
        Assert.Equal(0, (int)ParagraphScheme.None);
        Assert.Equal(1, (int)ParagraphScheme.ParagraphNumber);
        Assert.Equal(2, (int)ParagraphScheme.PageNumber);
        Assert.Equal(3, (int)ParagraphScheme.Section);
    }
}
