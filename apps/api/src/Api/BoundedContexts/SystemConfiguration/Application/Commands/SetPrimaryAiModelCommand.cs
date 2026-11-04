using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// #4169: rende un modello il predefinito del suo provider. È ciò che <c>IsPrimary</c> significa a
/// runtime: <c>LlmProviderSelector</c> lo usa quando un provider entra nel fallback senza un modello
/// già scelto. Un provider ha un solo predefinito, quindi gli altri suoi modelli lo perdono.
/// </summary>
/// <remarks>
/// La pagina admin chiamava <c>POST /admin/ai-models/set-primary</c>, una rotta che non esisteva.
/// </remarks>
internal sealed record SetPrimaryAiModelCommand(Guid Id) : ICommand<AiModelDto>;
