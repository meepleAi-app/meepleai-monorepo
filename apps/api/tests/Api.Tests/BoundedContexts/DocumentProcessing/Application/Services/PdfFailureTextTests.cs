using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #4660 — il testo che un PDF fallito porta in <c>ProcessingError</c> è fisso e dipende solo dalla categoria.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4660")]
public sealed class PdfFailureTextTests
{
    public static TheoryData<ErrorCategory> Categories()
    {
        var data = new TheoryData<ErrorCategory>();
        foreach (var category in Enum.GetValues<ErrorCategory>())
        {
            data.Add(category);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void For_NamesTheCategory_AndNothingElse(ErrorCategory category)
    {
        PdfFailureText.For(category).Should().Be($"Processing failed ({category})");
    }

    [Fact]
    public void For_GivesADifferentTextToEveryCategory()
    {
        Enum.GetValues<ErrorCategory>()
            .Select(PdfFailureText.For)
            .Should().OnlyHaveUniqueItems("the category is what tells the failures apart once the detail is in the log");
    }
}
