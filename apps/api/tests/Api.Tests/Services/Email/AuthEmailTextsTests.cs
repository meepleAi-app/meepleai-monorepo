using System.Reflection;
using Api.Services.Email;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// #4812. <c>required</c> makes the compiler refuse an <see cref="AuthEmailTexts"/> that lacks a string, but
/// not one that has it copied from the other language: an Italian email can compile, and read, with a heading
/// still in English. The email tests catch what the recipient would notice in the sentences they quote; this
/// one walks every member, so a string nobody quoted is covered too.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4812")]
public sealed class AuthEmailTextsTests
{
    public static TheoryData<string> Members()
    {
        var names = new TheoryData<string>();
        foreach (var property in typeof(AuthEmailTexts).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            names.Add(property.Name);

        return names;
    }

    [Theory]
    [MemberData(nameof(Members))]
    public void Every_Italian_text_differs_from_its_English_one(string member)
    {
        var english = Render(AuthEmailTexts.For("en"), member);
        var italian = Render(AuthEmailTexts.For("it"), member);

        english.Should().NotBeNullOrWhiteSpace();
        italian.Should().NotBeNullOrWhiteSpace();
        italian.Should().NotBe(english, $"{member} must be written in Italian, not copied from English");
    }

    // A member of a shape not rendered here fails its own case with this exception: it is never skipped.
    private static string Render(AuthEmailTexts texts, string member)
    {
        var value = typeof(AuthEmailTexts).GetProperty(member)!.GetValue(texts);
        return value switch
        {
            string text => text,
            Func<string, string> withText => withText("X"),
            Func<int, string> withNumber => withNumber(7),
            _ => throw new InvalidOperationException($"{member} has a shape this test does not render."),
        };
    }
}
