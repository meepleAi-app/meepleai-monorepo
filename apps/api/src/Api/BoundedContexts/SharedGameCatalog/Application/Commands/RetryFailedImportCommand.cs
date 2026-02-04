using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to retry a failed BGG import
/// Issue #3541: BGG Import Queue Service
/// </summary>
public record RetryFailedImportCommand(Guid Id) : IRequest<bool>;
