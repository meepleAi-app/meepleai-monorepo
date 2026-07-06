using System;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Removes a photo from a GameNight recap gallery (#2724). Allowed for the uploader
/// or the night's organizer.
/// </summary>
internal record DeleteGameNightPhotoCommand(Guid GameNightId, Guid PhotoId, Guid UserId) : ICommand;
