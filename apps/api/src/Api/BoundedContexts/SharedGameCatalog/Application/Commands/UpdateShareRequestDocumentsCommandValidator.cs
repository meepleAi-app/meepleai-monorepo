using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UpdateShareRequestDocumentsCommand.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
internal sealed class UpdateShareRequestDocumentsCommandValidator : AbstractValidator<UpdateShareRequestDocumentsCommand>
{
    private readonly IPdfDocumentRepository _documentRepository;

    public UpdateShareRequestDocumentsCommandValidator(
        IPdfDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));

        ConfigureRules();
    }

    private void ConfigureRules()
    {
        RuleFor(x => x.ShareRequestId)
            .NotEmpty()
            .WithMessage("ShareRequestId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.DocumentIds)
            .NotNull()
            .WithMessage("DocumentIds cannot be null")
            .Must(docs => docs.Count <= ShareRequest.MaxDocumentCount)
            .WithMessage($"Cannot attach more than {ShareRequest.MaxDocumentCount} documents to a share request");

        // All documents must belong to the user
        RuleFor(x => x)
            .MustAsync(AllDocumentsBelongToUserAsync)
            .WithMessage("One or more documents do not belong to you")
            .When(x => x.DocumentIds != null && x.DocumentIds.Count > 0);
    }

    private async Task<bool> AllDocumentsBelongToUserAsync(
        UpdateShareRequestDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.DocumentIds == null || command.DocumentIds.Count == 0)
            return true;

        var documents = await _documentRepository.GetByIdsAsync(
            command.DocumentIds,
            cancellationToken).ConfigureAwait(false);

        // Check if all requested documents exist and belong to the user
        return documents.Count == command.DocumentIds.Count &&
               documents.All(d => d.UploadedByUserId == command.UserId);
    }
}
