using System.Reflection;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4599 — nessun comando che arriva a <c>IFeatureFlagService</c> può essere <c>[AtomicAudit]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>FeatureFlagService</c> crea la riga di un flag in uno scope DI suo (<c>CreateRowAsync</c>), così una
/// creazione che perde la gara con un'altra richiesta (23505) non sporca il <c>DbContext</c> del chiamante. Ne
/// segue che quella creazione fa commit da sola. Sotto <c>[AtomicAudit]</c>, <c>AuditLoggingBehavior</c> apre
/// la transazione sul <c>DbContext</c> del chiamante e la creazione nello scope proprio sopravvivrebbe al
/// suo rollback: la mutazione e la riga di audit non sarebbero più atomiche.
/// </para>
/// <para>
/// Oggi nessuno dei comandi lo è. Questo test lo tiene vero: trova i command handler che prendono
/// <c>IFeatureFlagService</c> nel costruttore e controlla l'attributo sulla richiesta che gestiscono. Non vede
/// un comando che arriva al servizio per un'altra strada (un servizio intermedio): per quelli resta la
/// riga di avvertimento nella documentazione di <c>CreateRowAsync</c>.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4599")]
public sealed class FeatureFlagWritersAreNotAtomicAuditArchitectureTests
{
    /// <summary>I quattro comandi che scrivono un flag, per nome: il test sotto fallisce se la scansione non li trova.</summary>
    private static readonly Type[] KnownWriters =
    [
        typeof(UpdateFeatureFlagCommand),
        typeof(EnableFeatureForTierCommand),
        typeof(DisableFeatureForTierCommand),
        typeof(ToggleRagEnhancementCommand),
    ];

    /// <summary>Le richieste gestite da una classe che prende <c>IFeatureFlagService</c> nel costruttore.</summary>
    private static List<Type> RequestsHandledByAFeatureFlagUser() =>
        typeof(Program).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false })
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IFeatureFlagService))))
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                         i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))
            .Select(i => i.GetGenericArguments()[0])
            .Where(r => !r.IsGenericParameter)
            .Distinct()
            .ToList();

    [Fact]
    public void TheScanFindsTheKnownWriters()
    {
        var found = RequestsHandledByAFeatureFlagUser();

        // Un elenco vuoto o incompleto farebbe passare il test sotto per niente.
        found.Should().Contain(KnownWriters,
            "la scansione dei command handler deve trovare i quattro comandi che scrivono un flag");
    }

    [Fact]
    public void NoCommandThatReachesTheFeatureFlagServiceIsAtomicAudit()
    {
        var atomic = RequestsHandledByAFeatureFlagUser()
            .Where(r => r.GetCustomAttribute<AtomicAuditAttribute>() is not null)
            .Select(r => r.FullName)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Joined, so the failure lists every command: BeEmpty on the list shows only the first.
        string.Join(Environment.NewLine, atomic).Should().BeEmpty(
            "FeatureFlagService.CreateRowAsync commits in a DI scope of its own, so under [AtomicAudit] — whose " +
            "transaction lives on the caller's DbContext — a row it created would survive the rollback. " +
            "Either remove [AtomicAudit] from the command, or give CreateRowAsync a way to join the caller's unit of work.");
    }
}
