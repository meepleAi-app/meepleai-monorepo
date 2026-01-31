using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to upvote a FAQ.
/// Issue #2681: Public FAQs endpoints
/// </summary>
/// <param name="FaqId">The ID of the FAQ to upvote.</param>
internal record UpvoteFaqCommand(Guid FaqId) : ICommand<UpvoteFaqResultDto>;
