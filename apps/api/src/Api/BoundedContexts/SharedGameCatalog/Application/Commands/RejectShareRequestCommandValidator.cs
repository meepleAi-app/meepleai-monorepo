using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for RejectShareRequestCommand.
/// </summary>
internal sealed class RejectShareRequestCommandValidator : AbstractValidator<RejectShareRequestCommand>
{
    private readonly IShareRequestRepository _shareRequestRepository;

    public RejectShareRequestCommandValidator(IShareRequestRepository shareRequestRepository)
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

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Rejection reason is required")
            .MinimumLength(10)
            .WithMessage("Please provide a meaningful reason (at least 10 characters)")
            .MaximumLength(2000)
            .WithMessage("Reason cannot exceed 2000 characters");

        RuleFor(x => x)
            .MustAsync(ShareRequestExistsAndInReviewAsync)
            .WithMessage("Share request not found or not in review status");

        RuleFor(x => x)
            .MustAsync(AdminIsCurrentReviewerAsync)
            .WithMessage("You are not the current reviewer of this share request");
    }

    private async Task<bool> ShareRequestExistsAndInReviewAsync(
        RejectShareRequestCommand command,
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
        RejectShareRequestCommand command,
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
