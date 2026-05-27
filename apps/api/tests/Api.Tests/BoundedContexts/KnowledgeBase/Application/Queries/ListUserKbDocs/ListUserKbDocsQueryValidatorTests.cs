using Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ListUserKbDocsQueryValidatorTests
{
    private readonly ListUserKbDocsQueryValidator _sut = new();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Valid_default_query_passes() =>
        _sut.TestValidate(new ListUserKbDocsQuery(UserId)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_less_than_1_fails(int page) =>
        _sut.TestValidate(new ListUserKbDocsQuery(UserId, Page: page))
            .ShouldHaveValidationErrorFor(x => x.Page);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_outside_1_to_100_fails(int size) =>
        _sut.TestValidate(new ListUserKbDocsQuery(UserId, PageSize: size))
            .ShouldHaveValidationErrorFor(x => x.PageSize);

    [Theory]
    [InlineData("recent")]
    [InlineData(null)]
    public void SortBy_recent_or_null_passes(string? sort) =>
        _sut.TestValidate(new ListUserKbDocsQuery(UserId, SortBy: sort))
            .ShouldNotHaveValidationErrorFor(x => x.SortBy);

    [Theory]
    [InlineData("title")]
    [InlineData("oldest")]
    [InlineData("unknown")]
    public void SortBy_unknown_value_fails(string sort) =>
        _sut.TestValidate(new ListUserKbDocsQuery(UserId, SortBy: sort))
            .ShouldHaveValidationErrorFor(x => x.SortBy);

    [Theory]
    [InlineData("ready")]
    [InlineData("all")]
    [InlineData(null)]
    public void State_ready_all_or_null_passes(string? state) =>
        _sut.TestValidate(new ListUserKbDocsQuery(UserId, State: state))
            .ShouldNotHaveValidationErrorFor(x => x.State);

    [Theory]
    [InlineData("pending")]
    [InlineData("failed")]
    [InlineData("nonsense")]
    public void State_unknown_value_fails(string state) =>
        _sut.TestValidate(new ListUserKbDocsQuery(UserId, State: state))
            .ShouldHaveValidationErrorFor(x => x.State);

    [Fact]
    public void UserId_empty_guid_fails() =>
        _sut.TestValidate(new ListUserKbDocsQuery(Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.UserId);
}
