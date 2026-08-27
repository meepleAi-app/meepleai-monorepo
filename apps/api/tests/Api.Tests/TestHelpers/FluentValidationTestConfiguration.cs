using System.Runtime.CompilerServices;
using Api.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Applica la configurazione FluentValidation di produzione a tutta la suite.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddFluentValidation()</c> imposta <c>ValidatorOptions.Global.DefaultRuleLevelCascadeMode</c>
/// su <c>Stop</c> (#3847). E' stato globale e mutabile: senza questo inizializzatore, i test che
/// costruiscono un validatore a mano girerebbero con la cascata di libreria (<c>Continue</c>) o
/// con quella di produzione a seconda di quale classe di test viene caricata prima — cioe' con un
/// esito che dipende dall'ordine di esecuzione.
/// </para>
/// <para>
/// Il ModuleInitializer gira prima di qualunque test e toglie l'ordine dall'equazione: tutta la
/// suite valida nelle stesse condizioni della produzione.
/// </para>
/// </remarks>
internal static class FluentValidationTestConfiguration
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Si invoca il vero metodo di registrazione, non una copia delle sue impostazioni:
        // una copia divergerebbe in silenzio.
        new ServiceCollection().AddFluentValidation();
    }
}
