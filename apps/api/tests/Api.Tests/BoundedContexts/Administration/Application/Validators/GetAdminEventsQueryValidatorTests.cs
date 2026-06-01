using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Unit tests for <see cref="GetAdminEventsQueryValidator"/>.
/// Validates that <see cref="GetAdminEventsQuery.Limit"/> is constrained to [1, 1000].
/// All other fields are optional — no validation rules required.
///
/// F4.1 issue #1718 — Task 1.1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1718")]
public sealed class GetAdminEventsQueryValidatorTests
{
    private readonly GetAdminEventsQueryValidator _validator = new();

    [Fact]
    public void Validate_LimitNegative_ShouldFail()
    {
        var query = new GetAdminEventsQuery(Limit: -1);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Validate_LimitZero_ShouldFail()
    {
        var query = new GetAdminEventsQuery(Limit: 0);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Validate_LimitOne_ShouldPass()
    {
        var query = new GetAdminEventsQuery(Limit: 1);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Validate_LimitOneThousand_ShouldPass()
    {
        var query = new GetAdminEventsQuery(Limit: 1000);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Validate_LimitOverMax_ShouldFail()
    {
        var query = new GetAdminEventsQuery(Limit: 1001);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Validate_DefaultQuery_ShouldPass()
    {
        // Default Limit = 100, all optional fields null.
        var query = new GetAdminEventsQuery();
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
