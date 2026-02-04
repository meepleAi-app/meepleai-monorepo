using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to cancel a queued BGG import
/// Issue #3541: BGG Import Queue Service
/// </summary>
public record CancelQueuedImportCommand(Guid Id) : IRequest<bool>;
