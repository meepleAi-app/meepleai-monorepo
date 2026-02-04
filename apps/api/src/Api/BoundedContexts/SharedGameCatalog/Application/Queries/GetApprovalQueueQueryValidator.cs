using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for GetApprovalQueueQuery.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// </summary>
internal sealed class GetApprovalQueueQueryValidator : AbstractValidator<GetApprovalQueueQuery>
{
    public GetApprovalQueueQueryValidator()
    {
        // All filters are optional, so validation is minimal
        // Just ensure the query is not null (handled by handler)
    }
}
