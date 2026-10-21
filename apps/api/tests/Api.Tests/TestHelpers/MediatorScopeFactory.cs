using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Un <see cref="IServiceScopeFactory"/> i cui scope risolvono il mediator dato.
/// </summary>
/// <remarks>
/// <c>FeatureFlagService</c> crea la riga di un flag in uno scope suo (#4599) e vi risolve
/// <see cref="IMediator"/>. Nei test unitari il mediator è un doppio: questo fa sì che lo scope lo restituisca,
/// così le verifiche sui comandi inviati restano quelle di prima.
/// </remarks>
internal static class MediatorScopeFactory
{
    public static IServiceScopeFactory For(IMediator mediator) =>
        new ServiceCollection()
            .AddSingleton(mediator)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
}
