using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Core;

/// <summary>
/// Seeds AI model configurations (OpenRouter + Ollama) via MediatR command.
/// </summary>
internal static class AiModelSeeder
{
    public static async Task SeedAsync(IMediator mediator, ILogger logger, CancellationToken ct)
    {
        logger.LogInformation("Seeding AI models...");
        await mediator.Send(
            new Api.BoundedContexts.SystemConfiguration.Application.Commands.SeedAiModelsCommand(), ct)
            .ConfigureAwait(false);
    }
}
