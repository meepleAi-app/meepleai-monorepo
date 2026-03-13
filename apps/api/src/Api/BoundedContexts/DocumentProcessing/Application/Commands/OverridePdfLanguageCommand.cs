using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to override the detected language of a PDF document.
/// E5-2: Language Intelligence for Game Night Improvvisata.
/// Passing null clears the override, reverting to detected language.
/// </summary>
internal sealed record OverridePdfLanguageCommand(Guid PdfId, string? LanguageCode) : ICommand<Unit>;
