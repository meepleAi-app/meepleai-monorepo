using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to upload a rulebook PDF for a game with content-hash deduplication.
/// </summary>
internal record AddRulebookCommand(
    Guid GameId,
    Guid UserId,
    IFormFile File
) : ICommand<RulebookUploadResult>;
