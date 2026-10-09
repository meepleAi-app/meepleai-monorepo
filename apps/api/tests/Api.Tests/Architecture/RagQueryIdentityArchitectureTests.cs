using System.Reflection;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #4137 — l'identità del chiamante è un parametro OBBLIGATORIO sulle query
/// che fanno recupero RAG scopato per gioco.
///
/// <para>
/// <b>Perché strutturale e non un test di comportamento.</b> La DoD della issue chiedeva
/// «un test sul percorso che spedisce, non solo sul servizio: una guardia corretta e un
/// endpoint che non la invoca danno lo stesso verde». È esatto, e nessun test del gestore
/// può darla: <c>StreamQaQueryHandler</c> aveva la guardia giusta e la eseguiva dentro
/// <c>if (query.UserId.HasValue &amp;&amp; …)</c>, quindi un endpoint che semplicemente non
/// passava l'identità saltava l'autorizzazione e compilava. È ciò che faceva
/// <c>RagDashboardEndpoints</c>, costruendo la query con <c>gameId</c> + <c>query</c> e
/// nient'altro: innocuo solo perché quella rotta è admin-gated e la regola 1 concede agli
/// admin — cioè la guardia teneva per coincidenza.
/// </para>
/// <para>
/// Un parametro obbligatorio rende quel difetto <b>inesprimibile</b>: il compilatore
/// nomina ogni chiamante. Questo test difende la proprietà che lo rende tale, perché
/// riportare il parametro a <c>Guid?</c> con default non romperebbe nulla e riaprirebbe
/// il buco in silenzio.
/// </para>
/// <para>
/// Segue il pattern di <c>ConstantTimeComparisonArchitectureTests</c>: un inventario
/// dichiarato che non può divergere dal codice senza diventare rosso.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.Security)]
[Trait("BoundedContext", "Architecture")]
public class RagQueryIdentityArchitectureTests
{
    /// <summary>
    /// Le query il cui gestore chiama <c>IRagAccessService</c>, con il nome dei parametri
    /// che portano l'identità. Aggiungendo una query che fa recupero scopato per gioco,
    /// registrala qui.
    /// </summary>
    private static readonly (string TypeName, string UserIdParam, string RoleParam)[] s_ragScopedQueries =
    {
        ("Api.BoundedContexts.KnowledgeBase.Application.Queries.StreamQaQuery", "UserId", "UserRole"),
        ("Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKnowledgeBaseStatusQuery",
            "RequestingUserId", "RequestingUserRole"),
    };

    private static Type ResolveQueryType(string typeName)
    {
        var assembly = typeof(Program).Assembly;
        var type = assembly.GetType(typeName, throwOnError: false);
        type.Should().NotBeNull(
            $"'{typeName}' è nell'inventario di questo test: se è stata rinominata o rimossa, " +
            "aggiorna l'inventario invece di lasciarlo divergere dal codice");
        return type!;
    }

    private static ParameterInfo FindPrimaryConstructorParameter(Type queryType, string parameterName)
    {
        // I record hanno un solo costruttore pubblico (il primario) salvo copy-ctor.
        var ctor = queryType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var parameter = ctor.GetParameters().SingleOrDefault(p => p.Name == parameterName);
        parameter.Should().NotBeNull(
            $"'{queryType.Name}' deve avere un parametro '{parameterName}' nel costruttore primario");
        return parameter!;
    }

    [Fact]
    public void RagScopedQueries_CarryTheUserIdAsANonNullableRequiredParameter()
    {
        foreach (var (typeName, userIdParam, _) in s_ragScopedQueries)
        {
            var queryType = ResolveQueryType(typeName);
            var parameter = FindPrimaryConstructorParameter(queryType, userIdParam);

            parameter.ParameterType.Should().Be(
                typeof(Guid),
                $"{queryType.Name}.{userIdParam} deve essere Guid e non Guid?: un'identità " +
                "annullabile rende la guardia del gestore condizionale, e un chiamante che la " +
                "omette salta l'autorizzazione compilando");

            parameter.HasDefaultValue.Should().BeFalse(
                $"{queryType.Name}.{userIdParam} non deve avere un valore di default: " +
                "con un default il compilatore smette di nominare i chiamanti che non la passano");
        }
    }

    [Fact]
    public void RagScopedQueries_CarryTheRoleAsANonNullableRequiredParameter()
    {
        foreach (var (typeName, _, roleParam) in s_ragScopedQueries)
        {
            var queryType = ResolveQueryType(typeName);
            var parameter = FindPrimaryConstructorParameter(queryType, roleParam);

            parameter.HasDefaultValue.Should().BeFalse(
                $"{queryType.Name}.{roleParam} non deve avere un valore di default — " +
                "il ruolo decide il bypass admin, e un default lo renderebbe implicito");
        }
    }

    /// <summary>
    /// L'identità deve precedere i parametri opzionali: è ciò che costringe ogni chiamante
    /// posizionale a dichiararla, e ciò che ha fatto fallire la compilazione dei due
    /// chiamanti reali quando il vincolo è stato introdotto.
    /// </summary>
    [Fact]
    public void RagScopedQueries_PlaceTheIdentityBeforeAnyOptionalParameter()
    {
        foreach (var (typeName, userIdParam, roleParam) in s_ragScopedQueries)
        {
            var queryType = ResolveQueryType(typeName);
            var ctor = queryType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            var parameters = ctor.GetParameters();
            var firstOptional = parameters.FirstOrDefault(p => p.HasDefaultValue);
            if (firstOptional is null)
            {
                continue; // nessun opzionale: l'ordine non può violare l'invariante
            }

            foreach (var name in new[] { userIdParam, roleParam })
            {
                var identity = parameters.Single(p => p.Name == name);
                identity.Position.Should().BeLessThan(
                    firstOptional.Position,
                    $"{queryType.Name}.{name} deve stare prima di '{firstOptional.Name}', " +
                    "il primo parametro opzionale");
            }
        }
    }
}
