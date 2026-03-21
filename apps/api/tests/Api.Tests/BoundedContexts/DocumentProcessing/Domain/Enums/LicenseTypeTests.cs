using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Unit tests for LicenseType enum and IsCopyrightFree() extension.
/// RAG Copyright KB Cards: determines verbatim vs paraphrased citation mode.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class LicenseTypeTests
{
    [Fact]
    public void LicenseType_Has_Three_Values()
    {
        var values = Enum.GetValues<LicenseType>();
        values.Length.Should().Be(3);
    }

    [Fact]
    public void LicenseType_Copyrighted_Is_Default_Zero()
    {
        ((int)LicenseType.Copyrighted).Should().Be(0);
    }

    [Theory]
    [InlineData(LicenseType.CreativeCommons)]
    [InlineData(LicenseType.PublicDomain)]
    public void LicenseType_IsCopyrightFree_Returns_True_For_Free_Types(LicenseType type)
    {
        type.IsCopyrightFree().Should().BeTrue();
    }

    [Fact]
    public void LicenseType_IsCopyrightFree_Returns_False_For_Copyrighted()
    {
        LicenseType.Copyrighted.IsCopyrightFree().Should().BeFalse();
    }
}
