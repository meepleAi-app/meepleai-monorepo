using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAllAgentsQueryValidatorTests
{
    private readonly GetAllAgentsQueryValidator _sut = new();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Global_query_no_scope_passes() =>
        _sut.TestValidate(new GetAllAgentsQuery()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Global_query_with_filters_no_scope_passes() =>
        _sut.TestValidate(new GetAllAgentsQuery(ActiveOnly: true, Type: "Tutor"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Scope_my_library_with_userId_passes() =>
        _sut.TestValidate(new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: UserId))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("global")]
    [InlineData("all")]
    [InlineData("mine")]
    public void Scope_unknown_value_fails(string scope) =>
        _sut.TestValidate(new GetAllAgentsQuery(Scope: scope, ScopeUserId: UserId))
            .ShouldHaveValidationErrorFor(x => x.Scope);

    [Fact]
    public void Scope_my_library_without_userId_fails() =>
        _sut.TestValidate(new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: null))
            .ShouldHaveValidationErrorFor(x => x.ScopeUserId);

    [Fact]
    public void Scope_my_library_with_empty_userId_fails() =>
        _sut.TestValidate(new GetAllAgentsQuery(Scope: "my-library", ScopeUserId: Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.ScopeUserId);
}
