using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// #4169 (ADR-095 D5/D8, pagina «Modelli e prezzi»): il prezzo di un modello del catalogo, in USD per
/// milione di token. È il valore che il gateway usa per il costo, il tetto e il degrado.
/// </summary>
/// <remarks>
/// Il PUT del modello sovrascrive anche priorità, stato e impostazioni di generazione: per cambiare
/// solo il prezzo serviva rimandare tutto. Questo comando tocca il prezzo e basta.
/// </remarks>
internal sealed record UpdateAiModelPricingCommand(
    Guid Id,
    decimal InputPricePerMillion,
    decimal OutputPricePerMillion
) : ICommand<AiModelDto>;
