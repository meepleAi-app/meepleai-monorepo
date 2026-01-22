using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for ApproveShareRequestCommand.
/// </summary>
internal sealed class ApproveShareRequestCommandValidator : AbstractValidator<ApproveShareRequestCommand>
{
    private readonly IShareRequestRepository _shareRequestRepository;

    public ApproveShareRequestCommandValidator(IShareRequestRepository shareRequestRepository)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));

        ConfigureRules();
    }

    private void ConfigureRules()
    {
        RuleFor(x => x.ShareRequestId)
            .NotEmpty()
            .WithMessage("ShareRequestId is required");

        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("AdminId is required");

        RuleFor(x => x.AdminNotes)
            .MaximumLength(2000)
            .WithMessage("Admin notes cannot exceed 2000 characters")
            .When(x => x.AdminNotes != null);

        RuleFor(x => x)
            .MustAsync(ShareRequestExistsAndInReviewAsync)
            .WithMessage("Share request not found or not in review status");

        RuleFor(x => x)
            .MustAsync(AdminIsCurrentReviewerAsync)
            .WithMessage("You are not the current reviewer of this share request");
    }

    private async Task<bool> ShareRequestExistsAndInReviewAsync(
        ApproveShareRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ShareRequestId == Guid.Empty)
            return true; // Let the NotEmpty rule handle this

        var shareRequest = await _shareRequestRepository.GetByIdAsync(
            command.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        return shareRequest != null && shareRequest.Status == ShareRequestStatus.InReview;
    }

    private async Task<bool> AdminIsCurrentReviewerAsync(
        ApproveShareRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ShareRequestId == Guid.Empty || command.AdminId == Guid.Empty)
            return true; // Let other rules handle this

        var shareRequest = await _shareRequestRepository.GetByIdAsync(
            command.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
            return true; // Let the exists rule handle this

        return shareRequest.ReviewingAdminId == command.AdminId;
    }
}
