using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Arbitro Agent management and beta testing.
/// Issue #4328: Arbitro Agent Beta Testing and Admin Tools.
/// </summary>
internal static class ArbitroAdminEndpoints
{
    public static void MapArbitroAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/agents/arbitro")
            .WithTags("Admin - Arbitro Agent")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // POST /api/v1/admin/agents/arbitro/generate-faq
        // Generate FAQ from validated conflict resolution
        group.MapPost("/generate-faq", GenerateFaqFromValidation)
            .WithName("GenerateFaqFromValidation")
            .WithSummary("Auto-generate FAQ entry from validated conflict")
            .WithDescription(@"
Analyzes a specific validation with user feedback to auto-generate FAQ entry.

**Eligibility Criteria**:
- Validation had conflicts (not FAQ fast-path)
- User feedback confirms AI was correct (Accuracy = Correct)
- AI confidence was high (>0.85)
- User rating was positive (≥4 stars)

**Process**:
1. Extracts conflict pattern from validation
2. Checks if FAQ already exists
3. Creates new FAQ entry with inferred resolution
4. Optional auto-approve or admin review required

**Request**:
- validationId: Correlation ID from validation
- autoApprove: Whether to activate FAQ immediately (default: false)

**Response**:
- faqId: Generated FAQ ID (null if not eligible or already exists)
");
    }

    private static async Task<IResult> GenerateFaqFromValidation(
        [FromBody] GenerateFaqRequest req,
        [FromServices] IMediator mediator,
        CancellationToken ct)
    {
        var command = new GenerateFaqFromValidationCommand
        {
            ValidationId = req.ValidationId,
            AutoApprove = req.AutoApprove
        };

        var faqId = await mediator.Send(command, ct).ConfigureAwait(false);

        if (faqId == null)
        {
            return Results.BadRequest(new
            {
                error = "Validation not eligible for FAQ generation",
                message = "Check eligibility criteria: conflicts, correct feedback, high confidence, positive rating"
            });
        }

        return Results.Ok(new
        {
            faqId,
            message = req.AutoApprove
                ? "FAQ generated and activated"
                : "FAQ generated, pending admin review"
        });
    }
}

/// <summary>
/// Request to generate FAQ from validation.
/// </summary>
internal record GenerateFaqRequest(
    Guid ValidationId,
    bool AutoApprove = false
);
