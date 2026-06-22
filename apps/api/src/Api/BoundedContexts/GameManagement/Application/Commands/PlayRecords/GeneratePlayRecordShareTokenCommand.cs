using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to generate a public share token for a play record. Creator-only (#2437-2).
/// </summary>
internal record GeneratePlayRecordShareTokenCommand(Guid PlayRecordId, Guid UserId) : ICommand<ShareLinkResponse>;
